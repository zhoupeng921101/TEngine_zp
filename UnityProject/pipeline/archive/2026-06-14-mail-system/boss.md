# 归档:mail 通用邮件系统·数据逻辑层 + 服务器/运营接缝

**结论:PASS 交付**。自治·放手默认。设计基线 `design-docs/21-mail-system.html`(§六 25 条验收)。来源 spec `1002通用邮件系统.xlsx`。

> **本轮分两段执行**(撞会话用量上限):① plan 段经 full baton 工作流(Run `wf_ebcd4971-61e`)写完设计稿 21 + nav.js + state/plan.md 交接区后,撞会话用量上限(20:30 上海重置)被中断,dev/test 未跑,返回 BLOCKED@plan;plan 产出提交为基线 commit `367c080d`。② 用量恢复后用 dev-test baton 续接(Run `wf_4f194fad-5f3`,baseline=design-docs/21),dev+test 一轮过,返回 PASS round 0。
>
> **首条假 PASS 教训**:plan 段那次工作流先返回过一条「PASS 333/333」错误通知(磁盘零文件、与第二条 usage 数字对不上),数分钟后才更正为真实的 BLOCKED。boss 关单前看 git status 起疑拦下。由此给关单事务加了「核磁盘交付物」硬前置闸(commit `ef4b5af2`,SKILL.md 关单第 1 步 + audit-log 登记)。本轮 PASS 经该闸核验:Mail 模块 5 源文件 + MailConfigMgr + MailSystemTests + mail/mail_global.xlsx + 两 .bytes + GameProto 行类/表类全部在盘,与 dev.md 清单一致。

## 关键决策(均与设计稿默认一致,无抵触 GDD/spec)

- **两道接缝分清**:`IMailService.Send`(游戏内对外发件 API,本轮真实实现,下轮排行榜结算接它)vs `IMailSource`(外部服务器/运营推送,`InertMailSource.Pull` 返空不连网 stub)。Code Review grep 无 `UnityWebRequest`/`HttpClient`/`System.Net`/`Socket` 等,零真实网络。
- **发奖复用 16**:奖励附件 = 礼包随机库 id(spec「Reward表id=奖励随机库表id」),经 `GiftOpener.OpenRandom(reward_id,1,rng)` → `ItemConfigMgr.GetItem` → `ItemGrant.GrantOnAcquire`,不另造落点。
- **持久化复用既有 Provider**:`MailPersistence` 包 `Persistence.Provider` 键 `Mail.Inbox`,JsonUtility 序列化 `MailInboxSave` 容器,无键/空串/非法 JSON 产空列表不抛;不另造存储栈。
- **时钟注入** `NowProvider`:有效期/过期清理/收件时间戳走注入时钟,EditMode 可控时间断言。
- **自动清理两条**:过期(SendTime+有效期 < now,有效期 = `ExpireDays>0?ExpireDays:Global.RetainDays`)+ 超量(>MaxCount 删最早留最新 N);收件后 + 列表前 + 红点查询前 + ClaimAll 前触发。`MaxCount<0` 视作不限(防误配清空),`==0` 清空。
- **领取顺序**:抽奖落点成功 → 标已领+已读 → 落盘;二次领 `AlreadyClaimed` 不重发。`ClaimAll` 先清过期、有奖未领未过期全领、全部邮件标已读。删除前置 `CanDelete = Read && (!HasReward || Claimed)`。
- **排序**:未读置顶(`Read?1:0` 升序),组内 `SendTimeTicks` 降序;用 `List.Sort` comparer 不引 `System.Linq`(HotFix GameLogic 全栈零 Linq,保持风格 + 避 AOT 顾虑)。
- **红点**:`HasUnreadOrUnclaimed`/`UnreadCount`/`UnclaimedCount` 派生 getter,不进盘。
- **本地 id 分配** `NextId = max(now.Ticks, lastId+1)` 单调自增(防同 tick 冲突 + 重启续号);未来服务器邮件用服务器全局 id(O8)。
- **单行全局配置表**用普通 Luban map 表 + id 固定 1,桥接取 `DataList[0]`、表空 getter `??=` 兜底默认 100/30(可单测「表缺省返默认」)。
- **命名空间** `GameLogic.Mail`(同 Redeem/Settings),配置桥接 `MailConfigMgr` 归 `GameLogic.Config`;Luban 行类 `GameConfig.Mail` 同名经全限定消歧。文案 textId 占位 110701-110705/标题 110711-110715/内容 110721-110725(互异)。

## 测试结论(test agent)

- 编译:`read_console(error)` 0 条、`(warning, Mail)` 0 条;GameProto + GameLogic + Tests 全编译通过。
- 单测:`run_tests(EditMode, BlockBlast.Tests)` job `a0b8b38d` → **335/335 passed,0 failed,0 skipped**(原 311 零回归 + 24 个 Mail 新测)。C3 单独复跑 job `ea50658b` include_details → `state: Passed`(mail_tbmail/mail_tbmailglobal.bytes 经真实生成类解码,5 行 + reward=1002 + expire=14 + 全局 100/30 断言全中)。
- 25 条验收 → 24 测试方法映射完整(C1-3/N1-2/SO1-2/RD1/CL1-4/DEL1-2/CU1-3/RDOT1-2/P1-2/SK1-2 + MailText 占位互异;R1/R2 为判定项)。
- 手验第 3 类 N/A:纯逻辑 + 配置 + 接缝,零 UI 改动,全被 24 例 EditMode + C3 覆盖,无 Play 手验面。
- Code Review 5 红线逐条过;持久文件交叉检 dev.md 0 命中(plan.md「包 FromJson」的「包」为技术义包裹,非比喻)。

## 遗留(转主 boss.md #26)

邮件表现层 UI(界面/详情/无邮件态/全部删除确认/红点显示/icon/真实 Sprite/主界面入口接线)→ 表现层延后轮 + 美术;真实服务器后台发删/定时邮件 + 区服多选 → 远程未来轮(无网络模块);多语言文案真实查表(textId 占位)。排行榜结算届时接本轮 `IMailService.Send`。
