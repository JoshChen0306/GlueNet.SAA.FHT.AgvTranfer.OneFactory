$(function () {
    var updatedata = {};

    //維護按鈕點擊事件
    $("#maintain-btn").on("click", function () {
        // 獲取整個表格的HTML內容
        originalData = $('#maintain-table').clone(true);
        $("input[type='checkbox']").prop("disabled", false);
        $('#maintain-save ,#maintain-cancel,#maintain-btn,#insert-btn').toggleClass('d-none');
    });

    //儲存按鈕點擊事件
    $('#maintain-save').on('click', function () {
        var groupid = $("#GroupName").val();
        updatedata = { [groupid]: [] }

        $("#dtFunction tbody tr input[type='checkbox']:checked").each(function () {
            var functionno = $(this).attr('id');
            updatedata[groupid].push(functionno);
        });
        $.ajax({
            type: "POST",
            url: "/ProgramGroup/Save",//Specify the controller action URL
            data: JSON.stringify(updatedata),
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            success: function (data) {

            },
            error: function (err) {
                console.log(err);
            }
        });

        $("input[type='checkbox']").prop("disabled", true);
        $('#maintain-save ,#maintain-cancel,#maintain-btn ,#insert-btn').toggleClass('d-none');
    });

    //取消按鈕點擊事件
    $('#maintain-cancel').on('click', function () {
        if (originalData) {
            // 將原始資料重新設定回表格
            $('#maintain-table').replaceWith(originalData);
            originalData = null;
  
        }
        $("input[type='checkbox']").prop("disabled", true);
        $('#maintain-save ,#maintain-cancel,#maintain-btn').toggleClass('d-none');
    });

    $('#GroupName').on('change',function () {

        $("#maintain-btn").removeClass("invisible");
        if ($(this).val() === "") {
            $("#dtFunction tbody").empty();
            $("#maintain-btn").addClass("invisible");
        }
        else {
            $.ajax({
                type: "Get",
                url: "/ProgramGroup/ShowpFunction?GroupId=" + $(this).val(),
                success: function (data) {
                    $("#pFunction").html(data);
                },
                error: function (response) {
                    console.log(responseText);
                }
            });
        }
        $('#maintain-save ,#maintain-cancel').addClass('d-none');
        $('#maintain-btn').removeClass('d-none');
    });
});