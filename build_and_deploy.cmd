@echo off
chcp 65001 >nul
rem 一键构建部署：无头导出 PCK + C# 编译 + 三件套进游戏 mods 目录 + 同步到工坊上传目录
rem
rem 2026-09-25 起本模组是**一份 DLL 同时支持正式版（public）与测试分支（public-beta）**
rem （做法见 src/Core/BranchCompat.cs 与 csproj 里的说明），所以不再分两个构建、也只有一个工坊条目。
rem 但这里保留一步**双分支编译校验**：先对着 libs\sts2.beta.dll 也编一遍。
rem 只要代码里重新出现"只在某一个分支能编过"的写法，这一步就会报错中止，
rem 免得把只在一边能跑的 DLL 发出去（那种问题进游戏才炸，玩家只会来评论区问）。
setlocal
set MOD_ID=lingdawnpao
set MOD_PATH=%~dp0
set GODOT_PATH=D:\sts2mod\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe
set STS2_MODS=D:\steam\steamapps\common\Slay the Spire 2\mods
set UPLOAD_CONTENT=D:\sts2mod\ModUploader-win-x64\workspace-lingdawnpao\content

echo [1/5] 导出 PCK...
"%GODOT_PATH%" --headless --path "%MOD_PATH%." --export-pack "Windows Desktop" "%MOD_PATH%build\%MOD_ID%.pck"
if errorlevel 1 ( echo PCK 导出失败 & exit /b 1 )

echo [2/5] 双分支编译校验（对着测试分支的参考程序集编一遍，产物丢弃）...
cd /d "%MOD_PATH%"
dotnet build -c Debug -p:Sts2Ref=beta --no-incremental
if errorlevel 1 ( echo 测试分支编译失败：代码里又出现了只在单分支能编过的写法，见 csproj 注释 & exit /b 1 )

echo [3/5] 编译 C#（正式版参考程序集，这份就是要发布的）...
dotnet build -c Debug --no-incremental
if errorlevel 1 ( echo 编译失败 & exit /b 1 )

echo [4/5] 部署到游戏 mods 目录...
if not exist "%STS2_MODS%\%MOD_ID%" mkdir "%STS2_MODS%\%MOD_ID%"
copy /y "%MOD_PATH%.godot\mono\temp\bin\Debug\%MOD_ID%.dll" "%STS2_MODS%\%MOD_ID%\" >nul
copy /y "%MOD_PATH%build\%MOD_ID%.pck" "%STS2_MODS%\%MOD_ID%\" >nul
copy /y "%MOD_PATH%%MOD_ID%.json" "%STS2_MODS%\%MOD_ID%\" >nul

echo [5/5] 同步到工坊上传目录（只拷贝，不会自动上传）...
if not exist "%UPLOAD_CONTENT%" mkdir "%UPLOAD_CONTENT%"
copy /y "%MOD_PATH%.godot\mono\temp\bin\Debug\%MOD_ID%.dll" "%UPLOAD_CONTENT%\" >nul
copy /y "%MOD_PATH%build\%MOD_ID%.pck" "%UPLOAD_CONTENT%\" >nul
copy /y "%MOD_PATH%%MOD_ID%.json" "%UPLOAD_CONTENT%\" >nul

echo 完成：已部署到 %STS2_MODS%\%MOD_ID%
dir /b "%STS2_MODS%\%MOD_ID%"
echo 已同步到 %UPLOAD_CONTENT%
dir /b "%UPLOAD_CONTENT%"
endlocal