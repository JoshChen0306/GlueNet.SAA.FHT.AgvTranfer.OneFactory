using SCP.Models;

namespace SCP.Helpers
{
    /// <summary>
    /// 站點是否被進行中任務占用、是否為可放置空位的共用判斷。
    /// pending 定義與 AutoDispatchRouteEngine 相同：
    /// oNeed AssignFlag 空/Y、oRequire OkFlag 空/R、oMission OkFlag 空/Y/R。
    /// </summary>
    public static class StationAvailability
    {
        /// <summary>站點是否為進行中任務的起點。</summary>
        public static bool IsPendingAsBegin(agvDB_1400004Context db, string stationNo)
            => db.oNeed.Any(n => n.ObjStation == stationNo && (n.AssignFlag == null || n.AssignFlag == "" || n.AssignFlag == "Y")) ||
               db.oRequire.Any(r => r.BeginStation == stationNo && (r.OkFlag == null || r.OkFlag == "" || r.OkFlag == "R")) ||
               db.oMission.Any(m => m.BeginStation == stationNo && (m.OkFlag == null || m.OkFlag == "" || m.OkFlag == "Y" || m.OkFlag == "R"));

        /// <summary>站點是否已被進行中任務指派為終點。</summary>
        public static bool IsPendingAsEnd(agvDB_1400004Context db, string stationNo)
            => db.oNeed.Any(n => n.EndStation == stationNo && (n.AssignFlag == null || n.AssignFlag == "" || n.AssignFlag == "Y")) ||
               db.oRequire.Any(r => r.EndStation == stationNo && (r.OkFlag == null || r.OkFlag == "" || r.OkFlag == "R")) ||
               db.oMission.Any(m => m.EndStation == stationNo && (m.OkFlag == null || m.OkFlag == "" || m.OkFlag == "Y" || m.OkFlag == "R"));

        /// <summary>可放置空位：HaveFlag=0、未保留（BgnToEnd 空）、啟用中。不含 pending 檢查。</summary>
        public static bool IsEmptySlot(oPort port)
            => port.HaveFlag == "0" && string.IsNullOrEmpty(port.BgnToEnd) && port.UseFlag == "Y";
    }
}
