import { connection } from './common/hub.js';

console.log("=== Dispatch.js 已載入 ===");
console.log("floorAreaMap from window:", window.floorAreaMap);
// 本地 loadMapData 函數 (避免跨模組 import 問題)
function loadMapDataLocal(area) {
    $.ajax({
        type: "GET",
        url: "/api/Common/ShowMap",
        data: { area: area },
        success: function (data) {
            $("#Map").html(data);
            const mapSrc = area || 'FHT2-1F';
            $('#map-img').attr('src', `/img/${mapSrc}.png`);
        },
        error: function (jqXHR, textStatus, errorThrown) {
            console.error("地圖載入失敗: ", textStatus, errorThrown);
        }
    });
}

// 樓層與地圖區域對應
var floorToMapArea = {
    "1F": "FHT2-1F",
    "2F": "FHT2-2F",
    "3F": "FHT2-3F",
    "4F": "FHT2-4F"
};

// 全域變數：站點資料快取
var stationCache = {};
var isCacheLoaded = false;

$(function () {
    var form = $('#DispatchForm');
    var beginSations = [];
    var rowData = {};
    var btnName = "";
    UpdateDispatch();
    $(".Site").prop("disabled", true);

    // 選擇樓層後篩選 Area 選項並切換地圖
    $("#Floor").on("change", function () {
        var selectedFloor = $(this).val();
        var allowedAreas = window.floorAreaMap ? window.floorAreaMap[selectedFloor] || [] : [];

        console.log("選擇樓層:", selectedFloor);
        console.log("允許的區域:", allowedAreas);
        console.log("floorAreaMap:", window.floorAreaMap);

        // 1. 切換地圖
        if (floorToMapArea[selectedFloor]) {
            loadMapDataLocal(floorToMapArea[selectedFloor]);
        }

        // 2. 重置 Area 和後續選項
        $("#Area").val('');
        $("#BeginStation").val('');
        $("#EndStation").val('');
        $(".Site").prop("disabled", true);

        // 3. 顯示/隱藏符合樓層的 Area 選項（已經過權限篩選）
        var visibleCount = 0;
        $("#Area option").each(function () {
            var areaValue = $(this).val();
            if (areaValue === "" || allowedAreas.includes(areaValue)) {
                $(this).show();
                visibleCount++;
            } else {
                $(this).hide();
            }
        });
        console.log("可見的選項數:", visibleCount);

        // 4. 啟用 Area 選擇 (使用 removeAttr 強制移除 disabled)
        $("#Area").removeAttr("disabled");
        console.log("Area disabled 狀態:", $("#Area").prop("disabled"));
    });

    //選擇派送區域選擇完後得事件
    $("#Area").on("change", function () {
        $("#BeginStation").prop("disabled", false);
        $.ajax({
            type: "GET",
            url: "/Dispatch/GetoNeed",
            success: function (data) {
                beginSations = data.map(item => item.objStation);
            },
            error: function (jqXHR, textStatus, errorThrown) {
                console.error("AJAX 請求失敗: ", textStatus, errorThrown);
            }
        });
        // 重置第二個選項的選擇
        $('#BeginStation').val('');
        $('#EndStation').val('');
        if ($(this).val() == "C") {
            $('#ChangeButton').show();
            $('#RejectdButton').show();
        } else {
            $('#ChangeButton').hide();
            $('#RejectdButton').hide();
        }
    });

    //點選派送起點，展開下拉時就會觸發的事件
    $("#BeginStation").on("focus", function () {
        var selectedValue = $("#Area").val();

        if (!selectedValue) {
            alert('請先選擇派送區域');
            return;
        }

        // 如果已有快取，直接使用
        if (isCacheLoaded && Object.keys(stationCache).length > 0) {
            $('#BeginStation option').hide();
            filterBeginStationOptions(selectedValue);
            return;
        }

        console.log('=== 開始載入站點資料 ===');

        // 顯示 Loading 遮罩
        $('#beginStationLoading').show();
        $('#BeginStation').prop('disabled', true);
        $('#BeginStation option').hide();

        $.ajax({
            type: "GET",
            url: "/Dispatch/GetAllStations",
            dataType: "json",
            success: function (stations) {
                console.log('✅ API 成功回傳 ' + stations.length + ' 筆資料');

                // 隱藏 Loading
                $('#beginStationLoading').hide();
                $('#BeginStation').prop('disabled', false);

                if (stations.length === 0) {
                    alert('沒有可用的站點資料');
                    $('#BeginStation option').show();
                    return;
                }

                // 更新快取
                stationCache = {};
                stations.forEach(function (s) {
                    var stationNo = s.stationNo || s.StationNo;
                    stationCache[stationNo] = {
                        haveFlag: s.haveFlag || s.HaveFlag,
                        reserve: s.reserve || s.Reserve || "N",
                        workOrder: s.workOrder || s.WorkOrder,
                        rackId: s.rackId || s.RackId,
                        putTime: s.putTime || s.PutTime,
                        block: s.block || s.Block,
                        machineName: s.machineName || s.MachineName
                    };
                });

                isCacheLoaded = true;
                console.log('快取已更新:', Object.keys(stationCache).length, '個站點');

                filterBeginStationOptions(selectedValue);
            },
            error: function (jqXHR, textStatus, errorThrown) {
                $('#beginStationLoading').hide();
                $('#BeginStation').prop('disabled', false);
                $('#BeginStation option').show();

                console.error('❌ AJAX 失敗:', textStatus);
                alert('無法取得站點資料');
            }
        });
    });

    //選擇完派送起點的值後觸發的事件
    $("#BeginStation").on("change", function () {
        var area = $("#Area").val();
        var selectedValue = $(this).val();
        $('#WorkOrder').val('');

        // 從快取取得選中站點的資料
        var selectedStation = stationCache[selectedValue];

        switch (area) {
            case "A":
                autoSelectEndStation("B", "0", "N");
                break;
            case "C":
                $('#EndStation').val('');
                if (selectedStation && selectedStation.workOrder) {
                    $('#WorkOrder').val(selectedStation.workOrder);
                }
                break;
            case "D":
                autoSelectEndStation("E", "0", "N");
                break;
            case "J":
                autoSelectEndStation("EE", "0", "N");
                break;
            case "EE":
                autoSelectEndStation("J", "0", "N");
                break;
            case "H":
                autoSelectEndStation("K", "0", "N");  // H區（2F成型後）→ K區（4F烘烤前入貨區）
                break;
            case "L":
                autoSelectEndStation("I", "0", "N");  // L區（4F出貨區）→ I區（3F品檢區）
                break;
            case "M":
                // 雷雕區可選: O(左上料), P(右上料), T(V cut)
                break;
            case "T":
                // V cut區可選: O(左上料), P(右上料)
                break;
            case "Q":
                autoSelectEndStation("S", "0", "N");  // 出料區 → 清洗區
                break;
            case "R":
                autoSelectEndStation("N", "0", "N");  // 廢料區 → 廢料回收區
                break;
            case "E":
                break;
        }

        if (area == "C" || area == "H" || area == "M" || area == "T") {
            $("#EndStation").prop("disabled", false);
            $("#WorkOrder").prop("disabled", true);
        } else {
            $("#EndStation").prop("disabled", true);
            $("#WorkOrder").prop("disabled", false);
        }

        // 從快取讀取 RackId
        if (selectedStation && selectedStation.rackId) {
            $('#RackId').val(selectedStation.rackId);
        }
    });

    //點選派送終點展開下拉選單時觸發的事件
    $("#EndStation").on("focus", function () {
        var selectedValue = $('#BeginStation').val();
        $('#EndStation option').hide();

        if (!selectedValue) {
            $('#EndStation option').show();
            return;
        }

        var firstChar = selectedValue.substring(0, 1);

        switch (firstChar) {
            case "A":
                filterEndStationOptions("B", "0");
                break;
            case "B":
                filterEndStationOptions("C", null);
                break;
            case "D":
                filterEndStationOptions("E", "0");
                break;
            case "H":
                filterEndStationOptions("K", "0");
                break;
            case "L":
                filterEndStationOptions("I", "0");
                break;
            case "M":
                // 顯示 O, P, T 區空架
                filterEndStationOptions("O", "0");
                filterEndStationOptions("P", "0");
                filterEndStationOptions("T", "0");
                break;
            case "T":
                // 顯示 O, P 區空架
                filterEndStationOptions("O", "0");
                filterEndStationOptions("P", "0");
                break;
            case "Q":
                filterEndStationOptions("S", "0");
                break;
            case "R":
                filterEndStationOptions("N", "0");
                break;
            default:
                $('#EndStation option').show();
                break;
        }
    });

    //點選工單欄位後會全選
    $('#WorkOrder').on('click', function () {
        $(this).select();
    });

    // 當 WorkOrder 輸入框失去焦點時觸發事件
    $('#WorkOrder').on('blur', function () {
        var inputValue = $(this).val();
        var area = $("#Area").val();
        var InterfaceName = $(`[data-name='${inputValue}']`);

        if (InterfaceName.length > 0 && area === "D") {
            var workOrderData = InterfaceName.attr('data-workorder');
            $(this).val(workOrderData);
        }
    });

    //點擊確認檢查各項欄位輸出是否有問題
    $(".ConfirmButton").on("click", function (event) {
        var inputs = form.find('select[required]');
        var allValid = true;
        var area = $("#Area").val();
        var beginStation = $("#BeginStation").val();
        var endStation = $("#EndStation").val();
        var modal = "";
        btnName = event.target.id;

        inputs.each(function () {
            if (!this.checkValidity()) {
                alert('請選擇派送站點');
                allValid = false;
                return false;
            }
        });

        if (allValid) {
            if (beginStation && (beginStation.substring(0, 1) === 'A' || beginStation.substring(0, 1) === 'J' || beginStation.substring(0, 1) === 'H' || beginStation.substring(0, 1) === 'L')) {
                var workOrder = $("#WorkOrder").val();
                if (!workOrder) {
                    alert('請輸入工單');
                    allValid = false;
                    return false;
                }
            }

            // 從快取驗證站點狀態
            var beginStationData = stationCache[beginStation];
            var endStationData = stationCache[endStation];

            if (beginStationData && beginStationData.haveFlag === "0") {
                alert('派送起點為空貨架，請重新選擇站點');
                allValid = false;
            } else if (endStationData && endStationData.haveFlag !== "0" && area !== "C") {
                alert('派送終點已有貨架，請重新選擇站點');
                allValid = false;
            }
        }

        if (!allValid) {
            return;
        }

        // 手動顯示 modal
        if (btnName == "ConfirmButton") {
            modal = $("#dispatchModalToggle");
        } else {
            modal = $("#ReLoginModalToggle");
            $("#userId").val("");
            $("#password").val("");
        }
        var myModal = new bootstrap.Modal(modal, {
            keyboard: false
        });
        myModal.show();
    });

    $("#reLoginButton").on("click", function () {
        var userId = $("#userId").val();
        var password = $("#password").val();
        $.ajax({
            type: "POST",
            url: "/Dispatch/ReLogin",
            contentType: "application/json",
            data: JSON.stringify({ userId, password }),
            success: function (response) {
                var modal = ""
                if (btnName == "RejectdButton") {
                    modal = $("#RejectModalToggle")
                } else {
                    modal = $("#dispatchModalToggle")
                }

                var myModal = bootstrap.Modal.getOrCreateInstance(modal, {
                    keyboard: false
                });
                myModal.show();
            },
            error: function (error) {
                alert("帳號密碼錯誤或權限不足")
                $("#ReLoginModalToggle").modal("hide");
            }
        });

    })

    //下料完成按鈕事件
    $("#UnloadButton").on("click", function () {
        var inputs = form.find('select[required]');
        var allValid = true;
        var area = $("#Area").val();
        var beginStation = $("#BeginStation").val();
        var endStation = $("#EndStation").val();

        if (area !== "F") {
            alert("下料區才可做下料完成!!")
            allValid = false;
        } else if (!beginStation) {
            alert("請輸入下料起點")
            allValid = false;
        }

        if (!allValid) {
            return;
        }

        // 手動顯示 modal
        var myModal = new bootstrap.Modal($('#unloadModalToggle'), {
            keyboard: false
        });
        myModal.show();
    });

    //點擊派車發送給後端派車資訊
    $(".SubmitButton").on("click", function () {
        var area = $("#Area").val();
        var status = $("#Status").val();
        // 暫時啟用被禁用的元素
        $("#EndStation").prop("disabled", false);
        $("#WorkOrder").prop("disabled", false);
        // 獲取表單資料
        var formData = form.serializeArray();
        // 將表單數據轉換為 JSON 格式
        var jsonData = {};
        $.each(formData, function () {
            jsonData[this.name] = this.value;
        });
        jsonData["btnName"] = btnName;
        jsonData["Status"] = status;
        // 恢復被禁用的元素
        $("#EndStation").prop("disabled", true);
        $("#WorkOrder").prop("disabled", true);

        var url = area === "F" ? "/Dispatch/UpdateoPort" : "/Dispatch/InsertoNeed";
        // 使用 AJAX 發送表單資料到後端
        $.ajax({
            type: "POST",
            url: url, // 替換為你的後端 URL
            data: JSON.stringify(jsonData),
            contentType: "application/json",
            success: function (response) {
                // 處理成功響應
                console.log("表單資料已成功送出", response);
                form[0].reset();
                var myModal = bootstrap.Modal.getOrCreateInstance($('#dispatchModalToggle2'), {
                    keyboard: false
                });
                myModal.show();
            },
            error: function (error) {
                // 處理錯誤響應
                console.error("表單資料送出失敗", error);
                if (error.responseJSON.message) { alert("派送失敗:" + error.responseJSON.message) }

            }
        });
    });

    $(document).on("click", ".ConfirmCancle", function () {
        rowData["index"] = $(this).closest("tr").index();

    })
    $("#CancleButton").on("click", function () {

        var $row = $("#DispatchStatus").find("tr").eq(rowData["index"])
        var status = $row.find("td:eq(4)").text();
        //if (status === "執行中") {
        //    alert("任務已執行。");
        //    // 關閉 Modal 視窗
        //    $('#cancleModal').modal('hide');
        //    return;
        //}

        var data = {}
        data["beginStation"] = $row.find("td:eq(1)").text();
        data["endStation"] = $row.find("td:eq(2)").text();

        $.ajax({
            type: "POST",
            url: "/Dispatch/DeleteoNeed",
            data: JSON.stringify(data),
            contentType: "application/json",
            success: function (response) {
                // 處理成功響應
                console.log("表單資料已成功送出", response);
                $('#cancleModal').modal('hide');
            },
            error: function (error) {
                // 處理錯誤響應
                console.error("表單資料送出失敗", error);
            }
        });

    })

    connection.on("SendDispatchChange", function () {
        UpdateDispatch();
        // 清除快取，強制重新載入
        stationCache = {};
        isCacheLoaded = false;
    });

    // 啟動相機按鈕
    $("#barcode-scan-btn").on("click", function () {
        console.log("啟動相機按鈕")
        startBarcodeScanner()
    })

    // 停止掃描按鈕
    $("#barcode-scan-stop-btn").on("click", function () {
        console.log("停止掃描按鈕")
        stopScanner()
    })

    // 使用條碼按鈕
    $("#barcode-scan-use-code-btn").on("click", function () {
        console.log("使用條碼按鈕")
        useScannedCode()
    })
});

/**
 * 更新站點資料快取
 */
function updateStationCache(stations) {
    stationCache = {};
    stations.forEach(function (s) {
        stationCache[s.stationNo] = {
            haveFlag: s.haveFlag,
            reserve: s.reserve,
            workOrder: s.workOrder,
            rackId: s.rackId,
            putTime: s.putTime,
            block: s.block,
            machineName: s.machineName
        };
    });
    isCacheLoaded = true;
    console.log('站點資料已更新:', Object.keys(stationCache).length, '個站點');
}

/**
 * 篩選起點選項
 */
function filterBeginStationOptions(selectedValue) {
    var workoderMap = new Map();

    switch (selectedValue.substring(0, 1)) {
        case "C":
            // C 區：只顯示 B 區且 HaveFlag = 3 的站點
            $('#BeginStation option').filter(function () {
                var tracname = $(this).val();
                if (!tracname) return false;

                var station = stationCache[tracname];
                if (!station) return false;

                if (tracname.charAt(0) !== "B" || station.haveFlag != 3) return false;

                var haveflag = station.haveFlag;
                var workorder = (station.workOrder && station.workOrder.split("^")[3]) || "undefined";
                var lot = (station.workOrder && station.workOrder.split("^")[2]) || "undefined";
                var puttime = station.putTime;

                if (tracname.startsWith("B") && haveflag === "3") {
                    if (!workoderMap.has(workorder) || puttime < workoderMap.get(workorder).puttime) {
                        workoderMap.set(workorder, { tracname, lot, puttime });
                    }
                    return true;
                }
                return false;
            }).each(function () {
                var tracname = $(this).val();
                var station = stationCache[tracname];
                if (!station) return;

                var workorder = (station.workOrder && station.workOrder.split("^")[3]) || "undefined";
                if (workoderMap.has(workorder) && workoderMap.get(workorder).tracname === tracname) {
                    var lot = workoderMap.get(workorder).lot;
                    $(this).text(`${tracname}-${workorder}-${lot}`);
                    $(this).show();
                } else {
                    $(this).hide();
                }
            });
            break;
        case "H":  // <--- 新增 H 區起點邏輯
            $('#BeginStation option').filter(function () {
                var tracname = $(this).val();
                if (!tracname) return false;

                var station = stationCache[tracname];
                if (!station) return false;

                // 條件：站點開頭為 H 且 HaveFlag 為 1
                return tracname.startsWith("H") && station.haveFlag === "1";
            }).show();
            break;

        default:
            // 其他區：顯示符合區域且 HaveFlag 不為 0 的站點
            $('#BeginStation option').filter(function () {
                var tracname = $(this).val();
                if (!tracname) return false;

                var station = stationCache[tracname];
                if (!station) return false;

                return tracname.startsWith(selectedValue) && station.haveFlag !== "0";
            }).show();
            break;
    }
}

/**
 * 自動選擇終點站（統一處理邏輯）
 */
function autoSelectEndStation(prefix, requiredHaveFlag, requiredReserve) {
    var found = false;
    $('#EndStation option').each(function () {
        if (found) return false; // 已找到就跳出

        var tracname = $(this).val();
        if (!tracname || !tracname.startsWith(prefix)) return true;

        var station = stationCache[tracname];
        if (!station) return true;

        var haveFlagMatch = !requiredHaveFlag || station.haveFlag === requiredHaveFlag;
        var reserveMatch = !requiredReserve || station.reserve === requiredReserve;

        if (haveFlagMatch && reserveMatch) {
            $('#EndStation').val(tracname);
            found = true;
            return false;
        }
    });

    if (!found) {
        console.warn(`找不到符合條件的 ${prefix} 區站點`);
    }
}

/**
 * 篩選終點選項（用於 EndStation focus 事件）
 */
function filterEndStationOptions(prefix, requiredHaveFlag) {
    $('#EndStation option').filter(function () {
        var tracname = $(this).val();
        if (!tracname || !tracname.startsWith(prefix)) return false;

        if (!requiredHaveFlag) return true; // 不需檢查 HaveFlag

        var station = stationCache[tracname];
        if (!station) return false;

        return station.haveFlag === requiredHaveFlag;
    }).show();
}

//即時更新右側任務列表
function UpdateDispatch() {
    $.ajax({
        type: "GET",
        url: "/Dispatch/UpdateDispatch",
        success: function (data) {
            $('#DispatchStatus').html(data);
        },
        error: function (jqXHR, textStatus, errorThrown) {
            console.error("AJAX 請求失敗: ", textStatus, errorThrown);
        }
    });
}

// 條碼掃描相關的 JavaScript 代碼

// 使用 html5-qrcode 的條碼掃描函數
async function startBarcodeScanner() {
    console.log('開始啟動 html5-qrcode 掃描器');

    try {
        // 檢查函式庫是否載入
        if (typeof Html5QrcodeScanner === 'undefined') {
            alert('條碼掃描函式庫未載入，請重新整理頁面');
            return;
        }

        showScannerModal();

        // 等待模態視窗完全顯示後再初始化掃描器
        setTimeout(() => {
            initHtml5QrcodeScanner();
        }, 500);

    } catch (error) {
        console.error('條碼掃描器啟動失敗:', error);
        alert('啟動失敗: ' + error.message);
    }
}

function showScannerModal() {
    console.log('顯示掃描器模態視窗');
    const modalElement = document.getElementById('barcodeModal');
    if (!modalElement) {
        alert('找不到條碼掃描模態視窗');
        return;
    }
    const modal = new bootstrap.Modal(modalElement);
    modal.show();
}

function initHtml5QrcodeScanner() {
    const scannerDiv = document.getElementById('qr-reader');
    if (!scannerDiv) {
        alert('找不到掃描器容器');
        return;
    }

    // html5-qrcode 配置
    const config = {
        fps: 10,    // 每秒掃描次數
        qrbox: {    // 掃描框大小
            width: 250,
            height: 250
        },
        showTorchButtonIfSupported: true,
        // 相機配置
        aspectRatio: 1.0,
        disableFlip: false
    };

    // 建立掃描器實例
    const html5QrcodeScanner = new Html5QrcodeScanner(
        "qr-reader",
        config,
        false // verbose
    );

    // 掃描成功回調
    function onScanSuccess(decodedText, decodedResult) {
        console.log('掃描成功:', decodedText);
        console.log('掃描結果詳情:', decodedResult);

        // 停止掃描器
        html5QrcodeScanner.clear().then(() => {
            console.log('掃描器已清理');
        }).catch(error => {
            console.error('清理掃描器時發生錯誤:', error);
        });

        // 填入工單欄位
        const workOrderInput = document.getElementById('WorkOrder');
        if (workOrderInput) {
            workOrderInput.value = decodedText;
            $(workOrderInput).trigger('blur');
        }

        // 顯示成功訊息
        showScanResult(decodedText, decodedResult.result.format?.formatName || '未知格式');

        // 延遲關閉模態視窗
        setTimeout(() => {
            stopScanner();
        }, 2000);
    }

    // 掃描錯誤回調（可選）
    function onScanFailure(error) {
        // 這是正常的，不需要處理每個掃描失敗
        // console.log('掃描失敗:', error);
    }

    // 開始渲染掃描器
    html5QrcodeScanner.render(onScanSuccess, onScanFailure);

    // 存儲掃描器實例以便後續操作
    window.currentScanner = html5QrcodeScanner;

    console.log('html5-qrcode 掃描器已啟動');
}

function showScanResult(code, format) {
    const modalBody = document.querySelector('#barcodeModal .modal-body');
    if (modalBody) {
        // 移除之前的結果
        const existingResult = modalBody.querySelector('.scan-result');
        if (existingResult) {
            existingResult.remove();
        }

        const resultDiv = document.createElement('div');
        resultDiv.className = 'alert alert-success mt-3 scan-result';
        resultDiv.innerHTML = `
                <h5><i class="fa-solid fa-check-circle"></i> 掃描成功！</h5>
                <p><strong>內容：</strong>${code}</p>
                <p><strong>格式：</strong>${format}</p>
            `;
        modalBody.appendChild(resultDiv);
    }
}

// 停止掃描器並清理資源
function stopScanner() {
    console.log('停止條碼掃描器');

    // 清理 html5-qrcode 掃描器
    if (window.currentScanner) {
        window.currentScanner.clear().then(() => {
            console.log('掃描器已成功清理');
            window.currentScanner = null;
        }).catch(error => {
            console.error('清理掃描器時發生錯誤:', error);
            window.currentScanner = null;
        });
    }

    // 隱藏模態視窗
    const modal = bootstrap.Modal.getInstance(document.getElementById('barcodeModal'));
    if (modal) {
        modal.hide();
    }

    // 清理模態視窗內容
    setTimeout(() => {
        const modalBody = document.querySelector('#barcodeModal .modal-body');
        if (modalBody) {
            const scanResult = modalBody.querySelector('.scan-result');
            if (scanResult) {
                scanResult.remove();
            }
        }
    }, 500);
}