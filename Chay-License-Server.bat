@echo off
title C3 License Server
echo Dang khoi dong License Server tai http://localhost:5000...
dotnet run --project LicenseServer/LicenseServer.csproj --urls "http://localhost:5000"
pause
