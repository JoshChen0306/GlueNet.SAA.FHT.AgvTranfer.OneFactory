// 順風車
import { connection } from './common/hub.js';

$(function () {
    let currentModal = null;
    let countdownTimer = null; // 在這裡宣告

    connection.on("SendMissionChange", function () {
        console.log("oMission 狀態改變")

        const modalElement = parent.document.getElementById('hitchhikeModal');
        if (!modalElement) {
            alert('找不到順風車模態視窗');
            return;
        }

        if (modalElement.contains(parent.document.activeElement)) {
            parent.document.activeElement.blur();
        }

        //const hitchhikeMission = getHitchhikeStation();

        getHitchhikeStation().then(hitchhikeMission => {
            if (hitchhikeMission.beginStation && hitchhikeMission.endStation) {
                setTimeout(() => {
                    currentModal = new parent.bootstrap.Modal(modalElement, {
                        backdrop: 'static',
                        keyboard: false
                    });
                    const modalElementContent = parent.document.getElementById('hitchhike-model-content-station');
                    modalElementContent.innerHTML =
                        `搬運起點：${hitchhikeMission.beginStation} => 搬運終點：${hitchhikeMission.endStation}
                        <br />
                        Begin station：${hitchhikeMission.beginStation} => End station：${hitchhikeMission.endStation}`

                    currentModal.show();
                    startCountdown(300)
                }, 10);
            }
        })

        //if (hitchhikeMission) {
        //    setTimeout(() => {
        //        currentModal = new parent.bootstrap.Modal(modalElement, {
        //            backdrop: 'static',
        //            keyboard: false
        //        });
        //        currentModal.show();
        //        startCountdown(30)
        //    }, 10);
        //}

        //currentModal = new parent.bootstrap.Modal(modalElement, {
        //    backdrop: 'static',
        //    keyboard: false
        //});
        //currentModal.show();
        //startCountdown(30)
    });

    function getHitchhikeStation() {
        return new Promise((resolve, reject) => {
            $.ajax({
                type: "GET",
                url: "/api/Common/GetHitchhikeStation",
                success: function (data) {
                    if (!data) {
                        console.log("無可執行之順風車任務");
                        resolve(null);
                    } else {
                        console.log("獲取順風車任務資料 :", data);
                        resolve(data);
                    }
                },
                error: function (jqXHR, textStatus, errorThrown) {
                    console.error("AJAX 請求失敗: ", textStatus, errorThrown);
                    reject(null);
                }
            });
        });
    }

    function startCountdown(seconds) {
        const countdownElement = parent.document.getElementById('countdown');
        const progressElement = parent.document.getElementById('countdown-progress');

        let currentSeconds = seconds;
        const totalSeconds = seconds;

        function updateCountdown() {
            // 更新顯示
            if (countdownElement) {
                countdownElement.textContent = currentSeconds;
            }

            // 更新進度條
            if (progressElement) {
                const percentage = (currentSeconds / totalSeconds) * 100;
                progressElement.style.width = percentage + '%';
                progressElement.setAttribute('aria-valuenow', percentage);

                // 根據剩餘時間改變顏色
                if (currentSeconds <= 10) {
                    progressElement.className = 'progress-bar progress-bar-striped progress-bar-animated bg-danger';
                } else if (currentSeconds <= 20) {
                    progressElement.className = 'progress-bar progress-bar-striped progress-bar-animated bg-warning';
                } else {
                    progressElement.className = 'progress-bar progress-bar-striped progress-bar-animated bg-success';
                }
            }
        }

        updateCountdown()

        countdownTimer = setInterval(function () {
            currentSeconds--;

            // 倒計時結束
            if (currentSeconds === 0) {
                clearInterval(countdownTimer);
                handleTimeout();
            }
            updateCountdown()
        }, 1000);
    }

    function handleTimeout() {
        console.log('倒計時結束，自動關閉模態框');

        if (currentModal) {
            currentModal.hide();
        }

        // 這裡可以加入超時後的處理邏輯
        // 例如：自動執行某個動作或顯示其他提示
    }

    // 清理計時器（當模態框關閉時）
    function cleanup() {
        if (countdownTimer) {
            clearInterval(countdownTimer);
            countdownTimer = null;
        }
    }

    // 監聽模態框關閉事件
    if (parent.document) {
        parent.$(parent.document).on('hidden.bs.modal', '#hitchhikeModal', function () {
            console.log('模態框關閉，清理計時器');
            cleanup();
        });
    }
})