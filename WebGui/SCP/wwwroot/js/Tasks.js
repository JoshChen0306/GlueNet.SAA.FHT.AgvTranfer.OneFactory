var barChart, pieChart;
$(function () {
    
});

$('button[data-bs-toggle="tab"]').on('shown.bs.tab', function (e) {
    //barChart.resize();
    //pieChart.resize();
});

function GetSearchData() {
    // 獲取daterangepicker實例
    var datePicker = $('input[name="dates"]').data('daterangepicker');

    // 獲取選擇的開始和結束日期，並格式化為所需的格式
    var startDate = datePicker.startDate.format('YYYYMMDD');
    var endDate = datePicker.endDate.format('YYYYMMDD');

    var shuttleId = $('#ShuttleId').val();
    var shiftId = $('#ShiftId').val();

    $.ajax({
        url: '/Tasks/GetMission',
        type: 'GET',
        data: {
            startDate: startDate,
            endDate: endDate,
            shuttleId: shuttleId,
            shiftId: shiftId
        },
        success: function (data) {
            // 檢查 DataTable 實例是否已存在，如果存在則銷毀
            if ($.fn.DataTable.isDataTable("#task-table")) {
                $("#task-table").DataTable().destroy();
            }
            $("#Data").html(data)
            // 重新初始化 DataTable
            $("#task-table").DataTable({
                pageLength:8,
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
        url: '/Tasks/GetBarChat',
        type: 'GET',
        data: {
            startDate: startDate,
            endDate: endDate,
            shuttleId: shuttleId,
            shiftId: shiftId
        },
        success: function (data) {
            TaskBar(data, datePicker);
        },
        error: function (xhr, ajaxOptions, thrownError) {
            console.log(xhr.status);
            console.log(thrownError);
        }
    });

    $.ajax({
        url: '/Tasks/GetTasks',
        type: 'GET',
        data: {
            startDate: startDate,
            endDate: endDate,
            shuttleId: shuttleId,
            shiftId: shiftId
        },
        success: function (data) {
            // 檢查 DataTable 實例是否已存在，如果存在則銷毀
            if ($.fn.DataTable.isDataTable("#detail-table")) {
                $("#detail-table").DataTable().destroy();
            }
            $("#Tasks").html(data)
            // 重新初始化 DataTable
            $("#detail-table").DataTable({
                scrollX: true, // 啟用水平滾動條
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

function TaskBar(data, datePicker) {

    var taskBar = document.getElementById('Bar');
    //var titleName = taskBar.dataset.title;
    var barChart = echarts.getInstanceByDom(taskBar);
    if (!barChart) {
        barChart = echarts.init(taskBar);
    }
     

    // 將日期格式化為更簡潔的格式，並提取所有不重複的日期和班次名稱
    var endDate = datePicker.endDate;
    var startDate = datePicker.startDate;
    var title = taskBar.dataset.title;

    // 生成日期列表
    var dates = [];
    for (var m = startDate.clone(); m.isBefore(endDate.clone()); m.add(1, 'days')) {
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
                text: `${title}:${startDate.format('M/D')}-${endDate.format('M/D')}`,
            }
        ],
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
    
    barChart.setOption(option,true);
}
