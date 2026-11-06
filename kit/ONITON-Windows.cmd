@echo off
rem 윈도우: ONITON.exe 를 바로 더블클릭해도 됩니다. 경고(차단)가 뜨면 이 파일을 대신 실행하세요.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Unblock-File -Path '%~dp0ONITON.exe' -ErrorAction SilentlyContinue"
start "" "%~dp0ONITON.exe"
