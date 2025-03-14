let barChart, pieChart;
$(function () {   
    var TaskPie = document.getElementById('pie');
    pieChart = echarts.getInstanceByDom(TaskPie)
    if (!pieChart) {
        pieChart = echarts.init(TaskPie);
    }
    $('button[data-bs-toggle="tab"]').on('shown.bs.tab', function (e) {
        barChart.resize();
        pieChart.resize();
    });

});

function SearchData() {
    // 獲取daterangepicker實例
    var datePicker = $('input[name="dates"]').data('daterangepicker');

    // 獲取選擇的開始和結束日期，並格式化為所需的格式
    var startDate = datePicker.startDate.format('YYYYMMDD');
    var endDate = datePicker.endDate.format('YYYYMMDD');

    var shuttleId = $('#ShuttleId').val();

    $.ajax({
        url: '/Activation/GetPieActivation',
        type: 'GET',
        data: {
            startDate,
            endDate,
            shuttleId
        },
        success: function (data) {
            AgvPie(data);
        },
        error: function (xhr, ajaxOptions, thrownError) {
            console.log(xhr.status);
            console.log(thrownError);
        }
    });

    $.ajax({
        url: '/Activation/GetBarActivation',
        type: 'GET',
        data: {
            startDate,
            endDate,
            shuttleId
        },
        success: function (data) {
            AgvBar(data);
        },
        error: function (xhr, ajaxOptions, thrownError) {
            console.log(xhr.status);
            console.log(thrownError);
        }
    });

    $.ajax({
        url: '/Activation/GetTaskTable',
        type: 'GET',
        data: {
            startDate,
            endDate,
            shuttleId,
        },
        success: function (data) {
            // 檢查 DataTable 實例是否已存在，如果存在則銷毀
            if ($.fn.DataTable.isDataTable("#task-table")) {
                $("#task-table").DataTable().destroy();
            }
            $("#taskTabContent").html(data)
            // 重新初始化 DataTable
            $("#task-table").DataTable({
                pageLength: 8,
                autoWidth: false,
                language: {
                    url: "../JSON/zh-HANT.json"
                },
                layout: {
                    topStart: {
                        buttons: ['copy', {
                            extend: 'csv',
                            text: 'CSV',
                            bom: true
                        }, 'excel'/* , 'pdf', 'print' */]
                    }
                },
                columnDefs: [
                    { className: "text-center", targets: "_all" }
                ],
            });
        },
        error: function (xhr, ajaxOptions, thrownError) {
            console.log(xhr.status);
            console.log(thrownError);
        }
    });

    $.ajax({
        url: '/Activation/GetDetailTable',
        type: 'GET',
        data: {
            startDate,
            endDate,
            shuttleId,
        },
        success: function (data) {         
            // 檢查 DataTable 實例是否已存在，如果存在則銷毀
            if ($.fn.DataTable.isDataTable("#detail-table")) {
                $("#detail-table").DataTable().destroy();
            }
            $("#detailTabContent").html(data)
            // 重新初始化 DataTable
            $("#detail-table").DataTable({
                pageLength: 8,
                autoWidth: false,
                language: {
                    url: "../JSON/zh-HANT.json"
                },
                layout: {
                    topStart: {
                        buttons: ['copy', {
                            extend: 'csv',
                            text: 'CSV',
                            bom: true
                        }, 'excel'/* , 'pdf', 'print' */]
                    }
                },
                columnDefs: [
                    { className: "text-center", targets: "_all" }
                ],
            });
        },
        error: function (xhr, ajaxOptions, thrownError) {
            console.log(xhr.status);
            console.log(thrownError);
        }
    });
    $(".Interval").text(`${datePicker.startDate.format('M/D')}-${datePicker.endDate.format('M/D')}`)
}

function AgvPie(data) {

    

    // 獲取daterangepicker實例
    var datePicker = $('input[name="dates"]').data('daterangepicker');

    // 獲取選擇的開始和結束日期
    var startDate = datePicker.startDate
    var endDate = datePicker.endDate

    var totalRangeTime = endDate.diff(startDate, 'hours');

    // 根據資料筆數動態生成餅圖配置
    var taskName = { Travling: "運行", Idle: "閒置", Charging: "充電", Alarm:"異常",Offline:"離線"}
    var series = [];
    var title = [];
    var centers = data.length > 1 ? [['25%', '50%'], ['75%', '50%']] : [['50%', '50%']];
    var titelLeft = data.length > 1 ? ['24%','74%']: ['49%'];

    if (!data || data.length === 0) {
        // 沒有資料時的處理
        pieChart.clear(); // 清除餅圖
        pieChart.setOption({
            title: {
                text: '無資料',
                subtext: '沒有找到符合條件的資料',
                left: 'center',
                top:'5%'
            }
        });
        return; // 終止函數執行
    }
    $.each(data,function (index, item) {
        var pieData = [];
        $.each(taskName,function (index, taskType) {
            pieData.push({
                value: item[index],
                name: taskType,
                itemStyle: index === "Offline" ?{ color: "rgba(167, 164, 164, 0.93)" } : {}
            });
        })

        series.push({
            name: item.ShuttleId,
            type: 'pie',
            radius: [70, 100],
            center: centers[index],
            data: pieData
        });

        title.push({
            text: item.ShuttleId,
            top: '48%',
            left: titelLeft[index],
            textAlign: 'center'
        })
    });

    //圓餅圖
    var pieoption = {
        title: {
            top: '5%',
            left: '3%',
            text: `統計區間:${startDate.format('M/D')} -${endDate.format('M/D')}`,
            textStyle: {
                color: '#747474',
            },
        },
        tooltip: {
            trigger: 'item'
        },
        legend: {
            top: '5%',
            right: '5%',
            itemWidth: 30,
            itemGap: 20,
        },
        label: {
            formatter: '{b}:{d}%',
            position: 'center',
        },
        series: series,
        title,
        // Optional. Only for responsive layout:
    };
    pieChart.clear();
    pieChart.setOption(pieoption);
}

function AgvBar(data) {
    var TaskBar = document.getElementById('bar');

    barChart = echarts.init(TaskBar);

    // 獲取daterangepicker實例
    var datePicker = $('input[name="dates"]').data('daterangepicker');

    // 獲取選擇的開始和結束日期
    var startDate = datePicker.startDate
    var endDate = datePicker.endDate

    // 生成日期列表
    var dates = [];
    for (var m = startDate.clone(); m.isBefore(endDate.clone()); m.add(1, 'days')) {
        dates.push(m.format('M/D'));
    }
    if (!data || data.length === 0) {
        // 沒有資料時的處理
        barChart.clear(); // 清除餅圖
        barChart.setOption({
            title: {
                text: '無資料',
                subtext: '沒有找到符合條件的資料',
                left: 'center',
                top: '5%'
            }
        });
        return; // 終止函數執行
    }

    var shuttleId = [...new Set(data.map(item => item.ShuttleId))];
    // 根據預期順序進行排序
    shuttleId.sort(function (a, b) {
        return Number(a) - Number(b);
    });
    // 初始化 series 數據結構
    var series = shuttleId.map(shuttleId => ({
        name: shuttleId,
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
        var shuttleIndex = shuttleId.indexOf(item.ShuttleId);
        series[shuttleIndex].data[index] = item.Activation;
    });
    // 確保所有未指定的日期都填充為 0
    $.each(series, function (index, item) {
        for (let i = 0; i < dates.length; i++) {
            if (item.data[i] === undefined) {
                item.data[i] = 0;
            }
        }
    });

    //柱狀圖
    var baroption = {
        grid: {
            top: '20%',
            bottom: '10%',
        },
        title: {
            top: '5%',
            left: '3%',
            text: `統計區間:${startDate.format('M/D')} -${endDate.format('M/D')}`,
            textStyle: {
                color: '#747474',
            },
        },
        tooltip: {
            trigger: 'axis'
        },
        legend: {
            top: '5%',
            data: shuttleId,
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

    barChart.clear()
    barChart.setOption(baroption);

}