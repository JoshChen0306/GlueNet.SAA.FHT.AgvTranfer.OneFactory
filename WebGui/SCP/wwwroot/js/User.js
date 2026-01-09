$(function () {
    var insertdata = {};
    var updatedata = {};
    var deletedata = {};
    var originformdata = {};
    var originalData = null;
    var groupname = {};
    var fieldToColumnIndex = {
        'UserId': 1,
        'UserName': 2,
        'GroupId': 3,
        'Mail': 4,
        'Tel': 5,
    };
    //讀取下拉選單中的值對應顯示文字
    $('#GroupId').first().find('option').each(function () {
        groupname[$(this).val()] = $(this).text();
    });
    //$('#shift-table').dataTable({
    //});
    $(".edit-btn,.delete-btn,#confirm-btn,#cancel-btn").css("visibility", "hidden");

    //維護按鈕點擊事件
    $("#maintain-btn").on("click", function () {
        // 獲取整個表格的HTML內容
        originalData = $('#maintain-table').clone(true);

        $(".edit-btn,.delete-btn").css("visibility", function (i, visibility) {
            if (visibility === "hidden") {
                $(this).css("visibility", "visible").hide().fadeIn(300);
            }
            else {
                $(this).fadeOut(300, function () {
                    $(this).css("visibility", "hidden").show();
                });
            }
        })
        $('#maintain-save ,#maintain-cancel,#maintain-btn,#insert-btn').toggleClass('d-none');
    });

    //儲存按鈕點擊事件
    $('#maintain-save').on('click', function () {
        if (Object.keys(insertdata).length > 0 || Object.keys(updatedata).length > 0 || Object.keys(deletedata).length > 0) {
            $.ajax({
                type: 'POST',
                url: '/User/DataChange',
                data: JSON.stringify({
                    insertdata: insertdata,
                    updatedata: updatedata,
                    deletedata: deletedata.userId,
                }),
                contentType: 'application/json',
                success: function (response) {
                    insertdata = {};
                    updatedata = {};
                    deletedata = {};
                },
                error: function (jqXHR, textStatus, errorThrown) {
                    // 處理錯誤
                    console.error("AJAX 請求失敗: ", textStatus, errorThrown);
                }
            });
        }

        $('#maintain-save ,#maintain-cancel,#maintain-btn ,#insert-btn').toggleClass('d-none');
        $(".edit-btn,.delete-btn").fadeOut(300, function () {
            $(this).css("visibility", "hidden").show();
        });
    });

    //取消按鈕點擊事件
    $('#maintain-cancel').on('click', function () {
        insertdata = {};
        updatedata = {};
        deletedata = {};
        if (originalData) {
            // 將原始資料重新設定回表格
            setTimeout(function () {
                $('#maintain-table').replaceWith(originalData);
                originalData = null;
            }, 150);  // 延遲150豪秒後執行
        }
        $('#maintain-save ,#maintain-cancel,#maintain-btn').toggleClass('d-none');
        $(".edit-btn,.delete-btn").fadeOut(300, function () {
            $(this).css("visibility", "hidden").show();
        });
    });

    //編輯按鈕點擊事件
    $(document).on('click', '.edit-btn', function () {
        // 找到當前按鈕所在的表單
        // $(this) 是當前被點擊的按鈕
        // .closest('tr') 找到最近的父級 <tr> 元素（即當前按鈕所在的資料列）
        // .next() 找到下一個同級元素（即包含表單的資料列）
        // .find('form') 在該元素中找到 <form> 元素
        var form = $(this).closest('tr').next().find('form');
        var icon = $(this).find('i');
        var row = $(this).closest('tr')
        if ($(this).hasClass("collapsed")) {
            //修改按鈕icon圖示   
            //抓取input資料
            var formData = form.serialize();
            if (originformdata !== formData) {
                var formData = form.serializeArray();
                var formDataObject = {};
                $.each(formData, function (i, item) {
                    formDataObject[item.name] = item.value;
                    // 更新資料列
                    var columnIndex = fieldToColumnIndex[item.name];
                    if (columnIndex !== undefined) {
                        if (item.name === "GroupId") {
                            row.find('td').eq(columnIndex).text(groupname[item.value]);
                        }
                        else {
                            row.find('td').eq(columnIndex).text(item.value);
                        }
                    }
                });
                //將更新資料存入陣列
                updatedata[formDataObject["UserId"]] = formDataObject;
            }
        } else {
            icon.removeClass('fa-square-pen');
            icon.addClass('fa-square-check');
            icon.css('color', '#4CCD99');
            //只顯示該列按鈕
            $(".edit-btn,.delete-btn,tfoot").css("visibility", "hidden");
            $('#maintain-save ,#maintain-cancel').toggleClass('d-none');
            row.find('.edit-btn,.delete-btn').css('visibility', 'visible');
            originformdata = form.serialize();
        }
    });

    //刪除按鈕點擊事件
    $(document).on('click', '.delete-btn', function () {
        var form = $(this).closest('tr').next().find('form');
        var row = $(this).closest('tr')

        if (form.is(':visible')) {
            //表單展開時收起表單
            form.parent().collapse('hide');
        }
        else {
            // 抓取資料列資料
            var userId = row.find('td:eq(1)').text();
            if (!deletedata['userId']) {
                deletedata['userId'] = [];
            }

            deletedata['userId'].push(userId);
            row.hide();
        }

    });

    //新增按鈕點擊事件
    $(document).on('click', '#InsertData', function () {
        var btn = $(this).closest('tr');
        if (!$(this).hasClass("collapsed")) {
            $(".edit-btn,.delete-btn").fadeOut(300, function () {
                $(this).css("visibility", "hidden").show();
            });
            $('#maintain-save ,#maintain-cancel').toggleClass('d-none');
            $("#confirm-btn,#cancel-btn").css("visibility", "visible").hide().fadeIn(300);
            btn.hide();
        }

    });

    //新增確認按鈕點擊事件
    $('#confirm-btn').on('click', function (e) {
        //var form = $(this).closest('tr').find('form');
        var form = $('#InsertForm');
        var formData = form.serializeArray();
        var formDataObject = {};
        if (!form[0].checkValidity()) {
            alert('請輸入員工編號');
            return;
        }

        $.each(formData, function (i, item) {
            formDataObject[item.name] = item.value;
        });

        // 檢查員工編號是否重複
        var newUserId = formDataObject["UserId"];
        var isDuplicate = false;
        $('#maintain-table tbody tr').each(function () {
            // 略過折疊內容列 (colspan)
            if ($(this).find('td').length <= 1) return;

            var existingId = $(this).find('td').eq(1).text().trim();
            if (existingId === newUserId) {
                isDuplicate = true;
                return false; // break loop
            }
        });

        if (isDuplicate) {
            alert('員工編號重複，請使用其他編號');
            return;
        }

        CreateNewRow(formData, groupname, fieldToColumnIndex);

        //將更新資料存入陣列 (修正 key 大小寫問題: userId -> UserId)
        insertdata[formDataObject["UserId"]] = formDataObject;
        form[0].reset();
        $('#insertcollapse').collapse('hide');
    });

    //全部表單收起來時做的動作
    $(document).on('hide.bs.collapse', '.collapse', function () {

        var btn = $(this).closest('tr').prev().find('button');
        var icon = $(this).closest('tr').prev().find('i').first();
        //修改按鈕icon圖示
        if (icon.hasClass('fa-square-check')) {
            icon.removeClass('fa-square-check');
            icon.addClass('fa-square-pen');
            icon.css('color', '#40B1FF');
        }
        $("tfoot tr").show();
        $(".edit-btn,.delete-btn,tfoot").not(btn).css("visibility", "visible").hide().fadeIn(300);
        $('#maintain-save ,#maintain-cancel').toggleClass('d-none');
        $("#confirm-btn,#cancel-btn").fadeOut(300, function () {
            $(this).css("visibility", "hidden").show();
        })
    });
});


function CreateNewRow(formData, groupname, fieldToColumnIndex) {
    var userId = formData.find(item => item.name === 'UserId').value;
    // 獲取行數
    var lastRow = ($('tbody tr').length / 2 + 1);



    var newRow = $(
        '<tr class="text-center">' +
        `<td class="align-middle">${lastRow}</td>` + // #
        '<td class="align-middle"></td>' + // UserId
        '<td class="align-middle"></td>' + // UserName
        '<td class="align-middle"></td>' + // GroupId
        '<td class="align-middle"></td>' + // Mail
        '<td class="align-middle"></td>' + // Tel
        '<td class="align-middle">' +
        `<button data-bs-toggle="collapse" data-bs-target="#edit-${userId}" aria-expanded="false" aria-controls="${userId}" class="btn edit-btn p-0">` +
        '<i class="fa-solid fa-square-pen fa-xl" style="color: #40B1FF;"></i>' +
        '</button> ' +
        `<button class="btn delete-btn p-0" id="delete-${userId}"><i class="fa-solid fa-square-minus fa-xl" style="color: #E25E3E;"></i></button>` +
        '</td>' +
        '</tr>' +
        '<tr>' +
        '<td colspan="6" class="border-0 p-0">' +
        `<div id="edit-${userId}" class="collapse ps-3">` +
        '<form>' +
        '<div class="row row-cols-2 gy-3 m-0">' +
        '<div class="col d-flex align-items-center">' +
        '<label class="me-3">員工編號</label>' +
        '<input class="form-control-plaintext  w-25" name="UserId" readonly value="@item.UserId" />' +
        '</div>' +
        '<div class="col d-flex align-items-center">' +
        '<label class="me-3">人員名稱</label>' +
        '<input class="form-control p-0 w-25" name="UserName" autocomplete="off" value="@item.UserName" />' +
        '</div>' +
        '<div class="col d-flex align-items-center">' +
        '<label class="me-3">群組類別</label>' +
        '<select class="form-select p-0 w-50" name="GroupId" id="GroupId">' +
        '</select>' +
        '</div>' +
        '<div class="col d-flex align-items-center">' +
        '<label class="me-3">電子郵件</label>' +
        '<input class="form-control p-0 w-75" name="Mail" autocomplete="off" value="@item.Mail" />' +
        '</div>' +
        '<div class="col d-flex align-items-center">' +
        '<label class="me-3">連絡電話</label>' +
        '<input class="form-control p-0 w-50" name="Tel" autocomplete="off" value="@item.Tel" />' +
        '</div>' +
        '</div>' +
        '</form>' +
        '</div>' +
        '</td>' +
        '</tr>'
    );

    // 將表單數據插入到新的 <tr> 元素中
    $.each(formData, function (i, item) {
        var columnIndex = fieldToColumnIndex[item.name];
        if (columnIndex !== undefined) {
            newRow.find('td.align-middle').eq(columnIndex).text(item.value);
            newRow.find(`input[name="${item.name}"]`).val(item.value);
        }
        if (item.name === 'GroupId') {
            newRow.find('td.align-middle').eq(columnIndex).text(groupname[item.value]);
            $.each(groupname, function (key, value) {
                var option = $('<option>').attr('value', key).text(value);
                newRow.find('select[name="GroupId"]').append(option);
            });
        }
    });
    // 將新的 <tr> 元素添加到表格的最後
    $('#maintain-table tbody').append(newRow);
}
