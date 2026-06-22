# boss 关单总结 — block-skin-switch 方块皮肤切换

**结论:PASS**(full,plan→dev→test,client;dev 2 轮:主体 + 彩色贴图追加,均非返修)。归档日期 2026-06-22。

## 任务定义

方块两套皮肤切换:彩色(`default_skin`,8 张 `blocks_main_*` 按类型固定配色)与单色(`blocks_skin`,374 张 `blocks_skin_atlas_*`)。触发链:初始彩色 → 首次「全清(perfect clear,整盘被清空那一刻)」整盘转单色(全盘统一一张随机 sprite)→ 此后每次全清换一张、排除当前在用那张(防连续重复)。皮肤态跨会话续存。

## 用户拍板

1. 单色 = 全盘所有方块不分类型统一显示同一张随机 sprite(真单色棋盘,非整套换风格)。
2. 每次换色排除当前在用那张(防连续重复)。
3. 皮肤态需跨会话续存。
4. **「全清」定义** = 一次落子消除后整盘被清空(perfect clear),≠ 消除道具、≠ DDA「清屏窗口」难度术语(工程「清屏」多义)。
5. **彩色态也用 default_skin 8 张纹理图**(交叉检发现现行棋盘是纯色填充、从未用纹理图;否则彩色扁平/单色纹理视觉不一致)。colorIdx→blocks_main 映射(boss 看图比色定):Blue0→14 / Green1→15 / Yellow2→16 / Orange3→21 / Red4→1 / Steel5→6 / Teal6→18 / Purple7→19。

## 实现(dev)

新增纯逻辑 `BlockSkinState`(状态机 + 防相邻重复 + 退化兜底 + 续存校验)+ `BlockSkinCatalog`(候选池 374 真实编号,从源文件导出非 1..N;`ColoredIds` 彩色 8 映射)。`MergeOrderWindow.ApplyCellSkin` 两态统一贴图(白 tint + SetSprite,散 PNG 按文件名寻址 —— SpriteAtlas v2 的 SetSubSprite 不可用)。全清钩子挂 `PlaceAndResolve` 的 `settle.AllClearRewarded`(已武装全清,同女神/图案口径),纯附加不污染结算。DTO 加 2 字段;悔棋快照含皮肤态。

## 验收(test PASS)

- 编译:0 CS 报错(prefab missing-script 错误确认非本任务引入)。
- 单测:EditMode 535/535,`BlockSkinStateTests` 18/18(A1–A9 + 续存整合 + 悔棋回滚 + 映射 + 边界)。
- Play 手验:B1–B4 + 8 边界场景全过(两态 sprite 运行期真加载、无空/白块)。
- Code Review:红线 5 条 + conventions 交叉检通过。

## 关键决策(关单复核已处置)

- **续存归元层**(非 plan 标的「局内态」):现行代码无局内续存通道(设计 49 无局模型未落地),dev 归元层 DTO(单调只增成就标记语义贴合元层,满足跨会话续存)。boss 接受为当前正确。**设计稿已同步**:设计 50 §六 + 14 §3.1 皮肤行已从「局内」改「元层」+ 前向注(无局局内续存通道落地后可平移回局内段)。
- 彩色态从纯色改纹理(用户拍板),boss 看图比色定 8 映射。

## 反向引用同步(本任务内完成)

- 新增 `design-docs/50-block-skin-switch.md`(plan)+ nav/index 注册。
- 01 §三方块库 / 11 §5.4 全清奖励 NOTE / 14 §3.1 存档段(元层皮肤行)forward cross-ref。
- 关单期同步:设计 50/14 续存层「局内→元层」、设计 50 §五 资源数 337→374。

## 提交链(client,UnityProject)

- `8b247c68` — plan 设计稿 50 + 01/14 反向引用 + nav/index + 皮肤美术底料(保护性提交,隔离并发无局任务)
- `5bd112b9` — 11 §5.4 皮肤 NOTE(untangle 后补)
- `5c30ad0c` — dev 代码 + 单测 + 设计稿续存层同步(关单 checkpoint)

## 遗留事项(交 boss.md carry-forward)

- **续存层迁移**:无局·无尽任务(设计 49,已暂停 commit `19303cdc`+`7a8441d1`)落地局内续存通道后,皮肤态可从元层平移回局内整盘续存(DTO 字段语义不变)。
- **资源偏差(非本任务引入)**:`blocks_skin/README.md` 标 337 张实际 374、且把实有文件的 99-114/116-136 段标「留空」;`Atlas_blocks_blocks_main` 图集只打包 6 sprite(实有 8)。本实现走散 PNG 寻址不依赖图集打包,不阻塞;留单开修文档/图集。
- **图集 vs 散 PNG**:两套皮肤运行期都走散 PNG 按文件名寻址(SpriteAtlas v2 SetSubSprite 不可用),Atlas_blocks_blocks_skin/main 图集实际未被本功能使用,如需图集化渲染另开。
