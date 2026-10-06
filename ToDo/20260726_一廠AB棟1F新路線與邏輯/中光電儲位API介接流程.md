# 中光電儲位 API 介接流程（草案）

> 建立日期：2026-10-06｜狀態：**草案，呼叫時機為我方推薦，待中光電確認**
> 來源規格：`D:\格路科技\格路科技 - 開發團隊 - project-客製化專案\2025\0.評估中\20251013_迅得_高技_自動搬運專案系統\一廠AB棟搬運\儲位api.pdf`（客戶提供）
> 相關任務：T8 任務完成物料資訊轉寫、T21 R1 跨派車系統物料資訊、T1 區域代碼（`區域代碼對照表.md`）

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

## 三、我方任務生命週期（既有，ACC `API/CallBackAPI.cs` `AGVCallback`）

| RCS 回呼 | 意義 | 我方現行動作 |
|----------|------|-------------|
| `start` | AGV 開始執行任務 | oRequire／oMission 狀態更新為執行中 |
| `outbin` | AGV 頂起貨架、離開起點儲位 | `Update_oPortEmpty`：清空**起點** oPort |
| `end` | 放下貨架、任務完成 | `Update_oPort`：把 oMission 的 WorkOrder／RackId 寫入**終點** oPort（有工單 HaveFlag=3，否則 1） |
| `cancel` | 任務取消 | oMission 歸檔，OkFlag=C |

## 四、呼叫時機（推薦）

| 路線 | 中光電儲位角色 | 呼叫 | 時機 |
|------|--------------|------|------|
| R1 中光電 D → 迅得 2/3/4 | 取貨 | ① 2-1 `status` → ② 2-3 `release` | ① 建任務前；② `outbin` |
| R4 中光電 A → 迅得 5/6/7 | 取貨 | ① 2-1 `status` → ② 2-3 `release` | ① 建任務前；② `outbin` |
| R2 迅得 2/3/4 → 中光電 C | 放貨 | ③ 2-2 `Mapping` | ③ `end` |
| R3 迅得 1 → 中光電 B | 放貨 | ③ 2-2 `Mapping` | ③ `end` |

### 4-1 從中光電取貨（R1、R4）

```
 SCP 自動派送 (AutoDispatch)            ACC (RCS 回呼)                 中光電 WCS
 ───────────────────────────            ──────────────                 ──────────
 sensor：中光電 D 有貨
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
                                   [outbin] AGV 頂起貨架離開 D
                                        │ 清空我方 D 的 oPort（既有）
                                        │ ② 通知中光電 D 已空
                                        ├── POST /locations/{D}/release ──►
                                        │ ◄──── 0 成功 / 1 已釋放（視為成功）
                                        │
                                   [end] 放到迅得 E1
                                        │ E1 的 oPort 寫入 WorkOrder=itemCode（既有）
                                        ▼
                                      完成
```

### 4-2 放貨到中光電（R2、R3）

```
 SCP 自動派送                            ACC (RCS 回呼)                 中光電 WCS
 ────────────                            ──────────────                 ──────────
 迅得 E 有料（取 PutTime 最舊）
 且中光電 C 空（sensor）
        │
   建 oNeed（WorkOrder = 我方物料）
        │
   cPair → oRequire → oMission → RCS 執行
                                        │
                                   [outbin] 離開 E1，清空我方 E1（既有）
                                        │
                                   [end] 貨架放到中光電 C
                                        │ 我方 C 的 oPort 寫入物料（既有）
                                        │ ③ 把物料綁到中光電儲位
                                        ├── POST /locations/{C}/Mapping ──►
                                        │      { itemCode: WorkOrder }
                                        │ ◄──── 0 成功 / 1 已佔用（異常，記 Error）
                                        ▼
                                      完成
```

### 4-3 選擇理由

| 呼叫點 | 理由 |
|--------|------|
| ① 建任務前 `status` | 物料資訊須先取得，才能隨 oNeed → oMission 傳到終點，`end` 時既有的 `Update_oPort` 直接寫入，不需額外處理；查不到不派車，避免搬回來路不明的貨架 |
| ② `outbin` 時 `release` | 貨架實際離開中光電儲位的時刻，與我方清空起點 oPort 同一時間點，兩邊狀態最一致 |
| ③ `end` 時 `Mapping` | 貨架確實放下才綁定；任務中途取消不會在中光電留下錯誤綁定 |

## 五、實作注意事項

| # | 事項 | 建議 |
|---|------|------|
| 1 | ②③ 在 RCS 回呼內同步呼叫中光電，對方慢或斷線會拖住 RCS 回應 | 寫入佇列，背景送出並重試；結果記 Log |
| 2 | `outbin` 後才取消任務：貨架在 AGV 上，中光電已 release | 訂人工處理 SOP |
| 3 | 2-3 回 `1`（已釋放） | 視為成功 |
| 4 | 2-2 回 `1`（已佔用） | 兩邊資料不一致：記 Error 並通知人員 |
| 5 | 回 `2`（ERROR）或逾時 | 重試（次數／間隔待定），超過上限記 Error |
| 6 | 我方站號 ↔ 中光電 `binCode` | 設定檔對照表（例：`O1 → 中光電 A 的 binCode`），我方站號沿用 T1 草案（中光電 A/B/C/D → O1/P1/Z1/D1） |
| 7 | 實作位置 | ① 在 SCP AutoDispatch（T6）；②③ 在 ACC 回呼或 T8 物料轉寫，與 T8 一併決定 |

## 六、待中光電／客戶確認

| # | 問題 |
|---|------|
| 1 | 上述三個呼叫時機是否同意？尤其 `release` 在「頂起離開時（outbin）」或「任務完成時（end）」 |
| 2 | 放貨後是由我方呼叫 `Mapping`，或中光電系統自行綁定？取貨前是否需要我方先查 `status`？ |
| 3 | 四個貨架（中光電 A／B／C／D）各自的 `binCode` |
| 4 | `itemCode` 對應我方哪個欄位：工單（WorkOrder）、貨架條碼（RackId），或組合格式 |
| 5 | `code=1`／`code=2` 的處理方式與重試建議 |
| 6 | Base URL、認證方式（token／IP 白名單）、逾時時間 |
| 7 | 2-3 說明欄「綁定儲位的物料資訊」是否為筆誤 |
