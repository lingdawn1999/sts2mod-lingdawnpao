# 雪豹埋伏动画原生检查

测试从导出的PCK加载实际4.2骨架、三页图集，通过本机Spine GDExtension播放。
使用真实CreatureAnimator和Harmony动画入口，省略无关的战斗UI初始化。

覆盖：正常攻击返回待机；进入埋伏后切入第二套循环；攻击/施法/能力动画不重启埋伏待机；
受击不站起；连续快速切换无旧队列残留；退出后正常待机；死亡不循环、迟到回调不能打断；复活恢复所选形态。
播放进度直接检查TrackTime，不能用Spine临时包装对象的ID判断是否重启。

PowerShell（先构建主项目并导出build/lingdawnpao.pck）：

```powershell
$env:NUGET_PACKAGES='C:\Users\Administrator\.nuget\packages'
dotnet build tests/AnimationRegression/AnimationRegression.csproj -c Debug --no-restore
& ./build/card-test-host/Godot_console.exe --path ./tests/AnimationRegression --rendering-method gl_compatibility --audio-driver Dummy
```

beta添加 `-p:Sts2Ref=beta -p:BetaGameReference=D:/sts2mod/lingdawnpao/.godot/ambush-tests/runtime-beta-no-telemetry/sts2.dll`。
测试工程通过 `.godot/extension_list.cfg` 加载spine.gdextension，路径仅供本地测试，不进入模组PCK。
完整客户端的战斗位置、受击观感及剧情死亡页面仍需实测。
