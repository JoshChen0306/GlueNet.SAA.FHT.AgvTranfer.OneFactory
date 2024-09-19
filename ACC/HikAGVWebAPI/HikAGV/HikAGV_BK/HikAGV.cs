using AGVDispatch;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HikAGVDll
{
    public class HikAGV
    {
        #region Config
        private readonly Configuration config;//抓取 Config 檔案資料
        private DBSettings DBSettings = new DBSettings();
        private AGVUrlSettings AGVUrlSettings = new AGVUrlSettings();
        public AGVSettings AGVSettings = new AGVSettings();
        private readonly string ConfigFileName = string.Format("{0}\\Config\\HikAGV.config", AppDomain.CurrentDomain.BaseDirectory);
        #endregion Config

        private static readonly HttpClient client = new HttpClient();
        //NLog
        private Logger NLog;
        private Thread AGVThread;
        private HikAGVDispatch AGVDispatch;
        private readonly SqlDataMgmt SQLDB;

        public HikAGV()
        {
            config = LoadExternalConfig(ConfigFileName);
            ReadDBConfig();
            NLog = LogManager.GetLogger("HikAGVLog");

            if (AGVSettings.ThreadEnable)
            {
                AGVDispatch = new HikAGVDispatch();
                AGVThread = new Thread(Execute);
                AGVThread.IsBackground = true;
                AGVThread.Start();
            }
        }

        #region 讀取設定檔全部資料
        private Configuration LoadExternalConfig(string configName)
        {
            try
            {
                ExeConfigurationFileMap configMap = new ExeConfigurationFileMap();
                configMap.ExeConfigFilename = configName;
                return ConfigurationManager.OpenMappedExeConfiguration(configMap, ConfigurationUserLevel.None);
            }
            catch
            {
                return null;
            }
        }
        #endregion 讀取設定檔全部資料

        #region 依照設定檔讀取 Section 資料
        private void ReadDBConfig()
        {
            try
            {
                SectionDB SectionDB = config.GetSection("SectionDB") as SectionDB;
                DBSettings = SectionDB?.DBSettings;

                //載入這套系統要搭配的 Config 派車資訊
                SectionAGV SectionAGV = config.GetSection("SectionAGV") as SectionAGV;
                AGVUrlSettings = SectionAGV?.AGVUrlSettings;
                AGVSettings = SectionAGV?.AGVSettings;
            }
            catch
            {
            }
        }
        #endregion 依照設定檔讀取 Section 資料

        #region
        private void Execute()
        {
            while (true)
            {
                try
                {
                    AGVSchedulingTask();
                }
                catch (Exception ex)
                {
                }

                Thread.Sleep(1000);
            }
        }
        #endregion

        private void AGVSchedulingTask()
        {
            try
            {
                List<ShuttleModel> AllShuttle = AGVDispatch.GetShuttle().Where(x => x.Enable.Equals("Y")).ToList();
                List<MissionTaskModel> AllMissionTask = AGVDispatch.GetMissionTask();

                foreach (ShuttleModel shuttle in AllShuttle)
                {
                    string sShuttle = shuttle.ShuttleID;
                    List<MissionTaskModel> ShuttleMission = AllMissionTask.Where(x => x.ShuttleID == sShuttle).ToList();
                    List<MissionTaskModel> NewShuttleMission = AllMissionTask.Where(x => string.IsNullOrEmpty(x.ShuttleID)).ToList();

                    if (ShuttleMission.Count == 0 && NewShuttleMission.Count > 0)//判斷此車無任務且有新增任務未指派車子
                    {
                        MissionTaskModel MissionTask = NewShuttleMission.FirstOrDefault();
                        AGVStatus AGVStatus = new AGVStatus()
                        {
                            reqCode = DateTime.Now.ToString("yyyyMMddhhmmssfffff"),
                            mapCode = AGVSettings.AGVMapCode,
                        };

                        AGVStatusAck AGVStatusAck = this.AGVStatus(AGVStatus);
                        AGVStatusData AGVStatusData = AGVStatusAck.data.Where(x => x.robotCode == sShuttle).FirstOrDefault();

                        if (AGVStatusData != null)
                        {
                            HikStatusEnum eStatus = (HikStatusEnum)Enum.Parse(typeof(HikStatusEnum), AGVStatusData.status, true);
                            switch (eStatus)
                            {
                                case HikStatusEnum.IdleTask:
                                    SchedulingTaskAck SchedulingTaskAck = SetSchedulingTask(MissionTask);
                                    if (SchedulingTaskAck.code.Equals("0"))
                                    {
                                        MissionTask.OkFlag = "R";
                                        AGVDispatch.UpdateMissionTask(MissionTask);
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        private SchedulingTaskAck SetSchedulingTask(MissionTaskModel MissionTask)
        {
            SchedulingTaskAck TaskAck = null;

            try
            {
                List<CodePath> positionCodes = new List<CodePath>()
                {
                    new CodePath() { positionCode = MissionTask.BeginStation, type = "00" },
                    new CodePath() { positionCode = MissionTask.EndStation, type = "00" }
                };

                SchedulingTask schedulingTask = new SchedulingTask()
                {
                    reqCode = DateTime.Now.ToString("yyyyMMddHHmmssffff"),
                    taskTyp = AGVSettings.AGVTaskType,
                    positionCodePath = positionCodes,
                };

                TaskAck = SchedulingTask(schedulingTask);
            }
            catch (Exception ex)
            {
            }

            return TaskAck;
        }

        protected internal T HikAGVPost<T>(HikFunctionEnum HikFunction, object model)
        {
            string sURL = string.Empty;

            try
            {
                if (HikFunction == HikFunctionEnum.agvCallback || HikFunction == HikFunctionEnum.warnCallback || HikFunction == HikFunctionEnum.bindNotify || HikFunction == HikFunctionEnum.applyBin)
                    sURL = string.Format(AGVUrlSettings.CallBackURL, HikFunction);
                else if (HikFunction == HikFunctionEnum.queryAgvStatus)
                    sURL = string.Format(AGVUrlSettings.AGVStatusURL, HikFunction);
                else
                    sURL = string.Format(AGVUrlSettings.RestURL, HikFunction);
                NLog.Info($"[Web URL] : {sURL}");
                string sJsonString = JsonConvert.SerializeObject(model);
                NLog.Info($"[Send Data] : {sJsonString}");
                string sResponse = PostData(sURL, sJsonString);
                NLog.Info($"[Receive Data] : {sResponse}");
                T RCSModelReturn = JsonConvert.DeserializeObject<T>(sResponse);
                //RCSModelReturn.SendJson = sJsonString;
                //RCSModelReturn.ReturnJson = sResponse;
                return RCSModelReturn;
            }
            catch (Exception ex)
            {
                return default(T);
            }
        }

        protected internal string PostData(string sURL, string sData)
        {
            HttpResponseMessage response = null;

            try
            {
                StringContent content = new StringContent(sData, Encoding.UTF8, "application/json");
                Task<HttpResponseMessage> task = Task.Run(() => client.PostAsync(sURL, content));
                task.Wait();
                response = task.Result;
                Task<string> streamReader = Task.Run(() => response.Content.ReadAsStringAsync());
                //Task<string> streamReader = response.Content.ReadAsStringAsync();
                return streamReader.Result;
            }
            catch (Exception e)
            {
                return string.Empty;
            }
            finally
            {
                response?.Dispose();
            }
        }

        #region AGV Functions
        /// <summary>
        /// 生成任務單
        /// </summary>
        /// <param name="SchedulingTask"></param>
        /// <returns></returns>
        public SchedulingTaskAck SchedulingTask(SchedulingTask SchedulingTask)
        {
            return HikAGVPost<SchedulingTaskAck>(HikFunctionEnum.genAgvSchedulingTask, SchedulingTask);
        }

        /// <summary>
        /// 繼續執行任務
        /// </summary>
        /// <param name="ContinueTask"></param>
        /// <returns></returns>
        public ContinueTaskAck ContinueTask(ContinueTask ContinueTask)
        {
            return HikAGVPost<ContinueTaskAck>(HikFunctionEnum.continueTask, ContinueTask);
        }

        /// <summary>
        /// 取消任務
        /// </summary>
        /// <param name="CancelTask"></param>
        /// <returns></returns>
        public CancelTaskAck CancelTask(CancelTask CancelTask)
        {
            return HikAGVPost<CancelTaskAck>(HikFunctionEnum.cancelTask, CancelTask);
        }

        /// <summary>
        /// 任務優先級設置
        /// </summary>
        /// <param name="TaskPriority"></param>
        /// <returns></returns>
        public TaskPriorityAck TaskPriority(TaskPriority TaskPriority)
        {
            return HikAGVPost<TaskPriorityAck>(HikFunctionEnum.setTaskPriority, TaskPriority);
        }

        /// <summary>
        /// 貨架與位置綁定、解綁
        /// </summary>
        /// <param name="PodAndBerth"></param>
        /// <returns></returns>
        public PodAndBerthAck PodAndBerth(PodAndBerth PodAndBerth)
        {
            return HikAGVPost<PodAndBerthAck>(HikFunctionEnum.bindPodAndBerth, PodAndBerth);
        }

        /// <summary>
        /// 貨架與物料綁定、解綁
        /// </summary>
        /// <param name="PodAndMat"></param>
        /// <returns></returns>
        public PodAndMatAck PodAndMat(PodAndMat PodAndMat)
        {
            return HikAGVPost<PodAndMatAck>(HikFunctionEnum.bindPodAndMat, PodAndMat);
        }

        /// <summary>
        /// 位置禁用與啟用
        /// </summary>
        /// <param name="LockPosition"></param>
        /// <returns></returns>
        public LockPositionAck LockPosition(LockPosition LockPosition)
        {
            return HikAGVPost<LockPositionAck>(HikFunctionEnum.lockPosition, LockPosition);
        }

        /// <summary>
        /// 全量同步地碼码數據
        /// </summary>
        /// <param name="SyncMapDatas"></param>
        /// <returns></returns>
        public SyncMapDatasAck SyncMapDatas(SyncMapDatas SyncMapDatas)
        {
            return HikAGVPost<SyncMapDatasAck>(HikFunctionEnum.syncMapDatas, SyncMapDatas);
        }

        /// <summary>
        /// 查詢貨架儲位與物料批次關係
        /// </summary>
        /// <param name="PodBerthAndMat"></param>
        /// <returns></returns>
        public QryPodBerthAndMatAck QryPodBerthAndMat(QryPodBerthAndMat PodBerthAndMat)
        {
            return HikAGVPost<QryPodBerthAndMatAck>(HikFunctionEnum.queryPodBerthAndMat, PodBerthAndMat);
        }

        /// <summary>
        /// 倉位禁用與啟用
        /// </summary>
        /// <param name="BlockStageBin"></param>
        /// <returns></returns>
        public BlockStageBinAck BlockStageBin(BlockStageBin BlockStageBin)
        {
            return HikAGVPost<BlockStageBinAck>(HikFunctionEnum.blockStgBin, BlockStageBin);
        }

        /// <summary>
        /// 容器與倉位綁定、解綁
        /// </summary>
        /// <param name="CtnrAndBin"></param>
        /// <returns></returns>
        public CtnrAndBinAck ContainerAndBin(CtnrAndBin CtnrAndBin)
        {
            return HikAGVPost<CtnrAndBinAck>(HikFunctionEnum.bindCtnrAndBin, CtnrAndBin);
        }

        /// <summary>
        /// 查詢任務狀態
        /// </summary>
        /// <param name="TaskStatus"></param>
        /// <returns></returns>
        public TaskStatusAck TaskStatus(TaskStatus TaskStatus)
        {
            return HikAGVPost<TaskStatusAck>(HikFunctionEnum.queryTaskStatus, TaskStatus);
        }

        /// <summary>
        /// 查詢 AGV 狀態
        /// </summary>
        /// <param name="AGVStatus"></param>
        /// <returns></returns>
        public AGVStatusAck AGVStatus(AGVStatus AGVStatus)
        {
            return HikAGVPost<AGVStatusAck>(HikFunctionEnum.queryAgvStatus, AGVStatus);
        }

        /// <summary>
        /// 停止 AGV
        /// </summary>
        /// <param name="StopRobot"></param>
        /// <returns></returns>
        public StopRobotAck StopRobot(StopRobot StopRobot)
        {
            return HikAGVPost<StopRobotAck>(HikFunctionEnum.stopRobot, StopRobot);
        }

        /// <summary>
        /// 恢復 AGV
        /// </summary>
        /// <param name="ResumeRobot"></param>
        /// <returns></returns>
        public ResumeRobotAck ResumeRobot(ResumeRobot ResumeRobot)
        {
            return HikAGVPost<ResumeRobotAck>(HikFunctionEnum.resumeRobot, ResumeRobot);
        }

        /// <summary>
        /// 區域清空/釋放
        /// </summary>
        /// <param name="BlockArea"></param>
        /// <returns></returns>
        public BlockAreaAck BlockArea(BlockArea BlockArea)
        {
            return HikAGVPost<BlockAreaAck>(HikFunctionEnum.blockArea, BlockArea);
        }

        /// <summary>
        /// 預調度對外接口
        /// </summary>
        /// <param name="PreSchedulingTask"></param>
        /// <returns></returns>
        public PreScheduleTaskAck PreSchedulingTask(PreScheduleTask PreSchedulingTask)
        {
            return HikAGVPost<PreScheduleTaskAck>(HikFunctionEnum.genPreScheduleTask, PreSchedulingTask);
        }

        /// <summary>
        /// 清空巷道
        /// </summary>
        /// <param name="ClearRoadWay"></param>
        /// <returns></returns>
        public ClearRoadWayAck ClearRoadWay(ClearRoadWay ClearRoadWay)
        {
            return HikAGVPost<ClearRoadWayAck>(HikFunctionEnum.clearRoadWay, ClearRoadWay);
        }

        /// <summary>
        /// 料箱出庫 TPS (CTU+分撥牆)
        /// </summary>
        /// <param name="GetOutPod"></param>
        /// <returns></returns>
        public GetOutPodAck GetOutPod(GetOutPod GetOutPod)
        {
            return HikAGVPost<GetOutPodAck>(HikFunctionEnum.getOutPod, GetOutPod);
        }

        /// <summary>
        /// 料箱回庫 TPS (CTU+分撥牆)
        /// </summary>
        /// <param name="ReturnPod"></param>
        /// <returns></returns>
        public ReturnPodAck ReturnPod(ReturnPod ReturnPod)
        {
            return HikAGVPost<ReturnPodAck>(HikFunctionEnum.returnPod, ReturnPod);
        }

        /// <summary>
        /// 料箱順序出庫 (CTU)
        /// </summary>
        /// <param name="GroupTaskBatch"></param>
        /// <returns></returns>
        public GroupTaskBatchAck GroupTaskBatch(GroupTaskBatch GroupTaskBatch)
        {
            return HikAGVPost<GroupTaskBatchAck>(HikFunctionEnum.genCtuGroupTaskBatch, GroupTaskBatch);
        }

        /// <summary>
        /// 料箱取放回調 (CTU)
        /// </summary>
        /// <param name="BoxApplyPass"></param>
        /// <returns></returns>
        public BoxApplyPassAck BoxApplyPass(BoxApplyPass BoxApplyPass)
        {
            return HikAGVPost<BoxApplyPassAck>(HikFunctionEnum.boxApplyPass, BoxApplyPass);
        }

        /// <summary>
        /// 指定料箱終點 (CTU)
        /// </summary>
        /// <param name="ContainerDestination"></param>
        /// <returns></returns>
        public ContainerDestinationAck ContainerDestination(ContainerDestination ContainerDestination)
        {
            return HikAGVPost<ContainerDestinationAck>(HikFunctionEnum.bindCtnrDestination, ContainerDestination);
        }

        /// <summary>
        /// 任務執行通知
        /// </summary>
        /// <param name="CallBack"></param>
        /// <returns></returns>
        public CallBackAck AGVCallBack(CallBack CallBack)
        {
            return HikAGVPost<CallBackAck>(HikFunctionEnum.agvCallback, CallBack);
        }

        /// <summary>
        /// 告警推送通知
        /// </summary>
        /// <param name="WarnCallBack"></param>
        /// <returns></returns>
        public WarnCallBackAck WarnCallBack(WarnCallBack WarnCallBack)
        {
            return HikAGVPost<WarnCallBackAck>(HikFunctionEnum.warnCallback, WarnCallBack);
        }

        /// <summary>
        /// 綁定解綁通知
        /// </summary>
        /// <param name="BindNotify"></param>
        /// <returns></returns>
        public BindNotifyAck BindNotify(BindNotify BindNotify)
        {
            return HikAGVPost<BindNotifyAck>(HikFunctionEnum.bindNotify, BindNotify);
        }

        /// <summary>
        /// 申請回庫倉位 (CTU)
        /// </summary>
        /// <param name="ApplyBin"></param>
        /// <returns></returns>
        public ApplyBinAck ApplyBin(ApplyBin ApplyBin)
        {
            return HikAGVPost<ApplyBinAck>(HikFunctionEnum.applyBin, ApplyBin);
        }
        #endregion AGV Functions
    }
}