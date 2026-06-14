# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区(角色职责在 `.claude/agents/pipeline-test.md`,spawn 时自动注入)。每完成一项验证就更新这里。

## 当前被测任务:mail 通用邮件系统·数据逻辑层 + 服务器/运营接缝

总判定:**PASS**

验收锚:`design-docs/21-mail-system.html` §六(25 条)。四类验证逐项通过,五红线 Code Review 无违规,dev 改过的持久文件交叉检通过。Unity 实例 `UnityProject@02a6dcaa`,unity-check 三步自检通过。

### 1. 编译验证 — PASS

- `refresh_unity(compile=request, mode=force)` → 编辑器返回 ready,`manage_scene get_active` 响应正常(域重载已稳定)。
- `read_console(types=error)`:0 条。`read_console(types=warning, filter=Mail)`:0 条。
- 结论:编译 0 报错 0 警告(Mail 相关)。GameProto(TbMail/TbMailGlobal lazy 属性)+ GameLogic + Tests 全程编译通过。

### 2. 单元测试 — PASS

- `run_tests(EditMode, assembly=BlockBlast.Tests, include_failed_tests)`,job `a0b8b38d`:**335/335 passed,0 failed,0 skipped**,耗时 0.38s,resultState=Passed。原 311 套零回归 + 24 个 Mail 新测全绿。
- C3 单独复跑(job `ea50658b`,include_details):`state: Passed`(非 Inconclusive/Skipped)。即 `mail_tbmail.bytes`/`mail_tbmailglobal.bytes` 经真实生成类 `GameConfig.mail.TbMail`/`TbMailGlobal` 解码,断言 5 行 + reward=1002 + expire=14 + 全局 100/30 全中。
  - 此 GREEN 同时构成源 xlsx → bytes 一致性的强交叉核验:bytes 由真实 Luban 类解出且字段全对,证明导表布局正确(无字节错位)。**故 C3 列 PASS,不列 BLOCKED**(工具链可达,bytes 已导入可读)。
- 25 条验收 → 24 测试方法映射完整:C1-3 / N1-2 / SO1-2 / RD1 / CL1-4 / DEL1-2 / CU1-3 / RDOT1-2 / P1-2 / SK1-2 + MailText 占位互异;R1(编译+回归)/R2(Code Review)是判定项非独立方法。无测试覆盖缺口。

### 3. 手动功能验证 — N/A(本层无 Play 手验面)

- 本轮交付为纯逻辑数据层 + 配置桥接 + 两道接缝,**零 UI 改动**(文件清单 + git status 确认:无 GameObject/prefab/窗口工作)。邮件界面/详情/红点显示/icon 为表现层,设计稿 §七 O7 明确延后轮 + 美术。
- 全部行为已被 24 例 EditMode + C3 Luban 直读覆盖(注入 NowProvider 时钟、注入 InMemory 持久化、state 可 null 纯解析路径),无运行期 UI 交互面需 Play 模式核对。无拖拽/手势类验证点。

### 4. Code Review — PASS(5 红线逐条核)

证据均为对 `Module/Mail/` + `Config/MailConfigMgr.cs` 的 grep + 源码通读:

- **异步优先 / 非阻塞 IO**:本层无 IO 异步操作(无 LoadAssetAsync);持久化走既有 `Persistence.Provider`(PlayerPrefs KV,非阻塞内存级,同 14/19/20 口径)。grep 无 `.Result`/`.Wait()`/`Thread.Sleep`/Coroutine。
- **模块访问**:grep 无 `ModuleSystem.GetModule`;本层不访问引擎模块(配置经 `ConfigSystem.Instance.Tables` 桥接,同 ItemConfigMgr 既有口径)。
- **资源释放**:本层无资源加载(grep 无 `LoadAssetAsync`/`LoadGameObjectAsync`/`Resources.Load`),无未配对释放风险。
- **热更边界**:全部源码在 `GameScripts/HotFix/GameLogic`(GameLogic asmdef)+ `GameProto/GameConfig`(Luban 生成),均热更区,无 Main 区改动。git status 确认。
- **事件解耦**:本层无 `GameEvent`/`AddUIEvent`(grep 0 命中),无事件泄漏/风暴风险。
- 重点核(R2/SK1):
  - **无真实网络**:`grep UnityWebRequest|HttpClient|WebRequest|System.Net|Socket|WWW|TcpClient|WebSocket` 在 Mail 目录 + MailConfigMgr 0 命中。`InertMailSource.Pull` 返 `Array.Empty<MailDraft>()` 不连网(SK1 测断言)。
  - **发奖复用 16 不复制落点**:`GrantPool` 调 `GiftOpener.OpenRandom(reward,1,rng)` → `ItemConfigMgr.GetItem` → `ItemGrant.GrantOnAcquire(def,num,state,rng)`,无自造「库→道具」落点(CL1 断言 state.Exp 增 = 库项 num,与 16 落点一致;CL4 断言库未登记返空仍 Success)。
  - **持久化复用既有 Provider 不另造存储栈**:`MailPersistence` 包 `GameLogic.BlockBlast.Persistence.Provider` 键 `Mail.Inbox`,无直接 `PlayerPrefs.*` 调用,无第二套存储(P1 跨实例往返断言)。
- **命名**:Mail 目录源码为命名空间/类型符号,非 UI 节点;本轮无 code-built UI 节点,naming-rules 的 `m_btn_`/`m_text_` 前缀规则 N/A。
- **风格一致性**:排序/计数用显式循环 + `List.Sort` comparer,grep 无 `System.Linq`(与 HotFix GameLogic 全栈零 Linq 一致)。

### 持久文件交叉检(conventions「交叉检」)

dev 改过的工作态文件:`pipeline/state/dev.md` 交接区。

- lint(`刚才|你说的|从.*改成|打死|挂了|收口|死在|尾巴上` 等)对 dev.md:0 命中。
- 抽查:交接区正文为平实陈述句,无指代词、无 diff 叙事、无拟人比喻;工作态内容可识别所属任务(标题明确「mail 通用邮件系统」)。
- plan.md 同检:lint 命中 line 37「try/catch 包 FromJson」的「包」非比喻(技术义=包裹),非违规。
- 结论:交叉检通过。

### 给 boss 的关单说明

- 四类全绿,无 FAIL、无 BLOCKED。代码可直接关单 PASS。
- 遗留(设计已授权,非本轮缺陷):邮件表现层 UI(界面/详情/红点显示/icon/真实 Sprite/主界面入口接线)+ 真实服务器后台发删/定时/区服多选 → 转后续表现层轮 + 远程未来轮(设计稿 §七 O7 / §六「不在本轮验收」)。
- CI/他人复跑导表须带 `DOTNET_ROLL_FORWARD=Major`(boss 遗留 #18);正式出包须 HybridCLR 重生热更 dll(boss 遗留 #2)——本轮 Editor 直跑已验证,出包步骤非本环节范围。
