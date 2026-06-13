# Boss 关单总结:Collect* 共享设施正名(collect-rename)

> 关单时从 `state/boss.md`「当前任务」编排日志归档而来。冻结历史,不修改。流水线迁移 Claude Code 后首单。

- **结论:PASS 交付**。dev→test 两个环节(纯重构裁剪环节),打回轮次 0。
- 来源:score-element-rm-collect 拍板第 3 项注册的低优正名任务。9 个符号正名(`CollectElement→MergeElement`、`CollectDemo→MergeElementVisual`、`CollectWinWindow→MergeOrderWinWindow` 含 prefab/location/[Window]、`CollectClearedElements/CollectAt→HarvestClearedElements/HarvestAt` 等),行为零变化。
- **运行验证**:编译 0 error;EditMode `BlockBlast.Tests` 96/96;**Play 寻址链实测通畅**(ShowUIAsync\<MergeOrderWinWindow\> 实例化+CloseUI 回主菜单,截图 `Assets/Screenshots/collect-rename_02_winwindow.png`);三对 .meta git rename 保 GUID;旧符号全工程 0 匹配。
- 证据:本目录 test.md(四类全过)+ dev.md(改名清单)。
- 模型档:dev=opus、test=opus(寻址链风险,不省档)。spawn 登记:dev `aafa03ff1c55f2800`、test `ad48020494dfd5675`(均已结束)。
- **流水线机制验证(首单顺带)**:角色卡硬注入、子 agent unityMCP 直跑运行验证(boss 无需补跑,OpenClaw 时代的代跑路径退役)、返回契约三行便条、交接区协议、关单事务——全部如设计运转;打回循环未触发(0 打回),留待后续任务实测。
