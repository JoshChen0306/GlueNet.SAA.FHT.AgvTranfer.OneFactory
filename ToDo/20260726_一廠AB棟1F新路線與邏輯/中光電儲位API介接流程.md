# 中光電儲位 API 介接流程

> 建立日期：2026-10-06｜狀態：**已依客戶流程圖（2026-10-06）修訂；binCode、itemCode、連線資訊仍待確認**
> 來源規格：`D:\格路科技\格路科技 - 開發團隊 - project-客製化專案\2025\0.評估中\20251013_迅得_高技_自動搬運專案系統\一廠AB棟搬運\儲位api.pdf`（客戶提供）
> 客戶流程圖：2026-10-06 客戶以訊息提供（路線圖＋「取得物料資訊-建立任務」兩張＋「物料資訊清除」一張）
> 相關任務：T6 自動派送、T7／T9 人工派車、T8 物料轉寫、T13 sensor、T25 介接實作、T1 區域代碼（`區域代碼對照表.md`）

## 一、角色

| 規格書寫法 | 實際是誰 | 說明 |
|-----------|---------|------|
| 呼叫方「AGV」 | **我方**（格路 SCP／ACC 派車系統） | 我方實作 HTTP client |
| 接收方「ACC（未來的 WCS）」 | **中光電的系統** | ⚠️ 與我方 ACC（HikAGVWebAPI）同名，文件與溝通時須註明是哪一方 |

## 二、API 摘要

| # | API | Method | 用途 | Request | Response `code` |
|---|-----|--------|------|---------|-----------------|
| 2-1 | `/api/v1/wcs/locations/{binCode}/status` | GET | 查詢儲位的物料資訊 | — | 0 成功（含 `itemCode`）、2 ERROR |
| 2-2 | `/api/v1/wcs/locations/{binCode}/Mapping` | POST | 綁定儲位的物料資訊 | `{ "itemCode": "物料資訊" }` | 0 成功、1 已佔用、2 ERROR |
| 2-3 | `/api/v1/wcs/locations/{binCode}/release` | POST | 清除儲位的物料資訊 | — | 0 成功、1 已釋放、2 ERROR |

- 所有回應皆含 `message`（字串）。
- 2-3 規格書「說明」欄寫「綁定儲位的物料資訊」，應為複製筆誤（待確認）。

## 三、路線（客戶流程圖，與 T1 對照表一致）

| 客戶圖 | 路線 | 我方站號 | 觸發方式（客戶流程圖） |
|--------|------|---------|----------------------|
| 黃線：中光電 D → 迅得 4/3/2 | R1 | D1 → E1～E3 | **sensor 自動**（Sensor ON） |
| 紅線：中光電 A → 迅得 7/6/5 | R4 | O1 → F1～F3 | **sensor 自動**（Sensor ON） |
| 綠線：迅得 2/3/4 → 中光電 C | R2 | E1～E3 → Z1 | **人員派發** |
| 黑線：迅得 1 → 中光電 B | R3 | B1 → P1 | **人員派發** |

## 四、我方任務生命週期（既有，ACC `API/CallBackAPI.cs` `AGVCallback`）

| RCS 回呼 | 意義 | 我方現行動作 |
|----------|------|-------------|
| `start` | AGV 開始執行任務 | oRequire／oMission 狀態更新為執行中 |
| `outbin` | AGV 頂起貨架、離開起點儲位 | `Update_oPortEmpty`：清空**起點** oPort |
| `end` | 放下貨架、任務完成 | `Update_oPort`：把 oMission 的 WorkOrder／RackId 寫入**終點** oPort（有工單 HaveFlag=3，否則 1） |
| `cancel` | 任務取消 | oMission 歸檔，OkFlag=C |

## 五、呼叫時機

| 情境 | 觸發 | 呼叫 | 對應我方時間點 | 客戶流程圖 |
|------|------|------|--------------|-----------|
| 取貨（R1、R4） | 中光電貨架 Sensor ON | ① 2-1 `status` → 建任務 → ② 2-3 `release` | ① 建任務前；② `outbin` | ✅ 一致 |
| 放貨（R2、R3） | 人員派發任務 | ③ 2-2 `Mapping` | ③ `end`（卸貨後） | ✅ 一致 |
| 物料資訊清除 | 中光電貨架 Sensor OFF | ④ 2-3 `release` | ACC StockSensorLink 偵測清空時 | 🆕 客戶流程圖新增 |

### 5-1 從中光電取貨（R1、R4）— 客戶流程「取得物料資訊-建立任務（中光電貨架 > SAA 貨架）」

```
 SCP 自動派送 (AutoDispatch)            ACC (RCS 回呼)                 中光電 WCS
 ───────────────────────────            ──────────────                 ──────────
 Sensor ON：中光電 D 有貨
 且迅得 2/3/4 有空位
        │
        │ ① 建任務前先查物料
        ├──────────── GET /locations/{D的binCode}/status ─────────────►
        │ ◄─────────────────────── code=0, itemCode ───────────────────
        │
   code≠0 或 itemCode 空？
     ├─ 是 → 不派車，記 Warning（下一輪再試）
     └─ 否 → 建 oNeed，WorkOrder = itemCode
        │
        ▼
   cPair → oRequire → oMission → RCS 執行
                                        │
                                   [outbin] AGV 取得貨物、離開 D
                                        │ 清空我方 D 的 oPort（既有）
                                        │ ② 通知中光電 D 已空
                                        ├── POST /locations/{D}/release ──►
                                        │ ◄──── 0 成功 / 1 已釋放（視為成功）
                                        │
                                   [end] 放到迅得 E1
                                        │ E1 的 oPort 寫入 WorkOrder=itemCode（既有）
                                        ▼
                                      結束
```

### 5-2 放貨到中光電（R2、R3）— 客戶流程「取得物料資訊-建立任務（SAA 貨架 > 中光電貨架）」

```
 SCP 派送頁（人工）                      ACC (RCS 回呼)                 中光電 WCS
 ──────────────────                      ──────────────                 ──────────
 人員派發任務（起點 E1～E3 或 B1）
        │
   SCP 取得資料庫資訊（起點 oPort 的物料）
        │
   建 oNeed（WorkOrder = 我方物料，終點中光電 C / B 空位）
        │
   cPair → oRequire → oMission → RCS 執行
                                        │
                                   [outbin] AGV 取得貨物，清空我方起點（既有）
                                        │
                                   [end] AGV 搬運卸貨到中光電 C
                                        │ 我方 C 的 oPort 寫入物料（既有）
                                        │ ③ 把物料綁到中光電儲位
                                        ├── POST /locations/{C}/Mapping ──►
                                        │      { itemCode: WorkOrder }
                                        │ ◄──── 0 成功 / 1 已佔用（異常，記 Error）
                                        ▼
                                      結束
```

### 5-3 物料資訊清除 — 客戶流程「物料資訊清除」

```
 ACC StockSensorLink（PLC sensor 輪詢）                            中光電 WCS
 ─────────────────────────────────────                            ──────────
 中光電貨架 Sensor OFF（物料消失，例如中光電 AGV 取走）
        │ 依既有去抖（ConfirmCount）確認清空，清我方 oPort（既有）
        │ ④ 該站屬中光電貨架 → 通知中光電
        ├──────────────── POST /locations/{binCode}/release ───────────►
        │ ◄──────────────── 0 成功 / 1 已釋放（視為成功）
        ▼
      結束
```

### 5-4 選擇理由

| 呼叫點 | 理由 |
|--------|------|
| ① 建任務前 `status` | 物料資訊須先取得，才能隨 oNeed → oMission 傳到終點，`end` 時既有的 `Update_oPort` 直接寫入；查不到不派車，避免搬回來路不明的貨架 |
| ② `outbin` 時 `release` | 貨架實際離開中光電儲位的時刻，與我方清空起點 oPort 同一時間點；客戶流程「AGV 取得貨物 → POST release」 |
| ③ `end` 時 `Mapping` | 貨架確實放下才綁定；任務中途取消不會在中光電留下錯誤綁定；客戶流程「AGV 搬運卸貨 → POST Mapping」 |
| ④ Sensor OFF 時 `release` | 客戶流程指定；涵蓋非我方搬走的情境（中光電 AGV 取走我方放上的貨） |

## 六、實作注意事項

| # | 事項 | 建議 |
|---|------|------|
| 1 | ②③ 在 RCS 回呼內同步呼叫中光電，對方慢或斷線會拖住 RCS 回應 | 寫入佇列，背景送出並重試；結果記 Log |
| 2 | **取貨時 release 會呼叫兩次**：② `outbin` 一次、緊接著 Sensor OFF ④ 一次 | 第二次回 `1`（已釋放）視為成功；或請客戶確認取貨後只靠 ④（待確認 #9） |
| 3 | `outbin` 後才取消任務：貨架在 AGV 上，中光電已 release | 訂人工處理 SOP |
| 4 | 2-2 回 `1`（已佔用） | 兩邊資料不一致：記 Error 並通知人員 |
| 5 | 回 `2`（ERROR）或逾時 | 重試（次數／間隔待定），超過上限記 Error |
| 6 | 我方站號 ↔ 中光電 `binCode` | 設定檔對照表（例：`O1 → 中光電 A 的 binCode`），我方站號沿用 T1 草案（中光電 A/B/C/D → O1/P1/Z1/D1） |
| 7 | ④ 的觸發點在 ACC `StockSensorLinkManager` | 既有 sensor 清空只處理 HaveFlag ∈ {1,3}（HaveFlag=2 不清），中光電站點的 HaveFlag 規劃須一併考量 |
| 8 | 實作位置 | ① SCP AutoDispatch（T6）；②③ ACC 回呼或 T8 物料轉寫；④ ACC StockSensorLink（T13）；統一由 T25 的 client 送出 |

## 七、待中光電／客戶確認

| # | 問題 | 狀態 |
|---|------|------|
| 1 | 呼叫時機：status 建任務前、release 於取貨（outbin）、Mapping 於卸貨（end） | ✅ 2026-10-06 客戶流程圖確認 |
| 2 | 放貨後由我方呼叫 `Mapping`；取貨前由我方先查 `status` | ✅ 2026-10-06 客戶流程圖確認 |
| 3 | 四個貨架（中光電 A／B／C／D）各自的 `binCode` | ❓ 待回覆 |
| 4 | `itemCode` 對應我方哪個欄位：工單（WorkOrder）、貨架條碼（RackId），或組合格式 | ❓ 待回覆 |
| 5 | `code=1`／`code=2` 的處理方式與重試建議 | ❓ 待回覆 |
| 6 | Base URL、認證方式（token／IP 白名單）、逾時時間 | ❓ 待回覆 |
| 7 | 2-3 說明欄「綁定儲位的物料資訊」是否為筆誤 | ❓ 待回覆 |
| 8 | **R2 改人員派發後，「最舊物料優先（FIFO）」如何落實**：起點清單依 PutTime 排序、系統自動帶最舊一筆，或由人員自選 | 🆕 待回覆 |
| 9 | **取貨後 release 是否只靠 Sensor OFF**（避免 outbin 與 Sensor OFF 各呼叫一次） | 🆕 待回覆 |
