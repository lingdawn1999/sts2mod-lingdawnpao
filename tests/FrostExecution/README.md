# 冰冻处决集成验证

使用实际 Godot 渲染器运行，不可使用 `--headless`：Dummy 渲染器不会生成敌人截图。
测试从导出的 `build/lingdawnpao.pck` 加载碎冰脚本和两个 Shader，并经真实 Harmony 补丁调用
`NDoomVfx.PlayVfxInternal`。fixture 复用原版视口大小、嵌套 Mask 偏移，使用普通 Sprite 代替 Spine 身体。

25 项检查覆盖补丁绑定、不同目标/嵌套作用域隔离、特殊死亡回退、截图尺寸与脚底位置、碎片生成、
正常释放、捕获及播放期间退出场景、空截图回退、多个目标同时播放。
空截图用例预期会输出一条“敌人捕获画面为空”的警告。

PowerShell：

```powershell
$env:NUGET_PACKAGES = 'C:\Users\Administrator\.nuget\packages'
dotnet build tests/FrostExecution/FrostExecution.csproj -c Debug --no-restore
& ./build/card-test-host/Godot_console.exe --path ./tests/FrostExecution --rendering-method gl_compatibility --audio-driver Dummy
```

beta 使用与 `build/beta-01110-validation-20261026.md` 相同的隔离参考文件：

```powershell
dotnet build tests/FrostExecution/FrostExecution.csproj -c Debug -p:Sts2Ref=beta -p:BetaGameReference=D:/sts2mod/lingdawnpao/.godot/ambush-tests/runtime-beta-no-telemetry/sts2.dll --no-restore
```

这不等同于完整客户端战斗验证：仍需在游戏里观察不同 Spine 模型、死亡钩子及战斗结束页面。
测试程序集与素材不进入发布包。
