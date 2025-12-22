
import { connection } from './common/hub.js';

export function loadMapData(area) {
    $.ajax({
        type: "GET",
        url: "/api/Common/ShowMap",
        data: { area: area },
        success: function (data) {
            $("#Map").html(data);

            const mapSrc = area || 'FHT2-1F';
            $('#map-img').attr('src', `/img/${mapSrc}.png`);

            // 初始化所有庫位的 Bootstrap Tooltip
            $('[data-bs-toggle="tooltip"]').tooltip({
                container: 'body',
                trigger: 'hover'
            });

            // 為 M/T 區站點綁定物料管理點擊事件（如果函數存在）
            if (typeof window.bindStationLotEvents === 'function') {
                window.bindStationLotEvents();
            }
        },
        error: function (jqXHR, textStatus, errorThrown) {
            console.error("AJAX 請求失敗: ", textStatus, errorThrown);
        }
    });
}

$(function () {

    //$.ajax({
    //    type: "GET",
    //    url: "/api/Common/ShowMap",
    //    success: function (data) {
    //        $("#Map").html(data)
    //    },
    //    error: function (jqXHR, textStatus, errorThrown) {
    //        // 處理錯誤
    //        console.error("AJAX 請求失敗: ", textStatus, errorThrown);
    //    }
    //});

    // 載入初始地圖
    loadMapData();

    connection.on("SendTracChange", function () {
        $.ajax({
            type: "GET",
            url: "/api/Common/UpdateTrac",
            success: function (data) {
                $.each(data, function (index, item) {
                    var tracName = $("#" + item.Name);
                    var img = tracName.find("img");

                    // 決定站點圖示
                    var imgSrc = item.ImgSrc;
                    // V Cut 物料顏色判斷：只有 HaveFlag=3 且有 WorkOrder 時才檢查
                    if (item.HaveFlag === "3" && item.WorkOrder) {
                        if (item.WorkOrder.includes("^VCUT^DONE")) {
                            // 紫色：V Cut 已加工完成
                            imgSrc = "/img/vcut-done.svg";
                        } else if (item.WorkOrder.includes("^VCUT")) {
                            // 橙色：V Cut 待加工
                            imgSrc = "/img/vcut-pending.svg";
                        }
                    }
                    img.attr("src", imgSrc);

                    tracName.attr("data-haveflag", item.HaveFlag);
                    tracName.attr("data-rackid", item.RackId);
                    tracName.attr("data-workorder", item.WorkOrder);
                    tracName.attr("data-reserve", item.Reserve);
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

    // 為所有導航連結添加點擊事件
    $(document).on('click', '.nav-link', function (e) {
        e.preventDefault();
        var area = $(this).data('area');

        if (!area) return;

        $('.nav-link').removeClass('active');
        $(this).addClass('active');

        loadMapData(area);
    });
});