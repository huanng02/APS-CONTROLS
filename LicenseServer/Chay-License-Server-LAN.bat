@echo off
title C3 License Server (LAN Sharing)
color 0B

:: Kiem tra quyen Admin (can thiet de mo firewall)
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [!] Can quyen Administrator de mo cong tuong lua (Firewall).
    echo [*] Dang yeu cau quyen Admin...
    powershell -Command "Start-Process cmd -ArgumentList '/c \"%~f0\"' -Verb RunAs"
    exit /b
)

echo ========================================================
echo        C3 LICENSE SERVER - CHIA SE TRONG MANG LAN
echo ========================================================
echo.

:: 1. Mo cong 5000 tren Windows Firewall
echo [1/3] Dang mo cong 5000 tren Windows Firewall...
netsh advfirewall firewall show rule name="C3 License Server" >nul 2>&1
if %errorlevel% neq 0 (
    netsh advfirewall firewall add rule name="C3 License Server" dir=in action=allow protocol=TCP localport=5000 >nul
    echo      [OK] Da tao thiet lap tuong lua cho Port 5000
) else (
    echo      [OK] Thiet lap tuong lua da co san
)

:: 2. Lay dia chi IP cua may ban
echo.
echo [2/3] Dang lay dia chi IP mang LAN cua ban...
for /f "tokens=4 delims= " %%i in ('route print ^| findstr 0.0.0.0 ^| findstr /v "127.0.0.1"') do (
    set LOCAL_IP=%%i
)
if "%LOCAL_IP%"=="" (
    for /f "usebackq tokens=*" %%A in (`powershell -NoProfile -Command "(Get-NetIPAddress -AddressFamily IPv4 -InterfaceAlias 'Ethernet*', 'Wi-Fi*').IPAddress | Select-Object -First 1"`) do set LOCAL_IP=%%A
)

echo      IP cua may ban la: %LOCAL_IP%

echo.
echo [3/3] Khoi dong server va chia se qua mang LAN...
echo ========================================================
echo  Server dang chay tai:
echo  - Noi bo:   http://localhost:5000
echo  - Mang LAN:  http://%LOCAL_IP%:5000
echo.
echo  GIAO DIA CHI SAU CHO DONG NGHIEP DE KET NOI:
echo  --> http://%LOCAL_IP%:5000
echo ========================================================
echo.

:: Chay dotnet server tu thu muc LicenseServer (dung file csproj hien tai)
dotnet run --project LicenseServer.csproj --urls "http://0.0.0.0:5000"

pause
