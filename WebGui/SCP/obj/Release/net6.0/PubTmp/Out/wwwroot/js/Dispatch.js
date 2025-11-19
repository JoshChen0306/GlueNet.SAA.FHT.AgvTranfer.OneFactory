import { connection } from './common/hub.js';
$(function () {
    var form = $('#DispatchForm');
    var beginSations = [];
    var rowData = {};
    var btnName = "";
    UpdateDispatch();
    $(".Site").prop("disabled", true);

    //選擇派送區域選擇完後得事件
    $("#Area").on("change", function () {
        $("#BeginStation").prop("disabled", false);
        $.ajax({
            type: "GET",
            url: "/Dispatch/GetoNeed",
            success: function (data) {
                beginSations = data.map(item => item.ObjStation);
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
        var workoderMap = new Map();
        // 獲取第一個選項的選擇值
        var selectedValue = $("#Area").val();
        // 隱藏所有第二個選項中的 <option>
        $('#BeginStation option').hide();

        switch (selectedValue.substring(0, 1)) {
            case "C":
                $('#BeginStation option').filter(function () {
                    var tracname = $(this).val();
                    if (!tracname) return false; // 排除空值
                    if (tracname.charAt(0) !== "B" || $(`#${tracname}`).attr("data-haveflag") !=3) return false
                    var haveflag = $(`#${tracname}`).attr("data-haveflag")
                    var lot = ($(`#${tracname}`).attr("data-workorder") && $(`#${tracname}`).attr("data-workorder").split("^")[2])||"undefined";
                    var workorder = ($(`#${tracname}`).attr("data-workorder") && $(`#${tracname}`).attr("data-workorder").split("^")[3]) ||"undefined";
                    var puttime = $(`#${tracname}`).attr("data-puttime")

                    if (tracname.startsWith("B") && haveflag === "3") {
                        if (!workoderMap.has(workorder) || puttime < workoderMap.get(workorder).puttime) {
                            workoderMap.set(workorder, { tracname, lot, puttime })
                        }
                        return true;
                    }
                    return false;
                }).each(function () {
                    var tracname = $(this).val();
                    var workorder = ($(`#${tracname}`).attr("data-workorder") && $(`#${tracname}`).attr("data-workorder").split("^")[3]) || "undefined";
                    if (workoderMap.has(workorder) && workoderMap.get(workorder).tracname === tracname) {
                        var lot = workoderMap.get(workorder).lot;
                        var beginStation = $(`#${tracname}`).attr("id");
                        $(this).text(`${beginStation}-${workorder}-${lot}`);
                        $(this).show()
                    } else {
                        $(this).hide()
                    }                   
                })
                break;
            default:
                // 使用 filter 方法來顯示所有與第一個選項相關的 <option>
                $('#BeginStation option').filter(function () {

                    // 檢查 <option> 的 value 是否以第一個選項的選擇值開頭 
                    var tracname = $(this).val();
                    return $(this).val().startsWith(selectedValue) && $(`#${tracname}`).attr("data-haveflag") !== "0";
                }).show();
                break;
        }       
    });

    //選擇完派送起點的值後觸發的事件
    $("#BeginStation").on("change", function () {
        var area = $("#Area").val();
        var selectedValue = $(this).val();
        $('#WorkOrder').val('');
        switch (area) {
            case "A":
                $('#EndStation option').each(function () {
                    var tracname = $(this).val();
                    if ($(this).val().startsWith("B") && $(`#${tracname}`).attr("data-haveflag") === "0" && $(`#${tracname}`).attr("data-reserve") === "N") {
                        $('#EndStation').val($(this).val());
                        return false;
                    }
                });
                break;
            case "C":
                // 重置第二個選項的選擇
                $('#EndStation').val('');
                $('#WorkOrder').val($("#" + selectedValue).attr('data-workorder'));
                break;
            case "D":
                $('#EndStation option').each(function () {
                    var tracname = $(this).val();   
                    if ($(this).val().startsWith("E") && $(`#${tracname}`).attr("data-haveflag") === "0" && $(`#${tracname}`).attr("data-reserve") === "N") {
                        $('#EndStation').val($(this).val());
                        return false;
                    }
                });
                break;
            case "E":
                break;
        }

        if (area === "C") {
            $("#EndStation").prop("disabled", false);
            $("#WorkOrder").prop("disabled", true);
        }
        else {
            $("#EndStation").prop("disabled", true);
            $("#WorkOrder").prop("disabled", false);
        }

        $('#RackId').val($("#" + selectedValue).attr('data-rackid'));
        
    })

    //點選派送終點展開下拉選單時觸發的事件
    $("#EndStation").on("focus", function () {

        // 獲取第一個選項的選擇值
        var selectedValue = $('#BeginStation').val();

        // 隱藏所有第二個選項中的 <option>
        $('#EndStation option').hide();

        switch (selectedValue.substring(0, 1)) {
            case "A":
                 $('#EndStation option').filter(function () {
                    var tracname = $(this).val()
                    return $(this).val().startsWith("B") && $(`#${tracname}`).attr("data-haveflag") === "0";
                }).show();
                break;
            case "B":
                $('#EndStation option').filter(function () {
                    var tracname = $(this).val()
                    return $(this).val().startsWith("C");
                }).show();
                break;
            case "D":
                $('#EndStation option').filter(function () {

                    var tracname = $(this).val()
                    return $(this).val().startsWith("E") && $(`#${tracname}`).attr("data-haveflag") === "0";
                }).show();
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

        if (InterfaceName.length > 0 && area ==="D") {
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
                return false; // 停止遍歷
            }
        });

        if (allValid) {
            if (beginStation.substring(0, 1) === 'A') {
                var workOrder = $("#WorkOrder").val();
                if (!workOrder) {
                    alert('請輸入工單');
                    allValid = false;
                    return false;
                }
            }
            if ($("#" + beginStation).attr("data-haveflag") === "0") {
                alert('派送起點為空貨架，請重新選擇站點')
                allValid = false;
            }
            else if ($("#" + endStation).attr("data-haveflag") !== "0" && area !=="C") {
                alert('派送終點已有貨架，請重新選擇站點')
                allValid = false;
            }
        }

        if (!allValid) {
            return;
        }

        // 手動顯示 modal
        if (btnName == "ConfirmButton") {
            modal = $("#dispatchModalToggle")
        }
        else {         
            modal = $("#ReLoginModalToggle")
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
                if (error.responseJSON.message) {alert("派送失敗:" + error.responseJSON.message) }
                
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

//即時更新右側任務列表
function UpdateDispatch() {
    $.ajax({
        type: "GET",
        url: "/Dispatch/UpdateDispatch",
        success: function (data) {
            $('#DispatchStatus').html(data);
        },
        error: function (jqXHR, textStatus, errorThrown) {
            // 處理錯誤
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