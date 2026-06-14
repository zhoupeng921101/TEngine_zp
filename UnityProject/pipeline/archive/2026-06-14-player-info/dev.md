# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:player-info 玩家信息系统·数据逻辑层(自治·放手默认)

设计基线 `design-docs/18-player-info.html`。已完成全部 C/M/H/T 改动,编译 0 error,EditMode 全量 279/279 PASS(251 既有 + 28 新增,零回归)。

### 改动摘要(做了什么 / 为何 / 关键决策)
- **C1 Luban 头像表**:新建源 `Configs/GameConfig/Datas/avatar.xlsx`(6 字段 id/type/image/unlock_text/unlock_cond/unlock_param,样例 6 行:头像 1/2/3、框 101/102/103);`__enums__.xlsx` 追加 `avatar.EAvatarType`(AVATAR=1/FRAME=2)、`avatar.EUnlockCond`(LEVEL=1/EVENT=2);`__tables__.xlsx` 追加 `avatar.TbAvatar`(input avatar.xlsx / index id / mode map / read_schema_from_file=True / group 留空走默认 c,s)。**导表已成功**(直调 `dotnet Luban.dll`,带 `DOTNET_ROLL_FORWARD=Major`,绕 .bat 的 pause),生成 `GameConfig.Avatar`(行,顶层命名空间)/ `GameConfig.avatar.TbAvatar`(表)/ 两枚举 + `avatar_tbavatar.bytes`(loader key `avatar_tbavatar`)+ 更新 `Tables.cs`。生成代码未手改。
- **C2 AvatarConfigMgr + AvatarEntry**:`GameLogic.Config.AvatarConfigMgr`(仿 ItemConfigMgr:`ToEntry`桥接/`EnsureLoaded`经ConfigSystem/`GetAvatar`/`GetByType`/`All`/`InitForTest`/`ResetForTest`);POCO `AvatarEntry`(落 `GameLogic.BlockBlast.Player`,Type/UnlockCond 存 int,不依赖 Luban 枚举,使解锁服务纯逻辑)。
- **C3 PlayerInfo**:POCO + 常量 DefaultAvatarId=1/DefaultFrameId=101 + `NewId()`(Guid) + `CreateDefault(rng)` + `Level` 计算属性。
- **C4 名字/改名/屏蔽字**:`PlayerNameGenerator.Generate`(Player+6 随机/62 字符集)/`ProfanityFilter.IsClean`(大小写不敏感子串、空表不拦、词表可注入)/`RenamePriceConfig`(固定价 100)/`PlayerRenameService.TryRename`(判定序:合法→屏蔽字→计费→写名;`trySpendDiamond` 接缝外置)+ `RenameResult`/`RenameReject`。
- **C5 等级曲线**:`PlayerLevelConfig`(BASE=100/STEP=50/MAX=60,`CumExp`闭式/`LevelFor`/`ExpIntoLevel`/`ExpToNext`)+ `PlayerExpService.AddExp`(只增,负数夹0)+ `ILevelRewardProvider`/`LevelReward`/`EmptyLevelRewardProvider` 占位(O7)。
- **C6 解锁判定**:`AvatarUnlockService`(`IsUnlocked`两源取或/`StateOf`三态/`SyncLevelUnlocks`去重/`TryEquip`限已解锁/`GrantUnlock` 活动钩子 O3)+ `AvatarState` 枚举 + `AvatarType`/`UnlockCond` 语义常量。
- **C7 剪贴板**:`GameLogic.BlockBlast.ClipboardUtil`(可注入 `Sink`,默认写 `GUIUtility.systemCopyBuffer`,空串不写)。
- **M1 持久化(增量)**:`MergeMetaSave` 加 8 个玩家平铺字段(playerId/playerName/playerRenameCount/playerExp/curAvatarId/curFrameId/unlockedAvatarIds[]/unlockedFrameIds[]),既有 15 字段一行未改,CurrentVersion 不升(git diff 核实:15 insertions / 0 deletions)。Export/Import + 逐字段保底夹值做成 **PlayerInfo 上的纯静态方法** `ExportToMeta(dto)`/`ImportFromMeta(dto, rng, avatarValid?, frameValid?)`,不挂进 MergeOrderState/悔棋快照(见下决策)。
- **H1 入口钩子**:`MainMenuWindow.OnCreate` 末尾加 `// TODO(player-info UI 轮): 左上角入口 → 打开 PlayerInfoWindow` 注释,无实现(4 insertions)。
- **T1 测试**:`Assets/Editor/Tests/BlockBlast/PlayerInfoTests.cs`,28 个 test 覆盖 30 验收点;C 类直读 `avatar_tbavatar.bytes`,其余纯逻辑 new/静态/InitForTest。

### 关键决策(自主拍板,均安全默认)
1. **持久化挂载点**:plan M1 字面写「MergeOrderState.ExportMeta/ImportMeta 加玩家字段拷贝」,但 MergeOrderState 不持有 PlayerInfo(玩家等级是独立第三进度线 §3.4)。把玩家字段塞进该状态机会耦合两个无关域、并污染悔棋 Snapshot。改为把 Export/Import + 保底做成 `PlayerInfo` 的纯静态方法(DTO 字段仍平铺在同一份 MergeMetaSave,做法 a 不变)——设计稿 §2.2 注已显式认可此为可接受做法(「独立子对象做法则这两处只是新增」),非偏离 spec。验收 S1-S3 全绿。
2. **AvatarEntry Type/UnlockCond 存 int** 而非 Luban 枚举:同 ItemDef 存 Quality/Type int 的做法,使 AvatarUnlockService 不引生成代码、保持纯逻辑(Z1)。常量类 `AvatarType`/`UnlockCond` 提供可读名。
3. **ImportFromMeta 的「头像在表内」校验**经可选 `avatarValid`/`frameValid` 谓词注入(运行期可接 AvatarConfigMgr.GetAvatar!=null;单测不传 = 只夹 ≤0),不把持久化纯方法耦合到 ConfigSystem 加载(Z1)。
4. **EmptyLevelRewardProvider** 返 `LevelReward?`(可空)表达「该级无预览」,O7 占位空实现。

### 文件清单
新增:
- `Configs/GameConfig/Datas/avatar.xlsx`(Luban 源)
- `UnityProject/Assets/AssetRaw/Configs/bytes/avatar_tbavatar.bytes`(+.meta,导表产出)
- `UnityProject/Assets/GameScripts/HotFix/GameProto/GameConfig/{Avatar.cs, avatar.TbAvatar.cs, avatar.EAvatarType.cs, avatar.EUnlockCond.cs}`(+.meta,生成代码,勿手改)
- `UnityProject/Assets/GameScripts/HotFix/GameLogic/Config/AvatarConfigMgr.cs`
- `UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Player/{PlayerInfo.cs, PlayerNameGenerator.cs, ProfanityFilter.cs, PlayerRenameService.cs, PlayerLevelConfig.cs, AvatarEntry.cs, AvatarUnlockService.cs, ClipboardUtil.cs}`
- `UnityProject/Assets/Editor/Tests/BlockBlast/PlayerInfoTests.cs`(+.meta)

修改(增量):
- `Configs/GameConfig/Datas/{__enums__.xlsx, __tables__.xlsx}`(追加 avatar 枚举/表注册)
- `UnityProject/Assets/GameScripts/HotFix/GameProto/GameConfig/Tables.cs`(导表自动加 TbAvatar 懒加载)
- `UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/MergeMetaSave.cs`(加 8 玩家字段,既有不动)
- `UnityProject/Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MainMenuWindow.cs`(H1 TODO 注释)

未碰(零回归):`ItemGrant.cs`/`NumericConfigMgr.cs`/`ItemConfigMgr.cs`/`RewardDisplay.cs`/既有玩法逻辑/任何 UI 窗口/MergeMetaSave 既有字段/MergeOrderState。

### 验证点(交测试逐条核对验收标准 §六 30 条)
- **该验什么 / 怎么验 / 预期**:运行 EditMode 全量(unityMCP `run_tests mode=EditMode`),应 279/279 PASS。新增 `PlayerInfoTests` 28 例逐条对应:
  - N1-N2(名字结构/随机) · R1-R5(改名:首免不调 trySpend / 二次扣 100 / 不足拒 / 屏蔽字拒不计费 / 空·超长拒) · P1-P2(屏蔽字大小写不敏感/空表不拦) · L1-L4(等级换算 0→1/99→1/100→2/250→3,槽 300→Into50·ToNext150,封顶60·ToNext0,AddExp只增) · U1-U6(等级实时解锁/三态Equipped·Unlocked·Locked/EVENT本轮不判·写集合后可解/换装限已解锁/Sync去重) · C1-C2(直读 .bytes 6 行按 id 查字段 / AvatarConfigMgr 桥接 GetAvatar·GetByType) · D1-D2(CreateDefault 缺省 / id 不随改名变) · S1-S3(往返保真 / 旧档缺字段保底不抛不丢既有 / 篡改值夹合法) · B1(剪贴板注内存sink Copy==Id·空不写·测后还原) · Z1(纯逻辑无 ConfigSystem)。
  - Z2(既有零回归):本轮已跑全量 279/279,既有 251 全绿。
- **可复跑命令**:导表如需重生成——仓库根 `Configs/GameConfig/`,PowerShell 设 `$env:DOTNET_ROLL_FORWARD="Major"` 后直调 `dotnet ..\..\Tools\Luban\Luban.dll -t client -c cs-bin -d bin --conf .\luban.conf --customTemplateDir .\CustomTemplate\CustomTemplate_Client_LazyLoad -x code.lineEnding=crlf -x outputCodeDir=<GameProto/GameConfig/> -x outputDataDir=<AssetRaw/Configs/bytes/>`(.bat 的 pause 会卡自动化,直调绕开)。

### 标注
- **热更程序集**:本轮所有逻辑落 `GameScripts/HotFix/`(GameLogic + GameProto),属热更范围。
- **Luban 重生成**:已重生成并提交产出(avatar_tbavatar.bytes + GameConfig.avatar.*);若改 avatar.xlsx/枚举需重跑导表(命令见上)。
- **需进 Play 模式手验**:无。全部数据逻辑层,EditMode 即可验;UI 表现层本轮不做(O1 延后,只留 H1 TODO)。

### 自检(交接前已做)
- `read_console` 无 CS 编译错误(refresh 后 state idle,compile_requested=false)。
- EditMode 全量 279/279 PASS(job 已 succeeded,failed=0)。
- 核心路径自跑:名字生成/改名四分支/等级换算/解锁三态/持久化往返/剪贴板注入均经 28 例断言通过。

上一单 reward-display 已于 2026-06-14 关单 PASS,归档 `pipeline/archive/2026-06-14-reward-display/dev.md`。
