$(function () {
    var updatedata = {};
    var originalData = null;
    $("#maintain-btn").on("click", function () {
        // 獲取整個表格的HTML內容
        originalData = $('form').clone(true);
        $('input').each(function () {
            if ($(this).attr('name') !== 'UserId') {
                $(this).removeAttr('readonly');
                $(this).removeClass('form-control-plaintext');
                $(this).addClass('form-control');
            }
        });
        $('#maintain-save ,#maintain-cancel,#maintain-btn,#insert-btn').toggleClass('d-none');
    });

    //儲存按鈕點擊事件
    $('#maintain-save').on('click', function () {
        updatedata = $('form').serializeArray().reduce(function (obj, item) {
            obj[item.name] = item.value;
            return obj;
        },{});
       
        if (Object.keys(updatedata).length > 0) {
            $.ajax({
                type: 'POST',
                url: '/Account/DataChange',
                data: JSON.stringify(updatedata),
                contentType: 'application/json',
                success: function (response) {
                    updatedata = {};
                },
                error: function (jqXHR, textStatus, errorThrown) {
                    // 處理錯誤
                    console.error("AJAX 請求失敗: ", textStatus, errorThrown);
                }
            });
        }
        else {
            $('form').replaceWith(originalData);
            originalData = null;
        }

        $('#maintain-save ,#maintain-cancel,#maintain-btn ,#insert-btn').toggleClass('d-none');

    });

    //取消按鈕點擊事件
    $('#maintain-cancel').on('click', function () {
        updatedata = {};
        if (originalData) {
            // 將原始資料重新設定回表格
            $('form').replaceWith(originalData);
            originalData = null;
        }
        $('#maintain-save ,#maintain-cancel,#maintain-btn').toggleClass('d-none');
    });
});