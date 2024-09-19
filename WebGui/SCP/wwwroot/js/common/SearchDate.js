$('input[name="dates"]').daterangepicker({
    "showDropdowns": true,
    "maxSpan": {
        "days": 30
    },
    ranges: {
        '今天': [moment(), moment()],
        '昨天': [moment().subtract(1, 'days'), moment().subtract(1, 'days')],
        '前 7 天': [moment().subtract(6, 'days'), moment()],
        '前 30 天': [moment().subtract(29, 'days'), moment()],
        '這個月': [moment().startOf('month'), moment().endOf('month')],
        '上個月': [moment().subtract(1, 'month').startOf('month'), moment().subtract(1, 'month').endOf('month')]
    },
    "locale": {
        "format": "YYYY年MM月DD日",
        "separator": " - ",
        "applyLabel": "確認",
        "cancelLabel": "取消",
        "customRangeLabel": "自訂義範圍",
        "daysOfWeek": [
            "日",
            "一",
            "二",
            "三",
            "四",
            "五",
            "日"
        ],
        "monthNames": [
            "1 月",
            "2 月",
            "3 月",
            "4 月",
            "5 月",
            "6 月",
            "7 月",
            "8 月",
            "9 月",
            "10 月",
            "11 月",
            "12 月"
        ],
        "firstDay": 1
    },
    "startDate": moment().subtract(30, 'days'),
    "endDate": moment(),
    "maxDate": moment(),
});