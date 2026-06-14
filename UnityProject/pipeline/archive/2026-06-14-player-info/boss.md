# 关单总结:player-info — 玩家信息系统·数据逻辑层(2026-06-14;自治模式·放手默认)

**结论:PASS 交付(数据逻辑层;UI 表现层转遗留)**。baton=full(plan→dev→test),**打回 0**。git 基线 `863c3b76`。设计基线 `design-docs/18-player-info.html`(§六 30 验收点)。源 spec `C:\Users\pc\Downloads\1001玩家信息系统.xlsx`。

## 交付范围
数据逻辑层(此处是 spec 主体,非薄归一):玩家档案模型 + Luban 头像表 + 一组无状态纯逻辑服务(名字生成 / 改名 / 屏蔽字 / 解锁三态 / 等级曲线 / 剪贴板),全 EditMode 可测;持久化增量并入既有 MergeMetaSave。UI 窗口(玩家信息界面 / 改名界面 / 三态网格 / 经验槽 / 奖励预览 / 主界面入口)= 表现层延后(需美术),只留服务 + 入口 TODO 钩子。

## 运行验证(test 经 MCP,UnityProject@02a6dcaa)
- 编译 0 error;EditMode `BlockBlast.Tests` **279/279**(251 基线 + 28 新 PlayerInfoTests),零回归(job e79c4c9c;28 例逐例 state=Passed,非静默跳过)。
- 生产真实路径验证:进 Play 反射调 `ConfigSystem.Instance.Tables.TbAvatar`(YooAsset 真实加载),DataList.Count=6,6 行字段逐字段比对源 avatar.xlsx 一致,无夹具/源漂移。
- Code Review 5 红线全过;`git diff --stat` 核实 MergeOrderState/ItemGrant/NumericConfigMgr/ItemConfigMgr/RewardDisplay 一行未改,MergeMetaSave 纯追加 8 玩家字段(既有 15 字段零改、CurrentVersion 不升)。

## 实现范围(加法式,既有玩法/数值/道具/存档路径只读)
- C1 Luban 头像表 `avatar.TbAvatar`(6 字段 id/type/image/unlock_text/unlock_cond/unlock_param)+ 2 枚举(EAvatarType 头像1·框2 / EUnlockCond 等级1·活动2)→ 导表生成 + avatar_tbavatar.bytes(DOTNET_ROLL_FORWARD=Major 直调 dotnet 绕 .bat pause)。
- C2 `AvatarConfigMgr` + POCO `AvatarEntry`(Type/UnlockCond 存 int,不引生成代码,保解锁服务纯逻辑)。
- C3 `PlayerInfo`(档案 POCO + 默认头像/框常量 + NewId Guid + CreateDefault)。
- C4 `PlayerNameGenerator`(Player+6 随机/62 字符集)/ `ProfanityFilter`(大小写不敏感子串、空表不拦、词表可注入)/ `RenamePriceConfig`(固定价 100)/ `PlayerRenameService.TryRename`(判定序 合法→屏蔽字→计费→写名;trySpendDiamond 接缝外置)。
- C5 `PlayerLevelConfig`(BASE=100/STEP=50/MAX=60 闭式)+ `PlayerExpService.AddExp`(只增)+ 等级奖励占位接口。
- C6 `AvatarUnlockService`(解锁判定 / 三态 Locked·Unlocked·Equipped / SyncLevelUnlocks 去重 / TryEquip 限已解锁 / GrantUnlock 活动钩子)。
- C7 `ClipboardUtil`(可注入 Sink,默认写 GUIUtility.systemCopyBuffer)。
- M1 MergeMetaSave 加 8 玩家平铺字段;Export/Import + 逐字段保底做成 `PlayerInfo` 纯静态方法(不挂 MergeOrderState/悔棋快照,避耦合无关域)。
- H1 MainMenuWindow 入口 TODO 注释钩子。T1 PlayerInfoTests 28 例。

## 关键拍板(boss 授权 + plan/dev 自主,均安全默认无 spec/GDD 抵触)
- 玩家等级 = 独立第三进度线(新 PlayerInfo.Exp + PlayerLevelConfig),不复用守护者经验(语义不同,复用会混淆两套等级)。
- 离线/去变现适配:id 本地生成 Guid(无服务器)、账号绑定不做、type2 活动发放留钩子、改名直接生效、屏蔽字逻辑+可注入词表、钻石扣费经 trySpendDiamond 接缝(生产默认返 true,钻石未实装为可花费余额时为 no-op)。
- 持久化挂载点:dev 把 plan 字面要求的「挂 MergeOrderState.Export/ImportMeta」改为 PlayerInfo 纯静态方法(该状态机不持有 PlayerInfo,强挂会污染悔棋 Snapshot)——设计稿 §2.2 已显式认可,非偏离 spec。
- 头像表补 unlock_param int 字段(LEVEL 解锁须知哪一级,spec 隐含必需,同 numeric 给 num 补 func_name)。

## 模型档 / 运行登记
- plan/dev/test=opus。Run `wf_aab6f6a6-581`(full,round 0)PASS。本轮 plan 把全部范围开关放进 decisions、未塞 blockers——plan 空停根治(commit 31e5e37b)首次实战验证,流水线一路 plan→dev→test 无空停。

## 遗留/观察(转主 boss.md)
- player-info **UI 表现层**:6 个窗口/入口未投放,阻塞于美术 + 需接真实 UI 流程,基于本层服务驱动。
- player-info **数据层接线待补**:type2 活动发放解锁(钩子就绪、无活动系统)/ 钻石可花费余额(trySpendDiamond no-op、待数值系统实装钻石)/ 屏蔽字真实词表(可注入、当前空表不拦)/ 多语言文本表(存 text id、真实查表延后)/ 玩家经验真实来源接入 + 等级奖励内容(容器+曲线就绪、未接来源)。均「接 UI / 对应系统建好时做」,数据层不返工,低优。
