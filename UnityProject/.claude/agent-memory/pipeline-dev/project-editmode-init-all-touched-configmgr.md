---
name: project-editmode-init-all-touched-configmgr
description: EditMode 测试链路触达 ConfigSystem.Tables 的所有系统都要 InitForTest(哪怕空表),否则抛 YooAsset Default package is null。
metadata:
  type: project
---

EditMode 测试里只要调用链最终触达 `ConfigSystem.Instance.Tables`(典型:跨系统复用别的系统入口,如排行榜结算调 `GameLogic.Mail.MailDraft.FromTemplate` → `MailConfigMgr.GetMail` → `EnsureLoaded`),就必须先给那个系统的 `XxxConfigMgr.InitForTest` 注入(哪怕空表 `Array.Empty<...>`),否则抛 `System.Exception: Default package is null. Please use SetDefaultPackage!`(YooAsset 在 EditMode 未初始化)。自己系统的 ConfigMgr 注入了不够,被调系统的也要注;空表注入即可让 `FromTemplate` 返 null 走兜底草稿路径,正好顺带覆盖「模板缺省兜底」验收。

**Why:** EditMode 不跑游戏启动流程,YooAsset 未 SetDefaultPackage,任何穿透到 `Tables` 的调用都会抛 NPE 类异常;自己系统注入但跨系统调用对方未注入,失败点远离测试主题不易诊断(2026-06,rank)。

**How to apply:** EditMode 测试 SetUp:① grep 被测代码所有 ConfigMgr 引用,列清单;② 每个 ConfigMgr 都 `InitForTest(Array.Empty<...>)` 或喂样本数据;③ 失败时报错指向 `Default package is null` 即说明漏注;④ 空表注入还顺便覆盖「兜底草稿/默认值」验收路径。
