@echo off
rem 双击启动 design-docs 本地服务器并打开浏览器(运行时渲染 .md 必须经 http:// 打开)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0server.ps1"
