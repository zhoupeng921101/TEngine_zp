Cd /d %~dp0
echo %CD%

set WORKSPACE=../../
set LUBAN_DLL=%WORKSPACE%/Tools/Luban/Luban.dll
set CONF_ROOT=.
rem 生成路径指向 Fantasy 服务端工程的 Entity 项目:
rem   - 代码 = Entity/Generate/GameConfig (与协议导出物 NetworkProtocol 同层,被 Entity.csproj 自动 glob 编译)
rem   - 数据 = Entity/Generate/GameConfigBytes (.bytes 由 Entity.csproj 的 None+CopyToOutputDirectory 跟随 Main 进程 bin)
set DATA_OUTPATH=%WORKSPACE%/Fantasy/examples/Server/APP/Entity/Generate/GameConfigBytes
set CODE_OUTPATH=%WORKSPACE%/Fantasy/examples/Server/APP/Entity/Generate/GameConfig

dotnet %LUBAN_DLL% ^
    -t server^
    -c cs-bin ^
    -d bin^
    --conf %CONF_ROOT%\luban.conf ^
    -x code.lineEnding=crlf ^
    -x outputCodeDir=%CODE_OUTPATH% ^
    -x outputDataDir=%DATA_OUTPATH% 
if not defined AI_MODE if errorlevel 1 pause

