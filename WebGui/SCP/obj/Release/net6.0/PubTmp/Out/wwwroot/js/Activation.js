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

    var TaskBar = document.getElementById('bar');
    

    TaskBar.style.height = '553px';


    barChart = echarts.init(TaskBar);
   
    //柱狀圖
    var baroption = {
        grid: {
            top: '20%',
            bottom: '10%',
        },
        title: {
            top: '5%',
            left: '3%',
            text: '統計區間:4/22-4/28',
            textStyle: {
                color: '#747474',
            },
        },
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
                // prettier-ignore
                data: ['4/22', '4/23', '4/24', '4/25', '4/26', '4/27', '4/28']
            }
        ],
        yAxis: [
            {
                type: 'value'
            }
        ],
        series: [
            {
                name: '早班',
                type: 'bar',

                data: [
                    2.0, 4.9, 7.0, 23.2, 25.6, 76.7, 35.6,
                ],
                markPoint: {
                    data: [
                        { type: 'max', name: 'Max' },
                        { type: 'min', name: 'Min' }
                    ]
                },
                markLine: {
                    data: [{ type: 'average', name: 'Avg' }]
                }
            },
            {
                name: '晚班',
                type: 'bar',
                data: [
                    2.6, 5.9, 9.0, 26.4, 28.7, 70.7, 75.6,
                ],
                markPoint: {
                    data: [
                        { type: 'max', name: 'Max' },
                        { type: 'min', name: 'Min' }
                    ]
                },
                markLine: {
                    data: [{ type: 'average', name: 'Avg' }]
                }
            }
        ]
    };
    

    barChart.setOption(baroption);
    
});

function SearchData() {
    // 獲取daterangepicker實例
    var datePicker = $('input[name="dates"]').data('daterangepicker');

    // 獲取選擇的開始和結束日期，並格式化為所需的格式
    var startDate = datePicker.startDate.format('YYYYMMDD');
    var endDate = datePicker.endDate.format('YYYYMMDD');

    var shuttleId = $('#ShuttleId').val();

    $.ajax({
        url: '/Activation/GetActivation',
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
        url: '/Activation/GetTaskTable',
        type: 'GET',
        data: {
            startDate,
            endDate,
            shuttleId,
        },
        success: function (data) {
            $("#taskTabContent").html(data)
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