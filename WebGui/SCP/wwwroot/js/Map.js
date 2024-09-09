
import { connection } from './common/hub.js';
$(function () {

    $.ajax({
        type: "GET",
        url: "/api/Common/ShowMap",
        success: function (data) {
            $("#Map").html(data)
        },
        error: function (jqXHR, textStatus, errorThrown) {
            // 處理錯誤
            console.error("AJAX 請求失敗: ", textStatus, errorThrown);
        }
    });

    connection.on("SendTracChange", function () {
        $.ajax({
            type: "GET",
            url: "/api/Common/UpdateTrac",
            success: function (data) {
                $.each(data, function (index, item) {
                    var tracName = $("#" + item.Name);
                    var img = tracName.find("img");
                    img.attr("src", item.ImgSrc);
                    tracName.attr("data-haveflag", item.HaveFlag);
                    tracName.attr("data-rackid", item.RackId);
                    tracName.attr("data-workorder", item.WorkOrder);
                    if (item.Reserve == "Y") {
                        $("#R-" + item.Name).removeClass("d-none");
                    }
                    else {
                        $("#R-" + item.Name).addClass("d-none");
                    }
                });
            },
            error: function (jqXHR, textStatus, errorThrown) {
                // 處理錯誤
                console.error("AJAX 請求失敗: ", textStatus, errorThrown);
            }
        });
    });

    connection.on("SendAgvChange", function () {
        $.ajax({
            type: "GET",
            url: "/api/Common/UpdateAgv",
            success: function (data) {
                $.each(data, function (index, item) {
                    var $agv = $("#" + item.GustomerName);
                    $agv.css({
                        left: item.PosX,
                        bottom: item.PosY
                    });
                });
            },
            error: function (jqXHR, textStatus, errorThrown) {
                // 處理錯誤
                console.error("AJAX 請求失敗: ", textStatus, errorThrown);
            }
        });
    });

    //$("<style>")
    //    .prop("type", "text/css")
    //    .html("\
    //.smooth-transition {\
    //    transition: left 1.5s linear, bottom 1.5s linear;\
    //}")
    //    .appendTo("head");


    connection.start().then(function () {
        console.log("連線成功")
        }).catch(function (err) {
            return console.error(err);
        });
    window.addEventListener("beforeunload", function () {
        console.log("關閉連線")
        connection.stop();
    });
});