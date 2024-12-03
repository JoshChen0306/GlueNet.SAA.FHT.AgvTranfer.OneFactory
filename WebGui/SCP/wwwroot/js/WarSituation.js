import { connection } from './common/hub.js';
//顯示當前時間
$(function () {
    var descriptions = [];
    UpdateTotalTask();
    $('.Tasktype').each(function () {
        descriptions.push($(this).text().trim());
    });

    if (descriptions.includes("異常")) {
        window.parent.$('.fa-bell').addClass('fa-shake');
    }
    UpdateTotalAlarm();
    //稼動率圖表
    setTimeout(function run() {
        $.ajax({
            type: "GET",
            url: "/Home/GetAgvActivation",
            success: function (data) {
                AgvPie(data);
            },
            error: function (jqXHR, textStatus, errorThrown) {
                console.error("AJAX 請求失敗: ", textStatus, errorThrown);
            }
        });
        // 在函數內部設置 setInterval，確保下一次執行在 AJAX 請求完成後開始計時
        setTimeout(run, 10000);
    }, 0);

    //歷史任務圖表
    setTimeout(function run() {

        var now = moment();
        var nextTime = moment().hour(8).minute(30).second(0);

        if (now.isAfter(nextTime)) {
            nextTime.add(1,'days');
        }

        var timeout = nextTime.diff(now);
        $.ajax({
            type: "GET",
            url: "/Home/GetMission",
            success: function (data) {
                //console.log(data)
                TaskBar(data);
            },
            error: function (jqXHR, textStatus, errorThrown) {
                console.error("AJAX 請求失敗: ", textStatus, errorThrown);
            }
        });
        // 在函數內部設置 setInterval，確保下一次執行在 AJAX 請求完成後開始計時
        setTimeout(run, timeout);
    }, 0);

    //車輛任務狀態更新
    connection.on("SendAgvStatusChange", function () {
        $.ajax({
            type: "GET",
            url: "/Home/UpdateAgvStatus",
            success: function (data) {
                $("#TaskStatus").html(data);
                descriptions = [];
                $('.Tasktype').each(function () {
                    descriptions.push($(this).text().trim());
                });

                if (descriptions.includes("異常")) {
                    window.parent.$('.fa-bell').addClass('fa-shake');
                    UpdateTotalAlarm()
                }
                else {
                    window.parent.$('.fa-bell').removeClass('fa-shake');
                }
            },
            error: function (jqXHR, textStatus, errorThrown) {
                // 處理錯誤
                console.error("AJAX 請求失敗: ", textStatus, errorThrown);
            }
        });

       
    });

    //更新今日任務
    connection.on("SendTotalTaskChange", function () {
        UpdateTotalTask();
    });
});

function UpdateTotalTask() {
    $.ajax({
        type: "GET",
        url: "/Home/UpdateTotalTask",
        success: function (data) {
            //console.log(data);
            $("#TotalTask").text(`${data}`);
        },
        error: function (jqXHR, textStatus, errorThrown) {
            // 處理錯誤
            console.error("AJAX 請求失敗: ", textStatus, errorThrown);
        }
    });
}

function UpdateTotalAlarm() {
    $.ajax({
        type: "POST",
        url: "/Home/UpdateAlarm",
        success: function (data) {
            $("#TotalAlarm").text(`${data}`);
        },
        error: function (jqXHR, textStatus, errorThrown) {
            console.error("異常處理失敗:", textStatus, errorThrown);
        }
    });
}
//稼動率圓餅圖
function AgvPie(data) {
    var totalSecond = 0
    var AgvPie = document.getElementById('AGV-Pie');
    var titleName = AgvPie.dataset.title;
    var myChart = echarts.getInstanceByDom(AgvPie);
    if (!myChart) {
        // 如果沒有現有的實例，則初始化一個新的實例
        myChart = echarts.init(AgvPie);
    }
    

    // 獲取當前時間
    var now = moment();
    // 獲取當天的開始時間（午夜12點）
    var startOfDay = moment().hour(8).minute(30).second(0);
    console.log(startOfDay);
    if (now.isBefore(startOfDay)) {
        startOfDay.subtract(1,'days')
    }
    // 計算從午夜到現在的秒數
    var nowTime = now.diff(startOfDay, 'hours',true);
    nowTime = Math.round(nowTime * 100) / 100;

    var timeDifferences = { R: "", C: "", A: "", F: "" };
    $.each(data, function (index, item) {
        if (timeDifferences.hasOwnProperty(item.GroupID)) {
            timeDifferences[item.GroupID] = item.TimeDifference;
            totalSecond += item.TimeDifference
        }
    });
    var idleTime = (nowTime - totalSecond).toFixed(2);
    var option = {
        title:
        {
            top: '5%',
            left: '3%',
            text: titleName,
        },
        tooltip: {
            trigger: 'item'
        },
        legend: {
            bottom: '5%',
            left: '3%',
            itemGap: 15,
        },
        series: [
            {
                grid: {
                    right: '10%',
                    containLabel: true
                },
                name: 'AGV稼動率',
                type: 'pie',
                radius: ['40%', '55%'],
                center: ['50%', '45%'],
                avoidLabelOverlap: false,
                label: {
                    fontWeight: 'bold',
                    fontSize: 18,
                    show: false,
                    formatter: '{b}:{d}%',
                    position: 'center',
                },
                labelLine: {
                    show: false
                },
                data: [
                    { value: timeDifferences["R"], name: '運行', label: { show: true } },
                    { value: idleTime, name: '閒置' },
                    { value: timeDifferences["C"], name: '充電' },
                    { value: timeDifferences["A"], name: '異常' },
                    { value: timeDifferences["F"], name: '離線', itemStyle: {color: "rgba(167, 164, 164, 0.93)"}}

                ]
            }
        ]
    };
    myChart.setOption(option);
}

//任務統計柱狀圖
function TaskBar(data) {

    var taskBar = document.getElementById('Task-Bar');
    var titleName = taskBar.dataset.title;
    var myChart = echarts.getInstanceByDom(taskBar);
    if (!myChart) {
        myChart = echarts.init(taskBar);
    }


    // 將日期格式化為更簡潔的格式，並提取所有不重複的日期和班次名稱
    var endDate = moment().subtract(1, 'days'); // 當天前一天
    var startDate = moment().subtract(7, 'days'); // 從結束日期往前推六天

    // 生成日期列表
    var dates = [];
    for (var m = startDate; m.isBefore(endDate.clone().add(1, 'days')); m.add(1, 'days')) {
        dates.push(m.format('M/D'));
    }
    var shiftNames = [...new Set(data.map(item => item.ShiftName))];

    // 初始化 series 數據結構
    var series = shiftNames.map(shiftName => ({
        name: shiftName,
        type: 'bar',
        data: [],
        markPoint: {
            data: [
                { type: 'max', name: 'Max' },
                { type: 'min', name: 'Min' }
            ]
        },
        markLine: {
            data: [{ type: 'average', name: 'Avg' }]
        }
    }));

    // 填充 series 中的數據
    $.each(data, function (index, item) {
        var date = moment(item.Date).format('M/D');
        var index = dates.indexOf(date);
        var shiftIndex = shiftNames.indexOf(item.ShiftName);
        series[shiftIndex].data[index] = item.Count;
    });

    // 確保所有未指定的日期都填充為 0
    $.each(series, function (index, item) {
        for (let i = 0; i < dates.length; i++) {
            if (item.data[i] === undefined) {
                item.data[i] = 0;
            }
        }
    });

    var option = {
        grid: {
            top: '30%',
            bottom: '10%',
        },
        title: [
            {
                top: '5%',
                left: '3%',
                text: titleName
            },
            {
                top: '3%',
                left: '22%',
                subtext: `統計區間:${dates[0]}-${dates[6]}`,
            }],
        tooltip: {
            trigger: 'axis'
        },
        legend: {
            top: '5%',
            data: ['早班', '晚班'],
            itemWidth: 30,
        },
        toolbox: {
            right: '2%',
            show: true,
            feature: {
                dataView: { show: false, readOnly: false },
                magicType: { show: true, type: ['line', 'bar'] },
                restore: { show: true },
                saveAsImage: { show: true }
            }
        },
        calculable: true,
        xAxis: [
            {
                type: 'category',
                data: dates
            }
        ],
        yAxis: [
            {
                type: 'value'
            }
        ],
        series: series
    };

    myChart.setOption(option);
}