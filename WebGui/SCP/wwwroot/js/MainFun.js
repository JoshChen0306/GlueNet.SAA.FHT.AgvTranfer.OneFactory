$(function () {
    var insertdata = {};
    var updatedata = {};
    var deletedata = {};
    var originformdata = {};
    var originalData = null;

    var fieldToColumnIndex = {
        'ColumnNo': 1,
        'RowNo': 2,
        'FunctionNo': 3,
        'FunctionType': 4,
        'FunctionChineseName': 5,
        'FunctionEnglishName': 6,
        'ControlFlag': 7,
        'WebUrl': 8,
        'WebIcon': 9

    };

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
        if (Object.keys(insertdata).length > 0 ||Object.keys(updatedata).length > 0 || Object.keys(deletedata).length > 0) {
            $.ajax({
                type: 'POST',
                url: '/MainFun/DataChange',
                data: JSON.stringify({
                    insertdata: insertdata,
                    updatedata: updatedata,
                    deletedata: deletedata.FunctionNo,
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
                var functionno = row.find('td:eq(3)').text();
                $.each(formData, function (i, item) {
                    formDataObject[item.name] = item.value;
                    // 更新資料列
                    var columnIndex = fieldToColumnIndex[item.name];
                    if (columnIndex !== undefined) {
                        row.find('td').eq(columnIndex).text(item.value);
                    }
                });
                //將更新資料存入陣列
                updatedata[functionno] = formDataObject;
                console.log(updatedata);
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
    $(document).on('click','.delete-btn', function () {
        var form = $(this).closest('tr').next().find('form');
        var row = $(this).closest('tr')

        if (form.is(':visible')) {
            //表單展開時收起表單
            form.parent().collapse('hide');
        }
        else {
            // 抓取資料列資料
            var functionno = row.find('td:eq(3)').text();
            if (!deletedata['FunctionNo']) {
                deletedata['FunctionNo'] = [];
            }
            
            deletedata['FunctionNo'].push(functionno);
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
        var inputs = form.find('input[required]');
        var allValid = true;
        inputs.each(function () {
            if (!this.checkValidity()) {
                allValid = false;
                var label = $('label[for="' + this.name + '"]').text();
                alert('請輸入' + label);
                return false; // 停止遍歷
            }
        });
        if (!allValid) {
            return;
        }

        CreateNewRow(formData, fieldToColumnIndex);
        $.each(formData, function (i, item) {
            formDataObject[item.name] = item.value;
        });
        //將更新資料存入陣列
        insertdata[formDataObject["FunctionNo"]] = formDataObject;
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


function CreateNewRow(formData, fieldToColumnIndex) {
    var FunctionNo = formData.find(item => item.name === 'FunctionNo').value;
    // 獲取行數
    var lastRow = ($('tbody tr').length / 2 + 1);

    var newRow = $(
        '<tr class="text-center">' +
            `<td class="align-middle">${lastRow}</td>` + // #
            '<td class="align-middle"></td>' + // ColumnNo
            '<td class="align-middle"></td>' + // RowNo
            '<td class="align-middle"></td>' + // FunctionNo
            '<td class="align-middle"></td>' + // FunctionType
            '<td class="align-middle"></td>' + // FunctionChineseName
            '<td class="align-middle"></td>' + // FunctionEnglishName
            '<td class="align-middle"></td>' + // ControlFlag
            '<td class="align-middle"></td>' + // WebUrl
            '<td class="align-middle"></td>' + // WebIcon
            '<td class="align-middle">' +
            `<button data-bs-toggle="collapse" data-bs-target="#edit-${FunctionNo}" aria-expanded="false" aria-controls="${FunctionNo}" class="btn edit-btn p-0">` +
                    '<i class="fa-solid fa-square-pen fa-xl" style="color: #40B1FF;"></i>' +
                '</button> ' +  
            `<button class="btn delete-btn p-0" id="delete-${FunctionNo}"><i class="fa-solid fa-square-minus fa-xl" style="color: #E25E3E;"></i></button>` +
            '</td>' +
        '</tr>' +
        '<tr>' +
            '<td colspan="10" class="border-0 p-0">' +
                `<div id="edit-${FunctionNo}" class="collapse ps-3">` +
                    '<form>' +
                        '<div class="row row-cols-3 gy-3 mb-3 m-0">' +
                            '<div class="col d-flex align-items-center">' +
                                '<label class="me-3">主項編號</label>' +
                                '<input class="form-control p-1 w-50" name="ColumnNo" />' +
                            '</div>' +
                            '<div class="col d-flex align-items-center" >' +
                                '<label class= "me-5" > 次項編號</label >' +
                                '<input class= "form-control p-1 w-50" name = "RowNo" autocomplete = "off"  />' +
                            '</div>' +
                            '<div class="col d-flex align-items-center" >' +
                                '<label class="me-5" > 功能編號</label > ' +
                                '<input class="form-control p-1 w-50" name = "FunctionNo" autocomplete = "off" />'+
                            '</div>' +
                            '<div class="col d-flex align-items-center" > '+
                                '<label class="me-3" > 功能類型</label > '+
                                '<input class="form-control p-1 w-50" name = "FunctionType" autocomplete = "off"/>'+
                            '</div>'+
                            '<div class="col d-flex align-items-center" > '+
                                '<label class="me-3" > 功能中文名稱</label > '+
                                '<input class="form-control p-1 w-50" name = "FunctionChineseName" autocomplete = "off"  />'+
                            '</div>'+
                            '<div class="col d-flex align-items-center" > '+
                                '<label class="me-3" > 功能英文名稱</label > '+
                                '<input class="form-control p-1 w-50" name = "FunctionEnglishName" autocomplete = "off" />'+
                            '</div>'+
                            '<div class="col d-flex align-items-center" > '+
                                '<label class="me-3" > 控制旗標</label > '+
                                '<input class="form-control p-1 w-50" name = "ControlFlag" autocomplete = "off" />'+
                            '</div>'+
                            '<div class="col d-flex align-items-center" > '+
                                '<label class="me-5" > 頁面網址</label > '+
                                '<input class="form-control p-1 w-50" name = "WebUrl" autocomplete = "off" />'+
                            '</div>'+
                            '<div class="col d-flex align-items-center" > '+
                                '<label class="me-5" > 頁面圖示</label > '+
                                '<input class="form-control p-1 w-50" name = "WebIcon" autocomplete = "off" />'+
                            '</div>'+
                        '</div>' +
                    '</form>' +
                '</div>' +
            '</td>' +
        '</tr>'
    );

    // 將表單數據插入到新的表格行中
    $.each(formData, function (i, item) {
        var columnIndex = fieldToColumnIndex[item.name];
        if (columnIndex !== undefined) {
            newRow.find('td.align-middle').eq(columnIndex).text(item.value);
            newRow.find(`input[name="${item.name}"]`).val(item.value);
        }
    });

    // 將新的 <tr> 元素添加到表格的最後
    $('#maintain-table tbody').append(newRow);
}
