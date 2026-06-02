using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SAA_MsSql;
using SAA_PUBLIC;
using cTools;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using svrPair.Database;

namespace svrPair
{
    public class cPair
    {
        private static Ini mIni;        //存參數的INI檔
        private string mDbName = "";    //資料庫名稱(專案名稱)  "agvDB_1400004"
        private string mDbIp = "";      //網路位置
        private SqlHelper mSql;             //讀取資料庫方法的模組
        private DataTable mdtQuery;     //常用臨時資料表
        private DataTable moPairWay;    //存入口站可配對哪些出口站
        private bool mPanelDoB2C;       //將暫存區 B 的料送到生產區 C 的動作是由平板和WEB作的為True (原本為MCS作的，客戶要求改成平板)
        private bool mUnloadAuto;
        //FHT^N01^批號^料號^製單^列印日期 :::: FHT^N01^238090671^DMT6CVJ1536D^P3804231^202309181158
        private volatile Boolean mGo;   //判斷主執行緒是否在執行中的開關
        private Thread mThread;         //服務模組的主執行緒 

        #region [BgnPair == 啟動配對程序]
        public void BgnPair()
        {
            if (mGo != false) return;
            mGo = true;
            WriteLog("00.啟動配對程序");

            Initial();  //載入設定
            mThread = new Thread(MainProcess);
            mThread.Start();
        }
        #endregion

        #region [EndPair == 關閉配對程序]
        public void EndPair()
        {
            mGo = false;
            WriteLog("99.關閉配對程序");
        }
        #endregion

        #region [Setting == 設定 D、E 區的啟用停用]
        public void SettingBlockUseFlag(string Block, string UseFlag)
        {
            mSql.WriteSqlByAutoOpen("update oPort set UseFlag = @uf where Block = @blk", SP("@uf", UseFlag), SP("@blk", Block));
            WriteLog(string.Format("OA.設定{0}{1}", Block == "D" ? "生產區 D " : "下料區 E ", UseFlag == "Y" ? "啟用" : "停用"));
        }
        #endregion

        #region [Initial == 載入設定]
        public void Initial()
        {
            try
            {
                mIni = new Ini(System.IO.Directory.GetCurrentDirectory() + "\\Recipe.ini");
                mDbName = mIni.ReadValue("DbName", "Main");
                mDbIp = mIni.ReadValue("DbIp", "Main");
                mPanelDoB2C = (mIni.ReadValue("PanelDoB2C", "Main") == "T") ? true : false;         //將暫存區 B 的料送到生產區 C 的動作是由平板和WEB作的為True (原本為MCS作的，客戶要求改成平板)
                mUnloadAuto = (mIni.ReadValue("UnloadAuto", "Main") == "T") ? true : false;       //下料區的空RACK自動調派

                //mSql = new MsSql(mDbName, mDbIp);
                mSql = CreateSqlHelper();
            }
            catch (Exception ex) { Console.Write(ex.ToString()); }
            WriteLog("01.載入參數設定 >> Recipe.ini");

            LoadBasicSettingToTables();       //0.0 .主設定 == 載入基本設定檔--站點管理            
        }

        private SqlHelper CreateSqlHelper()
        {
            return new SqlHelper($"Data Source={mDbIp};Initial Catalog={mDbName};Persist Security Info=True;User ID=mcs;Password=Zz123456");
        }

        #endregion

        #region  [0-0 .主設定 == LoadBasicSettingToTables == 載入基本設定檔--站點管理]        
        private void LoadBasicSettingToTables()
        {
            try
            {
                moPairWay = mSql.QuerySqlByAutoOpen("select * from oPairWay").Tables[0];
            }
            catch
            { }
            WriteLog(string.Format("02.載入常用資料 >> oPairWay，IP = {1,-10}  DB = {2,-30} ", "", mDbIp, mDbName));
        }
        #endregion

        #region [MainProcess == 主要流程]
        private void MainProcess()  //可以加寫檢查若沒問題才呼叫Infinite()，否則就Terminate       
        {
            WriteLog("03.進入主要流程");
            WriteLog("===============");
            while (mGo)
            {
                // ★ 迴圈保命（第2層防護）：任一步驟的非預期例外都不得讓背景執行緒終止而閃退，
                //   記 log 後續行；單筆 oNeed 的精準隔離由 GenerateoRequireByoNeed 內層 try/catch 處理。
                try
                {
                    //由平板產生了oNeed
                    GenerateoRequireByoNeed();      //1-0 .主程式 == 轉成需求 : 尋找oNeed中AssignFlag = NULL的資料，以此產出oRequire關聯資料，而後註冊oPort的起終註記、oNeed中AssignFlag是Y正常或E異常

                    // ★★★ 工廠1修正：停用 B區→A區 自動派送 ★★★
                    // B區應該透過 Release 功能手動回送空板到 A區
                    // GenerateoNeedDataByMCSAsBtoA(); //1-2 .主程序 == 提出需要 : 將[B]暫存空Rack補至上料區[A]
                    // GenerateoRequireByoNeed();      //1-0 .主程式 == 轉成需求 :

                    if (mPanelDoB2C == false)//因應客戶要求自行由WEB程式和平板進行處理，故 MCS 不作處理
                    {
                        GenerateoNeedDataByMCSAsBtoC();    //1-1 .主程序 == 提出需要 : 從[B]上料暫存批配料號送至生產[C]
                    }
                    else
                    {   //搜尋來源是oNeed資料中AssignFlag = W(改成 P) , 起 = B3 , 終 = C5 ， 產生oNeed資料其起點 = C5 是來源資料終點，例終點 = A2 >> 指將空RACK送到上料區A 或 暫存區B 或 生產區 D (是否可用)
                        GenerateoNeedDataByMCSAsCtoABD();
                    }
                    GenerateoRequireByoNeed();      //1-0 .主程式 == 轉成需求 :

                    if (mUnloadAuto == true)
                    {
                        GenerateoNeedDataByMCSAsEtoABD();    //1-2 .主程序 == 提出需要 : 將[E]暫存空Rack補至[A、B、D]
                        GenerateoRequireByoNeed();      //1-0 .主程式 == 轉成需求 :
                    }
                    GenerateoNeedDataByMCSEsBtoF();     //1-2 .主程序 == 提出需要 : 從[E]下料暫存批配料號送至退pin[F]
                    GenerateoRequireByoNeed();       //1-0 .主程式 == 轉成需求 :

                    GenerateoMissionByoRequire();   //2-0 .主程序 == 尋找oRequire表中未指派的項目，產生oMission表
                    RecyclingoRequireByOkFlag();    //3-0 .主程序 == 處理oRequire表中的OkFlag欄位，Y=完成，X=異常結束，C=取消

                    //這一段不執行，異常的指派需由oMission的OkFlag改變處理，否則會一直輪迴新增刪除
                    //RecyclingoRequireByAssignFlag();//4-0 .主程序 == 處理oRequire表中的 AssignFlag 欄位為 Y、X、C

                    RecyclingoNeedByAssignFlag();   //5-0 .主程序 == 處理oNeed表中的 AssignFlag 欄位為 E、X、C
                }
                catch (Exception ex)
                {
                    WriteLog("98.主迴圈例外（已接住，續行，不終止進程）>> " + ex.Message, LogType.Error);
                }

                Thread.Sleep(50);
            }
        }
        #endregion




        #region  [Item 0.X == 產生oNeed : oNeed的來源一為平板叫車，分別是   從[A]上料送至暫存[B]、從[C]取空Rack搬移至[ABD]、從[D]將滿料運至下料[E]、從[E]下料成空Rack完移動[ABD]； ]
        // 平板叫車 :: A -> B
        // 平板叫車 :: C -> ABD
        // 平板叫車 :: D -> E
        // 平板叫車 :: E -> ABD 
        // 自動叫車 :: B >> A
        // 自動叫車 :: B >> C
        // 自動叫車 :: B >> D 暫時先不處理，若要使用和 B >> A 同一方法
        #endregion

        #region  [Item 1.X == 產生oNeed : oNeed的來源二為系統叫車，則是平板叫車完成後衍生出的需求，   從[B]暫存批配料號送至生產[C]、將[B]暫存空Rack補至生產區[A]  ]

        #region  [1-0 .主程式 == GenerateoRequireByoNeed == 尋找oNeed中AssignFlag = NULL的資料，以此產出oRequire入出和出入的關聯資料]        
        public void GenerateoRequireByoNeed()
        {
            DataTable dtoNeed = mSql.QuerySqlByAutoOpen("select * from oNeed where (AssignFlag is NULL or RTRIM(AssignFlag) ='') order by TaskDateTime").Tables[0];
            foreach (DataRow dr in dtoNeed.Rows)
            {
                // ★ 單筆隔離（第1層防護）：任一筆 oNeed 處理失敗只影響該筆，不得拖垮整個迴圈/進程。
                //   過去此處無 try/catch，毒資料拋 SqlException → 背景執行緒終止 → FleetManager 閃退。
                try
                {
                    ProcessSingleoNeed(dr);
                }
                catch (Exception ex)
                {
                    HandleoNeedRowException(dr, ex);
                }
            }
        }
        #endregion

        #region [1-0a .副程式 == ProcessSingleoNeed == 處理單筆 oNeed（依區域分派平板/系統配對；保留一廠分組差異）]
        private void ProcessSingleoNeed(DataRow dr)
        {
            string objStation = dr["ObjStation"].ToString();
            if (objStation.Length < 1)
            {
                // 站號空白屬資料異常：標 X 隔離，避免 Substring 例外與每輪重爆
                WriteLog("06.處理異常資料 >> 資料表 : oNeed , ObjStation 空白，標記 X 隔離", LogType.Warnning);
                QuarantineoNeedByTaskDateTime(dr["TaskDateTime"].ToString());
                return;
            }

            switch (objStation.Substring(0, 1))
            {               //這裡會執行的oNeed為人員選的有如下 == 平板的操作行為
                case "A":   //上料區A >> 暫存區B，將放RACK及製程前材料運至暫存區，如A1 >> B3
                case "C":   //生產區C >> 生產區D、上料區A、暫存區B，將空RACK運送至沒有RACK的地方，如C1 >> A1
                case "D":   //生產區D >> 下料區E，將放RACK及製程後材料運至下料區，如D2 >> E1
                case "F":   //下料區F >> 上料區A、生產區D、暫存區B，將下完料的空RACK運送至沒有RACK的地方，如E1 >> A1
                case "J":   // 3F 插針室
                case "H":   // 2F 成型後 -> 4F 烘烤前入貨區
                case "M":   // 2F 雷雕區 -> O/P/T
                case "T":   // 2F V cut區 -> O/P
                case "Q":   // 2F 出料區 -> 清洗區
                case "R":   // 2F 廢料區 -> 廢料回收區
                case "O":   // 2F OP上料區(左) -> M/Q/R (Release回送空板)
                case "P":   // 2F OP上料區(右) -> M/Q/R (Release回送空板)
                case "S":   // 2F 清洗區 -> M/Q/R (Release回送空板)
                case "N":   // 2F 廢料回收區 -> M/Q/R (Release回送空板)
                    WriteLog(string.Format("05.處理平板配對 >> ObjStation:{0} , EndStation:{1} , WorkOrder:{2} , RackId:{3}",
                        objStation, dr["EndStation"], dr["WorkOrder"], dr["RackId"]));
                    ProcessoNeedToRequire(objStation.Substring(0, 1), dr);
                    break;
                case "B":   //暫存區B >> 上料區A，將下完料的空RACK運送至沒有RACK的地方，如B1 >> A1
                            //暫存區B >> 生產區C，將放RACK及製程前材料運至生產區的地方，如B2 >> C1
                case "E":   //暫存區E >> 上料區F，將放RACK及製程完材料運至退pin區的地方，如E2 >> F1
                case "G":   // G區（1F電梯暫存區）→ J區（3F插針室）：Release 回送空板
                case "K":   // K區（4F烘烤前入貨區）→ H區（2F成型後）：Release 回送空板
                case "I":   // I區（3F品檢區）→ L區（4F烘烤後）：Release 回送空板 / NG回送
                case "L":   // L區（4F烘烤後）→ I區（3F品檢區）  ※一廠：L 歸系統配對群（與二廠不同，保留）
                    WriteLog(string.Format("05.處理系統配對 >> ObjStation:{0} , EndStation:{1} , WorkOrder:{2} , RackId:{3}",
                        objStation, dr["EndStation"], dr["WorkOrder"], dr["RackId"]));
                    ProcessoNeedToRequire(objStation.Substring(0, 1), dr);
                    break;
                default:
                    break;
            }
        }
        #endregion

        #region [1-0b .副程式 == 單筆 oNeed 例外處理（資料類隔離 / 連線類保留重試）]
        private void HandleoNeedRowException(DataRow dr, Exception ex)
        {
            string obj = SafeCol(dr, "ObjStation");
            string end = SafeCol(dr, "EndStation");
            string wo = SafeCol(dr, "WorkOrder");
            string rack = SafeCol(dr, "RackId");
            string task = SafeCol(dr, "TaskDateTime");

            if (IsDataException(ex))
            {
                // 資料類例外（語法/未閉合引號/截斷/約束）：重試也不會好 → 隔離標 X，
                // 交由 RecyclingoNeedByAssignFlag 清除，避免每輪重爆、log 狂洗。
                WriteLog(string.Format("06E.毒資料隔離 >> 將 oNeed 標 X , ObjStation:{0} , EndStation:{1} , WorkOrder:{2} , RackId:{3} , Error:{4}",
                    obj, end, wo, rack, ex.Message), LogType.Error);
                QuarantineoNeedByTaskDateTime(task);
            }
            else
            {
                // 連線/暫時性例外：保留重試，不標 X，避免 DB 短暫抖動誤隔離好資料。
                WriteLog(string.Format("06W.暫時性例外保留重試 >> ObjStation:{0} , EndStation:{1} , WorkOrder:{2} , RackId:{3} , Error:{4}",
                    obj, end, wo, rack, ex.Message), LogType.Warnning);
            }
        }

        private static string SafeCol(DataRow dr, string col)
        {
            try { return dr[col]?.ToString() ?? ""; }
            catch { return ""; }
        }

        /// <summary>SqlParameter 工廠：null 值自動轉 DBNull，供全檔參數化 SQL 使用。</summary>
        private static SqlParameter SP(string name, object value)
            => new SqlParameter(name, value ?? DBNull.Value);

        // 已知「資料類」SQL 錯誤碼（重試不會好）：語法/未閉合引號/截斷/約束/型別轉換/PK 重複等
        private static readonly System.Collections.Generic.HashSet<int> DataErrorNumbers =
            new System.Collections.Generic.HashSet<int> { 102, 103, 104, 105, 205, 206, 245, 257, 266, 515, 547, 2627, 2601, 2628, 8114, 8115, 8152 };

        private static bool IsDataException(Exception ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                var sql = e as System.Data.SqlClient.SqlException;
                if (sql != null)
                {
                    foreach (System.Data.SqlClient.SqlError err in sql.Errors)
                    {
                        if (DataErrorNumbers.Contains(err.Number)) return true;
                    }
                    return false; // 是 SqlException 但非已知資料錯誤 → 視為暫時性，保留重試
                }
            }
            return false; // 連線層或非 SQL 例外 → 保守視為暫時性，保留重試（避免誤隔離好資料）
        }

        private void QuarantineoNeedByTaskDateTime(string taskDateTime)
        {
            if (string.IsNullOrEmpty(taskDateTime)) return;
            try
            {
                // 只用 TaskDateTime（程式產生純數字、無注入風險）為 key。
                mSql.WriteSqlByAutoOpen("update oNeed set AssignFlag ='X' where TaskDateTime = @t", SP("@t", taskDateTime));
            }
            catch (Exception ex)
            {
                WriteLog("06X.隔離標記失敗 >> TaskDateTime:" + taskDateTime + " , Error:" + ex.Message, LogType.Warnning);
            }
        }
        #endregion

        #region [1-1 .主程序 == GenerateoNeedDataByMCStoC == 尋找oPort表中生產區C中，沒RACK即沒工單(終點) 再從  暫存區B中找有RACK和有工單(起點) == 考慮料號批配問題  ] 
        private void GenerateoNeedDataByMCSAsBtoC()    //從[B]上料暫存批配料號送至生產[C]
        {
            mdtQuery = GetoPort_NoRack_NoPair_CanWork_Sort_ByBlock("'C'");
            foreach (DataRow dr in mdtQuery.Rows)
            {
                //ProductionPartNo = X :表不管制  填入其他數值 : 表管制
                DataTable dt = GetoPort_PartNoTheSame_NoPair_CanWork_Sort_ByBlock("'B'", dr["ProductionPartNo"].ToString().Trim());
                if (dt.Rows.Count > 0)
                {
                    InsertoNeed(dt.Rows[0]["StationNo"].ToString(), dt.Rows[0]["RackId"].ToString(), dt.Rows[0]["WorkOrder"].ToString(), dr["StationNo"].ToString());
                    WriteLog(string.Format("04.產生配對資料 >> B區 -> C區 , ObjStation : {0} , EndStation : {1} , ProductionPartNo : {2} :: 從[B]暫存批配料號送至生產[C]", dt.Rows[0]["StationNo"].ToString(), dr["StationNo"].ToString(), dr["ProductionPartNo"].ToString()));
                }
                return;
            }
        }
        private void GenerateoNeedDataByMCSEsBtoF()    //從[E]下料暫存批配料號送至生產[F]
        {
            mdtQuery = GetoPort_NoRack_NoPair_CanWork_Sort_ByBlock("'F'");
            foreach (DataRow dr in mdtQuery.Rows)
            {
                //ProductionPartNo = X :表不管制  填入其他數值 : 表管制
                DataTable dt = GetoPort_PartNoTheSame_NoPair_CanWork_Sort_ByBlock("'E'", "X");
                if (dt.Rows.Count > 0)
                {
                    InsertoNeed(dt.Rows[0]["StationNo"].ToString(), dt.Rows[0]["RackId"].ToString(), dt.Rows[0]["WorkOrder"].ToString(), dr["StationNo"].ToString());
                    WriteLog(string.Format("04.產生配對資料 >> E區 -> F區 , ObjStation : {0} , EndStation : {1} , ProductionPartNo : {2} :: 從[E]下料暫存批配料號送至退pin[F]", dt.Rows[0]["StationNo"].ToString(), dr["StationNo"].ToString(), dr["ProductionPartNo"].ToString()));
                }
                return;
            }
        }
        #endregion

        #region [1-2 .主程序 == GenerateoNeedDataByMCStoA == 尋找oPort表中找上料區A缺少Rack的埠口(終點) 再從 暫存區B中找有Rack但沒工單(起點)] 
        private void GenerateoNeedDataByMCSAsBtoA()    //將[B]暫存空Rack補至上料區[A]
        {
            //找oPort表某一區域沒架子資料，條件是埠口是可用的、沒有Rack、沒被註冊、依權重排序 >> 找到A區有資料表示要從B區補
            mdtQuery = GetoPort_NoRack_NoPair_CanWork_Sort_ByBlock("'A'");
            foreach (DataRow dr in mdtQuery.Rows)
            {
                DataTable dt = GetoPort_HaveRack_NoPair_CanWork_Sort_ByBlock("'B'");
                if (dt.Rows.Count > 0)
                {
                    InsertoNeed(dt.Rows[0]["StationNo"].ToString(), dt.Rows[0]["RackId"].ToString(), "", dr["StationNo"].ToString());
                    WriteLog(string.Format("04.產生配對資料 >> B區 -> A區 ,  ObjStation : {0} , EndStation : {1} :: 將[B]暫存空Rack補至上料區[A]", dt.Rows[0]["StationNo"].ToString(), dr["StationNo"].ToString()));
                }
                return;
            }
        }

        private void GenerateoNeedDataByMCSAsEtoABD()    //將[F]暫存空Rack補至上料區[A、B、D]
        {
            //找oPort表某一區域沒架子資料，條件是埠口是可用的、沒有Rack、沒被註冊、依權重排序 >> 找到A區有資料表示要從B區補
            mdtQuery = GetoPort_NoRack_NoPair_CanWork_Sort_ByBlock("'A','B','D'");
            foreach (DataRow dr in mdtQuery.Rows)
            {
                DataTable dt = GetoPort_HaveRack_NoPair_CanWork_Sort_ByBlock("'F'");
                if (dt.Rows.Count > 0)
                {
                    InsertoNeed(dt.Rows[0]["StationNo"].ToString(), dt.Rows[0]["RackId"].ToString(), "", dr["StationNo"].ToString());
                    WriteLog(string.Format("04.產生配對資料 >> E區 -> A、B、D區 ,  ObjStation : {0} , EndStation : {1} :: 將[F]空Rack補至[A、B、D]", dt.Rows[0]["StationNo"].ToString(), dr["StationNo"].ToString()));
                }
                return;
            }
        }
        #endregion

        #region [1-3 .主程序 == GenerateoNeedDataByMCStoA == 將[C]暫存空Rack補至上料區[A]、暫存區[B]、生產區[D，然後再將前筆AssignFlag= W >> P 及退貨流程] 
        private void GenerateoNeedDataByMCSAsCtoABD()    //將[C]暫存空Rack補至上料區[A]、暫存區[B]、生產區[D，然後再將前筆AssignFlag= W >> P]
        {
            //搜尋來源是oNeed資料中AssignFlag = W(改成 P) , 起 = B3 , 終 = C5 ， 產生oNeed資料其起點 = C5 是來源資料終點，例終點 = A2 >> 指將空RACK送到上料區A 或 暫存區B 或 生產區 D (是否可用)
            //搜尋來源是oNeed資料中AssignFlag = R(改成 P) , 起 = B3 , 終 = C5 ， 產生oNeed資料其起點 = C5 是來源資料終點，例終點 = B2 >> 指將料盤送回暫存區B(是否可用)
            DataTable dt = mSql.QuerySqlByAutoOpen("select * from oNeed where AssignFlag in ('W','R') order by TaskDateTime").Tables[0];
            foreach (DataRow odr in dt.Rows)
            {
                //正常流程
                if (odr["AssignFlag"].ToString() == "W")
                {
                    mdtQuery = GetoPort_NoRack_NoPair_CanWork_Sort_ByBlock("'A','B','D'");
                    foreach (DataRow dr in mdtQuery.Rows)
                    {
                        InsertoNeed(odr["EndStation"].ToString(), odr["RackId"].ToString(), "", dr["StationNo"].ToString());
                        WriteLog(string.Format("04.產生配對資料 >> C區 -> ABD區 ,  ObjStation : {0} , EndStation : {1} :: 將[C]暫存空Rack補至[A、B、D]", odr["EndStation"].ToString(), dr["StationNo"].ToString()));
                        UpdateoNeedAssignFlag("P", odr["ObjStation"].ToString(), odr["EndStation"].ToString());
                        WriteLog(string.Format("04.更新配對資料 >> B區 -> C區 ,  ObjStation : {0} , EndStation : {1} :: 將[B]料盤[C]", odr["ObjStation"].ToString(), odr["EndStation"].ToString()));
                        return;
                    }
                }
                //退貨流程R=Reject
                else if (odr["AssignFlag"].ToString() == "R")
                {
                    string workOrder = mSql.QuerySqlByAutoOpen("select WorkOrder from oPort where StationNo = @sn", SP("@sn", odr["EndStation"].ToString())).Tables[0].Rows[0]["WorkOrder"].ToString();
                    mdtQuery = GetoPort_NoRack_NoPair_CanWork_Sort_ByBlock("'B'");
                    foreach (DataRow dr in mdtQuery.Rows)
                    {
                        InsertoNeed(odr["EndStation"].ToString(), odr["RackId"].ToString(), workOrder, dr["StationNo"].ToString());
                        WriteLog(string.Format("04.產生配對資料 >> C區 -> B區 ,  ObjStation : {0} , EndStation : {1} :: 將[C]料盤退貨至[B]", odr["EndStation"].ToString(), dr["StationNo"].ToString()));
                        UpdateoNeedAssignFlag("P", odr["ObjStation"].ToString(), odr["EndStation"].ToString());
                        WriteLog(string.Format("04.更新配對資料 >> B區 -> C區 ,  ObjStation : {0} , EndStation : {1} :: 將[B]料盤補至[C]", odr["ObjStation"].ToString(), odr["EndStation"].ToString()));
                        return;
                    }
                }


            }



        }
        #endregion

        #region  [1-4 .副程式 == ProcessoNeedToRequire == 處理各區需求]        
        private void ProcessoNeedToRequire(string Black, DataRow dr)
        {
            string WorkOrder = dr["WorkOrder"].ToString(); string ObjStation = dr["ObjStation"].ToString();
            string RackId = dr["RackId"].ToString(); string EndStation = dr["EndStation"].ToString();
            string TaskDateTime = dr["TaskDateTime"].ToString();

            if (dr["WorkOrder"].ToString().Trim() == "")
            {
                if (Black == "A")
                {
                    WriteLog(string.Format("06.處理異常資料 >> 資料表 : oNeed ,  BeginStation : {0} , EndStation : {1} , AssignFlag : C = 工單空白造成異常，故取消此項要求", ObjStation, EndStation));
                    UpdateoNeedAssignFlag("C", ObjStation, EndStation);
                    return;
                }
            }
            else
            {   //FHT^N01^批號^料號^製單^列印日期 :::: FHT^N01^238090671^DMT6CVJ1536D^P3804231^202309181158
                WorkOrder = dr["WorkOrder"].ToString();     //string[] sID = dr["WorkOrder"].ToString().Split('^');      //if (sID.Length >= 3) { PartNo = sID[3]; }
            }

            DataTable dt = mSql.QuerySqlByAutoOpen("select * from oRequire where ObjStation = @obj", SP("@obj", ObjStation)).Tables[0];
            if (dt.Rows.Count > 0)
            {
                UpdateoNeedAssignFlag("X", ObjStation, EndStation);
                WriteLog(string.Format("07.處理異常資料 >> 資料表 : oNeed , BeginStation : {0} , EndStation : {1} , AssignFlag : X = oRequire留有殘存資料，故將 oNeed 的 AssignFlag 改成 X ", ObjStation, EndStation));
                return;
            }

            if (CheckoPortBgnToEndIsNullAndUseFlagAsY(ObjStation, EndStation) == false)
            {
                UpdateoNeedAssignFlag("E", ObjStation, EndStation);
                WriteLog(string.Format("08.處理異常資料 >> 資料表 : oNeed , BeginStation : {0} , EndStation : {1} , AssignFlag : E = 異常結束，可能被註冊或該埠口被停用，故將 oNeed 的 AssignFlag 改成 E ", ObjStation, EndStation));
            }
            else
            {
                mSql.WriteSqlByAutoOpen(
                    "insert into oRequire(TaskDateTime, ObjStation, SerialNo, BeginStation, EndStation, TaskSource, RackId, WorkOrder) " +
                    "values(@td, @obj, 0, @obj, @end, 'MCS', @rack, @wo)",
                    SP("@td", TaskDateTime), SP("@obj", ObjStation), SP("@end", EndStation), SP("@rack", RackId), SP("@wo", WorkOrder));
                WriteLog(string.Format("09.產生oRequire >> oNeed -> oRequire , TaskDateTime : {0} , BeginStation : {1} , EndStation : {2} , WorkOrder : {3}", TaskDateTime, ObjStation, EndStation, WorkOrder));

                UpdateoNeedAssignFlag("Y", ObjStation, EndStation);
                WriteLog(string.Format("10.更新oNeed    >> oNeed -> Y , BeginStation : {0} , EndStation : {1} , AssignFlag : Y = 完成指派", ObjStation, EndStation));

                string lkString = ObjStation + ">" + EndStation;
                UpdateoPortBgnToEnd(ObjStation, EndStation, lkString);
                WriteLog(string.Format("11.更新oPort    >> 註冊路徑 , StationNo : {0} , BgnToEnd : {1} ", ObjStation, lkString));
                WriteLog(string.Format("12.更新oPort    >> 註冊路徑 , StationNo : {0} , BgnToEnd : {1} ", EndStation, lkString));
                WriteLog("---------------");
            }
        }
        #endregion



        #region  [1-5 .次程序 == CheckoPortBgnToEndIsNullAndUseFlagAsY == 找oPort表兩個站點資料，條件是埠口是可用的、沒被註冊]
        private bool CheckoPortBgnToEndIsNullAndUseFlagAsY(string StationNo1, string StationNo2)
        {
            bool rslt = false;
            DataTable dtoPort = mSql.QuerySqlByAutoOpen("select * from oPort where UseFlag ='Y' and StationNo in(@s1, @s2) and (BgnToEnd is null or RTRIM(BgnToEnd)='')", SP("@s1", StationNo1), SP("@s2", StationNo2)).Tables[0];
            if (dtoPort.Rows.Count == 2) { rslt = true; }
            return rslt;
        }
        #endregion

        #region [1-6 .次程序 == GetoPort_HaveRack_NoPair_CanWork_Sort_ByBlock == 找oPort表某一區域有架子資料但沒料，條件是埠口是可用的、沒有Rack、沒被註冊、依權重排序 ]
        private DataTable GetoPort_HaveRack_NoPair_CanWork_Sort_ByBlock(string Block)   //通常是找B、C、E
        {
            DataTable dt = mSql.QuerySqlByAutoOpen("select * from oPort where UseFlag ='Y' and HaveFlag ='1' and (BgnToEnd is null or RTRIM(BgnToEnd) ='') and" +
                                                   " Block in(" + Block + ") order by Priority desc").Tables[0];
            return dt;
        }
        #endregion

        #region [1-7 .次程序 == GetoPort_NoRack_NoPair_CanWork_Sort_ByBlock == 找oPort表某一區域沒架子資料，條件是埠口是可用的、沒有Rack、沒被註冊、依權重排序 ]
        public DataTable GetoPort_NoRack_NoPair_CanWork_Sort_ByBlock(string Block)   //通常是找A、B、C、D
        {
            //DataTable dt = mSql.QuerySqlByAutoOpen("select * from oPort where UseFlag ='Y' and (RackId is null or RTRIM(RackId) ='') and (BgnToEnd is null or RTRIM(BgnToEnd) ='') and" +
            var sql = @"SELECT * FROM oPort
                                WHERE UseFlag = 'Y'
                                AND HaveFlag = '0'
                                AND (BgnToEnd IS NULL OR RTRIM(BgnToEnd) = '')
                                AND Block IN (" + Block + @")
                                ORDER BY Priority DESC;
";

            //DataTable dt = mSql.QuerySqlByAutoOpen("select * from oPort where UseFlag ='Y' and HaveFlag ='0' and (BgnToEnd is null or RTRIM(BgnToEnd) ='') and" +
            //                                       " Block in(" + Block + ") order by Priority desc").Tables[0];

            var dt = mSql.QuerySqlByAutoOpen(sql).Tables[0];

            return dt;
        }
        #endregion

        #region [1-8 .次程序 == GetoPort_HaveWorkOrder_NoPair_CanWork_Sort_ByBlock == 找oPort表某一區域有工單資料，條件是埠口是可用的、有工單、沒被註冊、依權重排序 ]
        private DataTable GetoPort_HaveWorkOrder_NoPair_CanWork_Sort_ByBlock(string Block)    //通常是找A
        {
            //DataTable dt = mSql.QuerySqlByAutoOpen("select * from oPort where UseFlag ='Y' and (WorkOrder is not null or RTRIM(RackId) <>'') and (BgnToEnd is null or RTRIM(BgnToEnd) ='') and" +
            DataTable dt = mSql.QuerySqlByAutoOpen("select * from oPort where UseFlag ='Y' and HaveFlag ='3' and (BgnToEnd is null or RTRIM(BgnToEnd) ='') and" +
                                                   " Block in(" + Block + ") order by Priority desc").Tables[0];
            return dt;
        }
        #endregion

        #region [1-9 .次程序 == GetoPort_PartNoTheSame_NoPair_CanWork_Sort_ByBlock == 找oPort表某一區域指定料號，條件是埠口是可用的、有工單、沒被註冊、依權重排序 ]
        private DataTable GetoPort_PartNoTheSame_NoPair_CanWork_Sort_ByBlock(string Block, string PartNo)  //通常是找B(由C找B)
        {
            DataTable dt = new DataTable();
            if (PartNo == "X")
            {
                //dt = mSql.QuerySqlByAutoOpen("select * from oPort where UseFlag ='Y' and (WorkOrder is not null or RTRIM(WorkOrder) <>'') and (BgnToEnd is null or RTRIM(BgnToEnd) ='') and" +
                dt = mSql.QuerySqlByAutoOpen("select * from oPort where UseFlag ='Y' and  HaveFlag ='3' and (BgnToEnd is null or RTRIM(BgnToEnd) ='') and" +
                                                   " Block in(" + Block + ") order by Priority desc").Tables[0];
            }
            else
            {
                //dt = mSql.QuerySqlByAutoOpen("select * from oPort where UseFlag ='Y' and (WorkOrder is not null or RTRIM(WorkOrder) <>'') and (BgnToEnd is null or RTRIM(BgnToEnd) ='') and" +
                //                                   " Block in(" + Block + ") and PartNo ='" + PartNo + "' order by Priority desc").Tables[0];
                dt = mSql.QuerySqlByAutoOpen("select * from oPort where UseFlag ='Y' and HaveFlag ='3' and (BgnToEnd is null or RTRIM(BgnToEnd) ='') and" +
                                                   " Block in(" + Block + ") and WorkOrder like @pat order by Priority desc", SP("@pat", "%" + PartNo + "%")).Tables[0];
            }

            return dt;
        }
        #endregion

        #endregion

        #region  [Item 2.X == 尋找oRequire表中未指派的項目，產生oMission表 ]

        #region [2-0 .主程序 == GenerateoMissionByoRequire() == 尋找oRequire表中未指派的項目，產生oMission表 ]
        private void GenerateoMissionByoRequire()
        {
            DataTable dt = mSql.QuerySqlByAutoOpen("select * from oRequire where (AssignFlag is null or RTRIM(AssignFlag) = '') order by TaskDateTime").Tables[0];
            foreach (DataRow dr in dt.Rows)
            {
                if (dr["OkFlag"].ToString().Trim() == "")
                {
                    if (InsertoMission(dr) == true)
                    {
                        WriteLog(string.Format("20.新增oMission >> 成功"));
                        UpdateoRequireAssignFlag(dr["TaskDateTime"].ToString(), dr["ObjStation"].ToString(), int.Parse(dr["SerialNo"].ToString()), "Y");
                        WriteLog(string.Format("21.更新oRequire >>  TaskDateTime : {0} ,BeginStation : {1} , EndStation : {2} , AssignFlag : Y", dr["TaskDateTime"].ToString(), dr["BeginStation"].ToString(), dr["EndStation"].ToString()));
                    }
                    else
                    {
                        WriteLog(string.Format("20.新增oMission >> 失敗，刪除帳務請將 oRequire 的 OkFlag更新成 X 或 C 或 E"));
                        UpdateoRequireAssignFlag(dr["TaskDateTime"].ToString(), dr["ObjStation"].ToString(), int.Parse(dr["SerialNo"].ToString()), "C");
                        WriteLog(string.Format("21.更新oRequire >>  TaskDateTime : {0} ,BeginStation : {1} , EndStation : {2} , AssignFlag : C", dr["TaskDateTime"].ToString(), dr["BeginStation"].ToString(), dr["EndStation"].ToString()));
                    }
                    return;
                }
            }
        }
        #endregion

        #region [2-1 .次程序 == InsertoMission() == 新增 oMission 表資料  ]
        private bool InsertoMission(DataRow dr)   //
        {
            bool rslt = false;
            //FHT^N01^批號^料號^製單^列印日期 :::: FHT^N01^238090671^DMT6CVJ1536D^P3804231^202309181158  //string[] sID = dr["WorkOrder"].ToString().Split('^');if (sID.Length >= 3) { PartNo = sID[3]; }
            DataTable dt = mSql.QuerySqlByAutoOpen("select * from oMission where BeginStation = @bgn and EndStation = @end", SP("@bgn", dr["BeginStation"].ToString()), SP("@end", dr["EndStation"].ToString())).Tables[0];
            if (dt.Rows.Count == 0)
            {
                mSql.WriteSqlByAutoOpen(
                    "Insert into oMission(TaskDateTime, SerialNo, BeginStation, EndStation, TaskSource, ShuttleId, RackId, WorkOrder) " +
                    "values(@td, 0, @bgn, @end, 'MCS', 0, @rack, @wo)",
                    SP("@td", dr["TaskDateTime"].ToString()), SP("@bgn", dr["BeginStation"].ToString()), SP("@end", dr["EndStation"].ToString()), SP("@rack", dr["RackId"].ToString()), SP("@wo", dr["WorkOrder"].ToString()));
                rslt = true;
            }
            return rslt;
        }
        #endregion

        #region [2-2 .次程序 == UpdateoRequireAssignFlag() == 更新 oMission 表資料  ]
        private void UpdateoRequireAssignFlag(string TaskDateTime, string ObjStation, int SerialNo, string AssignFlag)
        {
            mSql.WriteSqlByAutoOpen("update oRequire set AssignFlag = @af where TaskDateTime = @td and ObjStation = @obj and SerialNo = @sn", SP("@af", AssignFlag), SP("@td", TaskDateTime), SP("@obj", ObjStation), SP("@sn", SerialNo));
            WriteLog(string.Format("22.更新oRequire >> 已指派 , TaskDateTime : {0} ,ObjStation : {1} , SerialNo : {2} :: AssignFlag ={3}", TaskDateTime, ObjStation, SerialNo.ToString(), AssignFlag));
        }
        #endregion

        #region [2-3 .次程序 == InsertoNee() == 新增oNeed ]
        private void InsertoNeed(string ObjStation, string RackId, string WorkOrder, string EndStation)   //
        {
            mSql.WriteSqlByAutoOpen(
                "Insert into oNeed(ObjStation, RackId, WorkOrder, EndStation, TaskSource, TaskDateTime) " +
                "values(@obj, @rack, @wo, @end, 'MCS', @td)",
                SP("@obj", ObjStation), SP("@rack", RackId), SP("@wo", WorkOrder), SP("@end", EndStation), SP("@td", GetTaskDateTimeIncludeRandom(true)));
        }
        #endregion

        #region [2-4 .次程序 == UpdateoNeedAssignFlag() == 更新 oNeed 的 AssignFlag ]
        private void UpdateoNeedAssignFlag(string AssignFlag, string ObjStation, string EndStation)
        {
            mSql.WriteSqlByAutoOpen("update oNeed set AssignFlag = @af where ObjStation = @obj and EndStation = @end", SP("@af", AssignFlag), SP("@obj", ObjStation), SP("@end", EndStation));
        }
        #endregion

        #region [2-5 .次程序 == UpdateoPortBgnToEnd() == 更新 oPort 的 BgnToEnd ]
        private void UpdateoPortBgnToEnd(string StationNo1, string StationNo2, string BgnToEnd = null)
        {
            // 註：原拼接寫法在 BgnToEnd 為 null 時會寫入空字串（非 NULL），此處以 (BgnToEnd ?? "") 維持完全相同行為。
            mSql.WriteSqlByAutoOpen("update oPort set BgnToEnd = @bte where StationNo in(@s1, @s2)", SP("@bte", BgnToEnd ?? ""), SP("@s1", StationNo1), SP("@s2", StationNo2));
        }
        #endregion

        #region [2-6 .次程序 == DeleteoNeedByAssignFlag() == 刪除 oNeed 指定註記的資料 ]
        private void DeleteoNeedByAssignFlag(string ObjStation, string EndStation, string AssignFlag)
        {
            mSql.WriteSqlByAutoOpen("Delete oNeed where ObjStation = @obj and EndStation = @end and AssignFlag = @af", SP("@obj", ObjStation), SP("@end", EndStation), SP("@af", AssignFlag));
        }
        #endregion

        #region [2-7 .次程序 == DeleteoRequireByOkFlag() == 刪除 oRequire 完成註記的資料 ]
        private void DeleteoRequireByOkFlag(string TaskDateTime, string ObjStation, int SerialNo, string EndStation, string OkFlag)
        {
            mSql.WriteSqlByAutoOpen("Delete oRequire where TaskDateTime = @td and ObjStation = @obj and SerialNo = @sn and EndStation = @end and OkFlag = @ok", SP("@td", TaskDateTime), SP("@obj", ObjStation), SP("@sn", SerialNo), SP("@end", EndStation), SP("@ok", OkFlag));
        }
        #endregion

        #region [2-8 .次程序 == DeleteoRequireByAssignFlag() == 刪除 oRequier 指定註記的資料 ]
        private void DeleteoRequireByAssignFlag(string ObjStation, string EndStation, string AssignFlag)
        {
            mSql.WriteSqlByAutoOpen("Delete oRequire where ObjStation = @obj and EndStation = @end and AssignFlag = @af", SP("@obj", ObjStation), SP("@end", EndStation), SP("@af", AssignFlag));
        }
        #endregion

        #endregion

        #region  [Item 3.X == 任務完成回收 Y=完成，X=異常結束，C=取消 ]

        #region [3-0 .主程序 == RecyclingoRequireByOkFlag() == 處理oRequire表中的OkFlag欄位，Y=完成，X=異常結束，C=取消 ]
        private void RecyclingoRequireByOkFlag()
        {
            //string lkString = NULL;
            DataTable dt = mSql.QuerySqlByAutoOpen("select * from oRequire where OkFlag in('Y','X','C') order by TaskDateTime").Tables[0];
            foreach (DataRow dr in dt.Rows)
            {
                switch (dr["OkFlag"].ToString())
                {
                    case "Y":
                    case "X":
                    case "C":
                        DeleteoNeedByAssignFlag(dr["ObjStation"].ToString(), dr["EndStation"].ToString(), "Y");
                        WriteLog(string.Format("30.回收oNeed    >>  ObjStation : {0} ,EndStation : {1} , AssignFlag : {2}", dr["ObjStation"].ToString(), dr["EndStation"].ToString(), dr["AssignFlag"].ToString()));


                        UpdateoPortBgnToEnd(dr["ObjStation"].ToString(), dr["EndStation"].ToString());
                        WriteLog(string.Format("31.取消註冊     >>  oPort路徑 , ObjStation : {0} , EndStation : {1} , BgnToEnd : {2} ", dr["ObjStation"].ToString(), dr["EndStation"].ToString(), dr["ObjStation"].ToString() + ">" + dr["EndStation"].ToString()));


                        DeleteoRequireByOkFlag(dr["TaskDateTime"].ToString(), dr["ObjStation"].ToString(), int.Parse(dr["SerialNo"].ToString()), dr["EndStation"].ToString(), dr["OkFlag"].ToString());
                        WriteLog(string.Format("32.回收oRequire >>  TaskDateTime : {0} ,ObjStation : {1} , SerialNo : {2} , AssignFlag : {3}", dr["TaskDateTime"].ToString(), dr["ObjStation"].ToString(), dr["SerialNo"].ToString(), dr["OkFlag"].ToString()));

                        if (mPanelDoB2C == true)
                        {
                            //處理起點為其他筆oNeed的終點，且該筆資料AssignFlag為 P，將該AssignFlag改成NULL
                            mSql.WriteSqlByAutoOpen("update oNeed set AssignFlag = NULL where AssignFlag ='P' and EndStation = @end", SP("@end", dr["ObjStation"].ToString()));
                        }
                        break;
                    default:
                        break;
                }
            }
        }
        #endregion

        #endregion

        #region [4-0 .主程序 == RecyclingoRequireByAssignFlag() == 處理oRequire表中的 AssignFlag 欄位為 E、X、C]
        private void RecyclingoRequireByAssignFlag()
        {
            DataTable dt = mSql.QuerySqlByAutoOpen("select * from oRequire where AssignFlag in('E','X','C') order by TaskDateTime").Tables[0];
            foreach (DataRow dr in dt.Rows)
            {
                switch (dr["AssignFlag"].ToString())
                {
                    case "E":
                    case "X":
                    case "C":
                        DeleteoNeedByAssignFlag(dr["ObjStation"].ToString(), dr["EndStation"].ToString(), "Y");
                        WriteLog(string.Format("38.回收oNeed    >>  ObjStation : {0} ,EndStation : {1} , AssignFlag : {2}", dr["ObjStation"].ToString(), dr["EndStation"].ToString(), dr["AssignFlag"].ToString()));


                        UpdateoPortBgnToEnd(dr["ObjStation"].ToString(), dr["EndStation"].ToString());
                        WriteLog(string.Format("39.取消註冊     >>  oPort路徑 , ObjStation : {0} , EndStation : {1} , BgnToEnd : {2} ", dr["ObjStation"].ToString(), dr["EndStation"].ToString(), dr["ObjStation"].ToString() + ">" + dr["EndStation"].ToString()));


                        DeleteoRequireByAssignFlag(dr["ObjStation"].ToString(), dr["EndStation"].ToString(), dr["AssignFlag"].ToString());
                        WriteLog(string.Format("40.處理暫存資料 >> 資料表 : oRequire , ObjStation : {0} ,EndStation : {1} , AssignFlag : {2} ", dr["ObjStation"].ToString(), dr["EndStation"].ToString(), dr["AssignFlag"].ToString()));
                        break;

                    default:
                        break;
                }
            }
        }
        #endregion

        #region [5-0 .主程序 == RecyclingoNeedByAssignFlag() == 處理oNeed表中的 AssignFlag 欄位為 E、X、C]
        private void RecyclingoNeedByAssignFlag()
        {
            DataTable dt = mSql.QuerySqlByAutoOpen("select * from oNeed where AssignFlag in('E','X','C') order by TaskDateTime").Tables[0];
            foreach (DataRow dr in dt.Rows)
            {
                switch (dr["AssignFlag"].ToString())
                {
                    case "E":
                    case "X":
                    case "C":
                        //DeleteoNeedByAssignFlag(dr["ObjStation"].ToString(), dr["EndStation"].ToString(), "E");
                        DeleteoNeedByAssignFlag(dr["ObjStation"].ToString(), dr["EndStation"].ToString(), dr["AssignFlag"].ToString());
                        WriteLog(string.Format("50.處理暫存資料 >> 資料表 : oNeed , ObjStation : {0} ,EndStation : {1} , AssignFlag : {2} ", dr["ObjStation"].ToString(), dr["EndStation"].ToString(), dr["AssignFlag"].ToString()));
                        break;

                    default:
                        break;
                }
            }
        }
        #endregion

        #region [6-0 .主程序 == GenerateoMissionDataByIdleShuttleFromoShuttle == 讀取oShuttle表，針對狀態為I:Idle的車子，且該車也未在oMission中出現]
        private void GenerateoMissionDataByIdleShuttleFromoShuttle()
        {

            DataTable dt = mSql.QuerySqlByAutoOpen("select * from oShuttle where Enabled ='Y' and Status ='I' and (not ShuttleId in (select ShuttleId from oMission where ShuttleId is not null))").Tables[0];
            foreach (DataRow dr in dt.Rows)
            {
                GetBeginStationFromoPair(int.Parse(dr["ShuttleId"].ToString()), dr["EndStation"].ToString());

            }
        }
        #endregion

        #region [6-1 .次程序 == GetBeginStationFromoPair == 尋找配對表中起點在同區域的優先，再找終點共同配對成oMission]
        private void GetBeginStationFromoPair(int ShuttleId, string EndStation)
        {
            DataTable oPair = mSql.QuerySqlByAutoOpen("select * from oPair where PairFlag <> 'Y'").Tables[0];
            //ToDo :: 從演算法中取得最靠近的起點，然後看oPair中是否有成對的資料
            string ObjStation = "";

            DataTable dt = mSql.QuerySqlByAutoOpen("select * from oShuttle where Enabled ='Y' and Status ='I' and (not ShuttleId in (select ShuttleId from oMission where ShuttleId is not null))").Tables[0];
            foreach (DataRow dr in dt.Rows)
            {


            }
        }
        #endregion

        #region [9.1 .公用序 == GetTaskDateTimeIncludeRandom == 抓取任務時間含6位數的亂數]
        public string GetTaskDateTimeIncludeRandom(bool bolDateTime)
        {
            Random rdn = new Random();
            Int32 int32Rdn = rdn.Next(100, 980) + 11; //刻意加避免撞到
            if (bolDateTime)
            {
                return DateTime.Now.ToString("yyyyMMddHHmmssfff") + int32Rdn.ToString();
            }
            else
            {
                return DateTime.Now.ToString("yyyyMMddHHmmss");
            }
        }
        #endregion

        #region [9.2 .公用序 == WriteLog == 寫LOG檔]
        private void WriteLog(string _LogMessage, LogType _Type = LogType.Normal)
        {
            LogManager.Dispatch.LogMessage(string.Format("{0}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " || " + _LogMessage), _Type);
        }
        #endregion
    }
}
