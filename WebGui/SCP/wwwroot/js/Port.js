$(function () {
    var name;
    $("#portModal").on('show.bs.modal', function (event) {
       
        var button = $(event.relatedTarget)
        name = button.attr("id");
        
        var machinename = button.attr('data-machinename')
        var interfacename = button.attr('data-interfacename')
        var useflag = button.attr('data-useflag')

        $("#machinename").val(machinename);
        $("#interfacename").val(interfacename);
        $("#useflag").prop('checked', useflag === 'Y');
    })

    $("#submit").on("click", function () {
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