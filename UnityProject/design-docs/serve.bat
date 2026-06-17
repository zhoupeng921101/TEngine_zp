@echo off
rem design-docs local server. Runtime .md rendering needs http:// (file:// blocks fetch via CORS).
cd /d "%~dp0"
set PORT=8765
where python >nul 2>nul && goto :py
where py >nul 2>nul && goto :pyl
echo Python not found, falling back to PowerShell server...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0server.ps1" -Port %PORT%
goto :eof
:py
echo Serving design-docs at http://localhost:%PORT%/   (close this window to stop)
start "" "http://localhost:%PORT%/index.html"
python -m http.server %PORT%
goto :eof
:pyl
echo Serving design-docs at http://localhost:%PORT%/   (close this window to stop)
start "" "http://localhost:%PORT%/index.html"
py -m http.server %PORT%
