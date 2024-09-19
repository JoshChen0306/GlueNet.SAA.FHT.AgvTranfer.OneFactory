document.getElementById('Start').addEventListener('change', function () {
    var endDateInput = document.getElementById('End');
    // 獲取所選日期的年份和月份
    var selectedDate = new Date(this.value);
    var year = selectedDate.getFullYear();
    var month = selectedDate.getMonth();

    // 計算當月的最後一天
    var lastDayOfMonth = new Date(year, month + 1, 0).getDate();

    // 設置第二個日期選擇器的範圍為當月的日期，並將其值設置為當月的最後一天
    endDateInput.setAttribute('min', year + '-' + ('0' + (month + 1)).slice(-2) + '-01');
    endDateInput.setAttribute('max', year + '-' + ('0' + (month + 1)).slice(-2) + '-' + lastDayOfMonth);
    endDateInput.value = this.value;;
});

document.addEventListener('DOMContentLoaded', function () {
    var startDateInput = document.getElementById("Start").value;
    // 取得當前日期
    var currentDate = new Date(startDateInput);
    var year = currentDate.getFullYear();
    var month = currentDate.getMonth();
    var lastDayOfMonth = new Date(year, month + 1, 0).getDate();

    // 設定最小值為兩個月前
    document.getElementById('End').setAttribute('min', year + '-' + ('0' + (month + 1)).slice(-2) + '-01');
    // 設定最大值為今天
    document.getElementById('End').setAttribute('max', year + '-' + ('0' + (month + 1)).slice(-2) + '-' + lastDayOfMonth);
});