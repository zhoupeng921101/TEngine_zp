# 状态:策划(plan)

> 开工先读本文件(角色职责在 `.claude/agents/pipeline-plan.md`,spawn 时自动注入)。每完成一步就更新这里。

## 当前任务:player-info 玩家信息系统·数据逻辑层(自治·放手默认)

设计稿:`design-docs/18-player-info.html`(已挂 index 卡片 + 全库 14 篇 sidebar 文档树同步;断链检查 0/19;TOC 锚点 19/19 解析)。
设计基调:**数据逻辑层先行**(沿用 15/16/17 节奏)——玩家信息数据模型 + Luban 头像表 + 一组无状态纯逻辑服务(名字/改名/屏蔽字/解锁三态/等级/剪贴板),全 EditMode 可测;持久化增量并入既有 MergeMetaSave;**UI 窗口(玩家信息界面/改名界面/三态网格/经验槽/奖励预览/主界面入口)→ 表现层延后(需美术),只留服务 + 入口 TODO 钩子**。加法式不重构既有玩法/数值/道具/存档。

### 设计基线(经 grep 核实的真实符号,dev 据此定位)
- 持久化(复用,增量并入):`GameLogic.BlockBlast.MergeMetaSave`(DTO 15 字段) + `MergeMetaPersistence`(`Serialize/Deserialize/Migrate/ApplyDailyReset/Load/SaveAsync/LoadAsync`,`CurrentVersion=1`,`StorageKey="block_blast_merge_meta_v1"`) + `MergeOrderState.ExportMeta(today)`/`ImportMeta(dto,today)`(纯方法,逐字段保底夹值)。`MergeMetaSave` 现有字段全平铺 `[Serializable]`。
- 数值/扣费:`GameLogic.Config.NumericConfigMgr`(常量 `Diamond=3`;`Get/GetByType/InitForTest/ResetForTest`);`ItemGrant.ApplyNumeric(state,payload)` 对钻石走 default 分支返 `false`(钻石无 MergeOrderState 字段——item-system 关单遗留 #19 同此)。
- 配置范本:`GameLogic.Config.ItemConfigMgr`(Luban 行→POCO 桥接 + `EnsureLoaded`/`GetItem`/`InitForTest`/`ResetForTest`);`Assets/Editor/Tests/BlockBlast/ItemSystemTests.cs`(`AssetDatabase.LoadAssetAtPath<TextAsset>(".../item_tbitemdef.bytes")` → `new GameConfig.item.TbItemDef(new Luban.ByteBuf(ta.bytes))` 直读绕 YooAsset)。枚举范本 `item.EItemQuality`(COMMON=1…MYTH=6)/`num.ENumType`。
- 品质色复用:`RewardDisplay.QualityColor(int)`(设计 17,6 档权威白/绿/蓝/紫/橙/红)——头像/框品质显示复用,不另造色表(O5;且 avatar 表本轮无 quality 字段,品质显示属可选)。
- 等级现状:工程**无「玩家账号等级」**——仅 `MergeOrderState.GuardianLevel`(`TempleConfig.GuardianLevelFor(Exp)` 纯函数,仅修神庙产经验)与 `GoddessLevel`。玩家等级是独立第三进度线(设计稿 §3.4 钉死,不复用守护者经验)。
- 剪贴板:工程无现成 systemCopyBuffer 用例;本层薄封装 `ClipboardUtil`(可注入 Sink,单测注内存 sink 不碰真实剪贴板)。
- Luban 源在仓库根 `Configs/GameConfig/Datas/`(与 UnityProject 同级),现有 num/item/itemdef/giftrandom/giftselect/weightcfg;无 player/avatar 表 → 本轮新建。导表带 `DOTNET_ROLL_FORWARD=Major`(遗留 #18)。
- 11-core-loop-completion.html 用旧 topbar 布局无 sidebar 树(遗留结构差异),不在 sidebar 同步范围,正确跳过(memory:加文档勿顺手扩布局改造范围)。

### 关键自治决策(放手默认推进;详见设计稿 §intro / 各节注 / §七)
1. 数据层先行,全部 UI(含主界面左上角入口)延后表现层轮,只留服务 + 入口 TODO(沿用 15/16/17;boss 授权)
2. 玩家等级 = 独立第三进度线(新 `PlayerInfo.Exp` + `PlayerLevelConfig` 线性曲线 BASE=100/STEP=50/MAX=60),**不复用守护者经验**——语义不同(守护者仅修神庙产出),复用会混淆两套等级。安全默认:加法式不动守护者
3. id 本地生成(默认 `Guid.NewGuid().ToString("N")`,本地唯一足够;离线无服务器);id 不随改名变
4. 改名价格默认固定价常量 100 钻(`RenamePriceConfig`,可后续换分档/Luban 表,不动 TryRename);首次 RenameCount==0 免费
5. 钻石扣费经可注入 `trySpendDiamond` 接缝,生产默认返 true(钻石无余额字段 + 去变现:不靠钻石卡改名),逻辑可测;待钻石实装接真实扣减不返工(O8)
6. 屏蔽字算法子串大小写不敏感 + 词表可注入,空词表=不拦(去变现/不阻塞);真实词表延后(O6)
7. 持久化默认平铺进 MergeMetaSave(同既有 15 字段口径),CurrentVersion 不升,ImportMeta 逐字段保底(同设计 14 红线)
8. 头像表补一个 `unlock_param int` 字段(spec 字段隐含必需:LEVEL 解锁须知哪一级;同设计15给num补func_name贴spec),非擅自扩需求
9. 已解锁集合用 `int[]` 落盘(JsonUtility 不序列化 HashSet,同 MergeMetaSave 约定),运行期临时转 HashSet 查重
10. 解锁条件 type 2 活动发放本轮只留钩子(表字段+判定分支齐备,无活动系统不判;接活动调 GrantAvatar 写集合,逻辑不返工,O3)

---

## 交接区(交开发)

### dev 改动清单(详见设计稿 §五;符号名经 grep 核实。C=新建 M=改既有增量 H=钩子 T=测试 X=不碰)
- **C1** Luban 头像表:源 `Configs/GameConfig/Datas/avatar.xlsx`(6 字段:id/type/image/unlock_text/unlock_cond/unlock_param,§3.5);`__enums__.xlsx` 追加 `avatar.EAvatarType`(AVATAR=1/FRAME=2)、`avatar.EUnlockCond`(LEVEL=1/EVENT=2);`__tables__.xlsx` 追加 `avatar.TbAvatar` 行(input avatar.xlsx / index id / mode map)。跑导表(`gen_code_bin_to_project_lazyload`,带 `DOTNET_ROLL_FORWARD=Major`)生成 `GameConfig.avatar.*` + `avatar_tbavatar.bytes`(loader key `avatar_tbavatar`)。**生成代码不手改**
- **C2** `GameLogic.Config.AvatarConfigMgr`(仿 ItemConfigMgr:`ToEntry`桥接/`EnsureLoaded`经ConfigSystem/`GetAvatar(id)`/`GetByType(type)`/`All()`/`InitForTest`/`ResetForTest`) + POCO `AvatarEntry`(Id/Type/Image/UnlockText/UnlockCond/UnlockParam)。落 `GameScripts/HotFix/GameLogic/Config/`
- **C3** `PlayerInfo`(POCO §3.1) + 常量 `DefaultAvatarId=1`/`DefaultFrameId=101` + `NewId()`(默认 Guid) + `CreateDefault(rng)`。新目录 `GameScripts/HotFix/GameLogic/Module/BlockBlast/Player/`
- **C4** `PlayerNameGenerator.Generate(rng)`(§3.2) / `ProfanityFilter.IsClean(name,wordList)`(§3.3) / `RenamePriceConfig`(常量 100) / `PlayerRenameService.TryRename(p,newName,wordList,Func<int,bool> trySpendDiamond)` + `RenameResult`/`RenameReject{None,Empty,TooLong,Profanity,NotEnoughDiamond}`(§3.2)
- **C5** `PlayerLevelConfig`(`LevelFor`/`ExpIntoLevel`/`ExpToNext`/`CumExp`,BASE=100/STEP=50/MAX=60,§3.4) + `PlayerExpService.AddExp(p,n)`(只增,负数夹0) + `LevelReward` 占位接口(空实现)
- **C6** `AvatarUnlockService`(`IsUnlocked`/`StateOf`/`SyncLevelUnlocks`/`TryEquip`,§3.6) + `AvatarState{Locked,Unlocked,Equipped}`
- **C7** `GameLogic.BlockBlast.ClipboardUtil`(可注入 `Sink`,默认写 `GUIUtility.systemCopyBuffer`,§3.7)
- **M1** `MergeMetaSave.cs` 加玩家平铺字段(playerId/playerName/renameCount/playerExp/curAvatarId/curFrameId/unlockedAvatarIds[]/unlockedFrameIds[]);`MergeOrderState.ExportMeta`/`ImportMeta` 加玩家字段拷贝 + 逐字段保底夹值(§3.8 注:id/name 空则现生成、count/exp<0夹0、头像框越界退默认、集合null重建含默认)。**既有字段一行不改,CurrentVersion 不升**
- **H1** 主界面 UI 脚本留 `// TODO(player-info UI 轮): 左上角入口 → 打开 PlayerInfoWindow` 注释钩子,不写实现
- **T1** 新建 `Assets/Editor/Tests/BlockBlast/PlayerInfoTests.cs`(asmdef 已引用 GameLogic/GameProto,新增 .cs 自动纳入):C 类直读 `avatar_tbavatar.bytes`;其余纯逻辑 new/静态/InitForTest
- **X 不碰(零回归)**:`ItemGrant.cs`/`NumericConfigMgr.cs`/`ItemConfigMgr.cs`/`RewardDisplay.cs`/既有玩法逻辑/任何 UI 窗口/`MergeMetaSave` 既有字段(只加不改)

### 验收标准(30 条,test 逐条核对;详见设计稿 §六)
- **N1-N2** 名字生成:Player 前缀 + 总长12 + 后6字符落62字符集;同种子确定/异种子不全等
- **R1-R5** 改名:首免Cost0未调trySpend / 二次读价扣钻 / 钻石不足拒不改 / 屏蔽字拒且不计费(trySpend未调) / 空·超长拒
- **P1-P2** 屏蔽字:大小写不敏感子串;空词表不拦
- **L1-L4** 等级:换算分档(0→1/99→1/100→2/250→3) / 槽进度(300→IntoLevel50,ToNext150) / 封顶60 ToNext0 / AddExp只增
- **U1-U6** 解锁三态:等级实时解锁 / 佩戴Equipped / 已解锁未佩戴·未解锁 / EVENT本轮不判(写集合后可解) / 换装限已解锁 / SyncLevelUnlocks补集合去重
- **C1-C2** 头像表:直读.bytes行数·按id查字段==表填值 / AvatarConfigMgr桥接GetAvatar·GetByType
- **D1-D2** CreateDefault缺省字段对 / id不随改名变
- **S1-S3** 持久化往返保真 / 旧档缺玩家字段保底不抛不丢既有 / 篡改值夹合法
- **B1** 剪贴板可注入断言(注内存sink,Copy后捕获==Id,空不写,测后还原)
- **Z1-Z2** 全链纯逻辑无ConfigSystem(N/R/P/L/U非C/D/S/B) / 既有251例EditMode零回归·编译0error

- **BLOCKED 条件**:(1) 导表工具链不可达(本机缺 .NET7/未配 DOTNET_ROLL_FORWARD,遗留#18)致 avatar_tbavatar.bytes 无法导出 → C1/C2 判 BLOCKED 不判 FAIL,纯逻辑验收照跑;(2) unityMCP 桥不可达致 EditMode 跑不起(no_session)→ BLOCKED,可备选 batchmode 跑 EditMode(boss memory)

### 待 boss/用户裁决(范围开关,不阻塞,均按本轮默认推进)
- O1 UI 表现层全部延后(默认√,需美术)
- O2 头像/框真实 Sprite 加载(默认延后,只给资源名占位)
- O3 解锁条件 type2 活动发放(默认留钩子)
- O4 多语言文本真实查表(默认延后,存 text id)
- O5 头像/框品质显示(默认不做;avatar 表无 quality 字段;要做则复用 RewardDisplay.QualityColor + 加字段)
- O6 屏蔽字真实词表来源 + 匹配策略增强(默认可注入夹具)
- O7 玩家经验来源接入 + 等级奖励内容(默认不接,给容器+换算+AddExp+占位结构)
- O8 钻石可花费余额(默认 no-op,接缝就绪;待钻石实装接真实扣减)

上一单 reward-display 已于 2026-06-14 关单 PASS,归档 `pipeline/archive/2026-06-14-reward-display/plan.md`。
