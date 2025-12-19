@echo off
chcp 65001 > nul
echo ====================================
echo   AGV Webhook 54632 恢復工具
echo ====================================
echo.

REM 檢查是否以管理員權限執行
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [錯誤] 請以管理員權限執行此腳本！
    echo 右鍵點選腳本 → 以系統管理員身分執行
    pause
    exit /b 1
)

echo [1/3] 移除 URL ACL 設定...
netsh http delete urlacl url=http://+:54632/
if %errorLevel% equ 0 (
    echo ✓ URL ACL 移除成功
) else (
    echo × URL ACL 不存在或移除失敗（這可能是正常的）
)
echo.

echo [2/3] 檢查防火牆規則（保留）...
netsh advfirewall firewall show rule name="AGV Webhook 54632" >nul 2>&1
if %errorLevel% equ 0 (
    echo ✓ 防火牆規則存在（保留不刪除）
) else (
    echo × 防火牆規則不存在
)
echo.

echo [3/3] 驗證 54632 埠狀態...
netstat -ano | findstr :54632
if %errorLevel% neq 0 (
    echo ✓ 埠 54632 目前沒有被佔用
) else (
    echo ! 埠 54632 正在被使用
)
echo.

echo ====================================
echo   恢復完成！
echo   請重新啟動 HikAGVWebAPI 專案
echo ====================================
pause
