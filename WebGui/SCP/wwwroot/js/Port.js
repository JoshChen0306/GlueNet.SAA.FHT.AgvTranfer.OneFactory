$(function () {
    var name;
    $("#portModal").on('show.bs.modal', function (event) {
       
        var button = $(event.relatedTarget)
        name = button.attr("id");
        
        var machinename = button.attr('data-machinename')
        var interfacename = button.attr('data-interfacename')
        var useflag = button.attr('data-useflag')
        var haveflag = button.attr('data-haveflag')
        var workorder = button.attr('data-workorder')

        $("#machinename").val(machinename);
        $("#interfacename").val(interfacename);
        $("#haveflag").val(haveflag);
        $("#workorder").val(workorder);
        $("#useflag").prop('checked', useflag === 'Y');

        if (haveflag === '3') {
            $("#workorder-container").show();
        }
        else {
            $("#workorder-container").hide();
        }

    })

    $("#haveflag").on("change", function () {
        if ($(this).val() === '3') {
            $("#workorder-container").show();
        }
        else {
            $("#workorder-container").hide();
        }
    })

    $("#submit").on("click", function () {
        var img = ['/img/empty.svg', '/img/trac.svg', '','/img/material.svg']
        var formData = $("#portform").serializeArray();
        // 將表單數據轉換為 JSON 格式
        var jsonData = {};
        $.each(formData, function () {
            jsonData[this.name] = this.value;
        });
        jsonData["name"] = name;
        console.log(jsonData);
        $.ajax({
            type: "POST",
            url: "/port/UpdateoPort",
            data: JSON.stringify(jsonData),
            contentType: "application/json",
            success: function (response) {
                // 處理成功響應
                console.log("表單資料已成功送出", response);
                $("#" + name).attr('data-machinename', jsonData["machinename"])
                $("#" + name).attr('data-interfacename', jsonData["interfacename"])
                $("#" + name).attr('data-haveflag', jsonData["haveflag"])
                $("#" + name).attr('data-workorder', jsonData["workorder"])
                $("#" + name).find('img').attr('src', img[jsonData["haveflag"]])
                if (jsonData["useflag"]) {
                    $("#" + name).attr('data-useflag', "Y")
                }
                else {
                    $("#" + name).attr('data-useflag', "N")
                }
                
            },
            error: function (error) {
                // 處理錯誤響應
                console.error("表單資料送出失敗", error);
            }
        })
    })
});