public enum HikStatusEnum : int
{
    /// <summary>
    /// 任務完成
    /// </summary>
    TaskCompleted = 1,
    /// <summary>
    /// 任務執行中
    /// </summary>
    ExecutingTask = 2,
    /// <summary>
    /// 任務異常
    /// </summary>
    AbnormalTask = 3,
    /// <summary>
    /// 任務空閒
    /// </summary>
    IdleTask = 4,
    /// <summary>
    /// 機器人暫停
    /// </summary>
    RobotStopped = 5,
    /// <summary>
    /// 舉升貨架狀態
    /// </summary>
    LiftingShelfStatus = 6,
    /// <summary>
    /// 充電狀態
    /// </summary>
    ChargingStatus = 7,
    /// <summary>
    /// 弧線行走中
    /// </summary>
    BatteryArcingInProgress = 8,
    /// <summary>
    /// 充滿維護
    /// </summary>
    FullyCharged = 9,
    /// <summary>
    /// 背貨未識別
    /// </summary>
    CarriedItemNotRecognized = 10,
}