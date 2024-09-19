public enum HikFunctionEnum : int
{
    None = 0,
    /// <summary>
    /// 生成任務單
    /// </summary>
    genAgvSchedulingTask,
    /// <summary>
    /// 繼續執行任務
    /// </summary>
    continueTask,
    /// <summary>
    /// 取消任務
    /// </summary>
    cancelTask,
    /// <summary>
    /// 任務優先級設置
    /// </summary>
    setTaskPriority,
    /// <summary>
    /// 貨架與位置綁定、解綁
    /// </summary>
    bindPodAndBerth,
    /// <summary>
    /// 貨架與物料綁定、解綁
    /// </summary>
    bindPodAndMat,
    /// <summary>
    /// 位置禁用語啟用
    /// </summary>
    lockPosition,
    /// <summary>
    /// 地圖位置信息同步
    /// </summary>
    syncMapDatas,
    /// <summary>
    /// 查詢貨架儲位與物料批次關係
    /// </summary>
    queryPodBerthAndMat,
    /// <summary>
    /// 倉位禁用語啟用
    /// </summary>
    blockStgBin,
    /// <summary>
    /// 容器與倉位綁定、解綁
    /// </summary>
    bindCtnrAndBin,
    /// <summary>
    /// 查詢任務狀態
    /// </summary>
    queryTaskStatus,
    /// <summary>
    /// 查詢 AGV 狀態
    /// </summary>
    queryAgvStatus,
    /// <summary>
    /// 停止 AGV
    /// </summary>
    stopRobot,
    /// <summary>
    /// 恢復 AGV
    /// </summary>
    resumeRobot,
    /// <summary>
    /// 區域清空、釋放
    /// </summary>
    blockArea,
    /// <summary>
    /// 預調度對外接口
    /// </summary>
    genPreScheduleTask,
    /// <summary>
    /// 清空巷道
    /// </summary>
    clearRoadWay,
    /// <summary>
    /// 料箱出庫 TPS（CTU+分撥牆）
    /// </summary>
    getOutPod,
    /// <summary>
    /// 料箱回庫 TPS（CTU+分撥牆）
    /// </summary>
    returnPod,
    /// <summary>
    /// 料箱順序出庫（CTU）
    /// </summary>
    genCtuGroupTaskBatch,
    /// <summary>
    /// 料箱取放回檔（CTU）
    /// </summary>
    boxApplyPass,
    /// <summary>
    /// 指定料箱終點（CTU）
    /// </summary>
    bindCtnrDestination,

    #region CallBack Function
    /// <summary>
    /// 任務執行通知
    /// </summary>
    agvCallback = 51,
    /// <summary>
    /// 告警推送通知
    /// </summary>
    warnCallback,
    /// <summary>
    /// 綁定解綁通知
    /// </summary>
    bindNotify,
    /// <summary>
    /// 申請回庫倉位（CTU）
    /// </summary>
    applyBin,
    #endregion CallBack Function
}