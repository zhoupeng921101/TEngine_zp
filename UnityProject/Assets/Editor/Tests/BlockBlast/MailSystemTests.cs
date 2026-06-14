using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GameLogic.Mail;
using GameLogic.Config;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Item;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 通用邮件系统测试（设计 21 §六，25 条验收点）。
    /// 纯逻辑全 EditMode 可测（POCO + InitForTest + InMemoryMailPersistence + 注入 NowProvider + state 可 null）；
    /// Luban 直读 C3 按工具链可达性（不可达列 BLOCKED 不判 FAIL）。
    /// 发奖复用 16：奖励库 id → GiftOpener.OpenRandom → ItemConfigMgr.GetItem → ItemGrant.GrantOnAcquire。
    /// </summary>
    [TestFixture]
    public class MailSystemTests
    {
        // ── 测试夹具：奖励库 + 道具 ─────────────────────────────
        // 库 id 1002（demo reward）含一项货币道具 30001（Exp），UseNum=1 使产出 Amount == 抽中 num。
        private const int RewardPool = 1002;
        private const int ItemExp = 30001;    // use_effect=1 → Numeric, use_value=Exp
        private const int RewardNum = 50;     // 库项数量

        private static ItemDef NumericItem(int id, int numId)
            => new ItemDef { Id = id, Automatic = 1, UseEffect = 1, UseValue = numId, UseNum = 1, UseLevel = 0 };

        // 灌入道具 + 礼包随机库（库 1002 → 道具 30001 ×RewardNum，权重 1）
        private void SeedRewards()
        {
            ItemConfigMgr.InitForTest(
                new[] { NumericItem(ItemExp, NumericConfigMgr.Exp) },
                randoms: new[] { (RewardPool, new GiftEntry { ItemId = ItemExp, Num = RewardNum, Rate = 1 }) });
        }

        private static MailDraft Draft(int reward = 0, int expireDays = 14, int title = 1, int body = 1, int sender = 9)
            => new MailDraft { SenderTextId = sender, TitleTextId = title, BodyTextId = body, ExpireDays = expireDays, RewardPoolId = reward };

        private static MailboxService NewService(IMailPersistence persist, DateTime now)
            => new MailboxService(persist) { NowProvider = () => now, RngProvider = () => new System.Random(12345) };

        private static readonly DateTime T0 = new DateTime(2026, 6, 14, 12, 0, 0);

        [SetUp]
        public void SetUp()
        {
            MailConfigMgr.ResetForTest();
            ItemConfigMgr.ResetForTest();
        }

        [TearDown]
        public void TearDown()
        {
            MailConfigMgr.ResetForTest();
            ItemConfigMgr.ResetForTest();
        }

        private static MailItem FindIn(IReadOnlyList<MailItem> list, long id)
        {
            foreach (var m in list) if (m.Id == id) return m;
            return null;
        }

        // ════════════ 配置 C ════════════

        [Test] // C1：InitForTest 灌入后 GetMail 返对应 def，字段正确；查不存在 id 返 null（不抛）
        public void C1_ConfigMgr_GetReturnsDef_MissReturnsNull()
        {
            MailConfigMgr.InitForTest(new[]
            {
                new MailDef { Id = 1, TitleTextId = 1001, BodyTextId = 2001, ExpireDays = 14, RewardPoolId = 1002 },
            });
            var def = MailConfigMgr.GetMail(1);
            Assert.IsNotNull(def);
            Assert.AreEqual(1001, def.TitleTextId);
            Assert.AreEqual(2001, def.BodyTextId);
            Assert.AreEqual(14, def.ExpireDays);
            Assert.AreEqual(1002, def.RewardPoolId);
            Assert.IsNull(MailConfigMgr.GetMail(999));
        }

        [Test] // C2：Global 表缺省返默认 100/30；注入自定义返注入值
        public void C2_Global_DefaultWhenMissing_InjectedOtherwise()
        {
            MailConfigMgr.InitForTest(Array.Empty<MailDef>()); // global=null → 兜底默认
            Assert.AreEqual(100, MailConfigMgr.Global.MaxCount);
            Assert.AreEqual(30, MailConfigMgr.Global.RetainDays);

            MailConfigMgr.InitForTest(Array.Empty<MailDef>(), new MailGlobalConfig { MaxCount = 5, RetainDays = 7 });
            Assert.AreEqual(5, MailConfigMgr.Global.MaxCount);
            Assert.AreEqual(7, MailConfigMgr.Global.RetainDays);
        }

        [Test] // C3：Luban 直读 mail.bytes 含 5 条 demo 行且字段映射正确（工具链不可达 → Inconclusive=BLOCKED，不判 FAIL）
        public void C3_LubanDirectRead_MailBytes_HasFiveDemoRows()
        {
            const string mailPath = "Assets/AssetRaw/Configs/bytes/mail_tbmail.bytes";
            const string globalPath = "Assets/AssetRaw/Configs/bytes/mail_tbmailglobal.bytes";
            var taMail = AssetDatabase.LoadAssetAtPath<TextAsset>(mailPath);
            var taGlobal = AssetDatabase.LoadAssetAtPath<TextAsset>(globalPath);
            if (taMail == null || taGlobal == null)
                Assert.Inconclusive($"找不到 {mailPath} / {globalPath}，Luban 导表工具链不可达 → BLOCKED（非 FAIL，纯逻辑条已覆盖）");

            var tbMail = new GameConfig.mail.TbMail(new Luban.ByteBuf(taMail.bytes));
            var tbGlobal = new GameConfig.mail.TbMailGlobal(new Luban.ByteBuf(taGlobal.bytes));
            Assert.AreEqual(5, tbMail.DataList.Count, "邮件模板表应有 5 条 demo 行");
            for (int id = 1; id <= 5; id++)
            {
                var row = tbMail.DataList.Find(r => r.Id == id);
                Assert.IsNotNull(row, $"缺 demo 行 id={id}");
                Assert.AreEqual(14, row.ExpireDays, $"id={id} expire_days 应=14");
                Assert.AreEqual(1002, row.RewardId, $"id={id} reward_id 应=1002");
            }
            Assert.Greater(tbGlobal.DataList.Count, 0, "全局配置表应有 1 行");
            Assert.AreEqual(100, tbGlobal.DataList[0].MaxCount);
            Assert.AreEqual(30, tbGlobal.DataList[0].RetainDays);
        }

        // ════════════ 收件 N ════════════

        [Test] // N1：Send 后 +1，新邮件未读未领，SendTimeTicks==注入 now，返回 id 能 Find
        public void N1_Send_AddsUnreadUnclaimed_FindableById()
        {
            MailConfigMgr.InitForTest(Array.Empty<MailDef>());
            var svc = NewService(new InMemoryMailPersistence(), T0);
            long id = svc.Send(Draft(reward: 0));
            Assert.AreEqual(1, svc.Count);
            var m = svc.Find(id);
            Assert.IsNotNull(m);
            Assert.IsFalse(m.Read);
            Assert.IsFalse(m.Claimed);
            Assert.AreEqual(T0.Ticks, m.SendTimeTicks);
        }

        [Test] // N2：FromTemplate 从配置复制字段；Send 进收件箱
        public void N2_FromTemplate_CopiesFields_SendEnqueues()
        {
            MailConfigMgr.InitForTest(new[]
            {
                new MailDef { Id = 3, TitleTextId = 1003, BodyTextId = 2003, ExpireDays = 14, RewardPoolId = 1002 },
            });
            var draft = MailDraft.FromTemplate(3, senderTextId: 9);
            Assert.IsNotNull(draft);
            Assert.AreEqual(1003, draft.TitleTextId);
            Assert.AreEqual(2003, draft.BodyTextId);
            Assert.AreEqual(14, draft.ExpireDays);
            Assert.AreEqual(1002, draft.RewardPoolId);

            var svc = NewService(new InMemoryMailPersistence(), T0);
            long id = svc.Send(draft);
            Assert.AreEqual(1002, svc.Find(id).RewardPoolId);

            Assert.IsNull(MailDraft.FromTemplate(999, 9), "模板不存在返 null（不抛）");
        }

        // ════════════ 排序 SO ════════════

        [Test] // SO1：未读排已读前；组内时间降序（新在前）
        public void SO1_List_UnreadFirst_NewerFirstInGroup()
        {
            MailConfigMgr.InitForTest(Array.Empty<MailDef>());
            var svc = new MailboxService(new InMemoryMailPersistence()) { RngProvider = () => new System.Random(1) };
            // 三封不同时间
            svc.NowProvider = () => T0.AddMinutes(0); long a = svc.Send(Draft(0)); // 早
            svc.NowProvider = () => T0.AddMinutes(10); long b = svc.Send(Draft(0)); // 中
            svc.NowProvider = () => T0.AddMinutes(20); long c = svc.Send(Draft(0)); // 晚
            svc.NowProvider = () => T0.AddMinutes(30);
            svc.MarkRead(b); // b 已读

            var list = svc.List();
            Assert.AreEqual(3, list.Count);
            // 未读组（c 晚, a 早）在前，已读组（b）在后 → [c, a, b]
            Assert.AreEqual(c, list[0].Id);
            Assert.AreEqual(a, list[1].Id);
            Assert.AreEqual(b, list[2].Id);
        }

        [Test] // SO2：2 未读 + 2 已读（各含早晚）→ [未读新, 未读旧, 已读新, 已读旧]
        public void SO2_List_MixedReadUnread_OrderedCorrectly()
        {
            MailConfigMgr.InitForTest(Array.Empty<MailDef>());
            var svc = new MailboxService(new InMemoryMailPersistence()) { RngProvider = () => new System.Random(1) };
            svc.NowProvider = () => T0.AddMinutes(0); long readOld = svc.Send(Draft(0));
            svc.NowProvider = () => T0.AddMinutes(10); long unreadOld = svc.Send(Draft(0));
            svc.NowProvider = () => T0.AddMinutes(20); long readNew = svc.Send(Draft(0));
            svc.NowProvider = () => T0.AddMinutes(30); long unreadNew = svc.Send(Draft(0));
            svc.NowProvider = () => T0.AddMinutes(40);
            svc.MarkRead(readOld);
            svc.MarkRead(readNew);

            var list = svc.List();
            Assert.AreEqual(unreadNew, list[0].Id, "未读新");
            Assert.AreEqual(unreadOld, list[1].Id, "未读旧");
            Assert.AreEqual(readNew, list[2].Id, "已读新");
            Assert.AreEqual(readOld, list[3].Id, "已读旧");
        }

        // ════════════ 已读 RD ════════════

        [Test] // RD1：MarkRead 后 Read==true；幂等；UnreadCount 减少
        public void RD1_MarkRead_SetsRead_Idempotent_UnreadCountDrops()
        {
            MailConfigMgr.InitForTest(Array.Empty<MailDef>());
            var svc = NewService(new InMemoryMailPersistence(), T0);
            long id = svc.Send(Draft(0));
            Assert.AreEqual(1, svc.UnreadCount);
            svc.MarkRead(id);
            Assert.IsTrue(svc.Find(id).Read);
            Assert.AreEqual(0, svc.UnreadCount);
            Assert.DoesNotThrow(() => svc.MarkRead(id)); // 幂等
            Assert.IsTrue(svc.Find(id).Read);
        }

        // ════════════ 领取 CL ════════════

        [Test] // CL1：有奖励 Claim(state!=null) → Success+Granted 含产出；邮件 Claimed&&Read
        public void CL1_Claim_WithReward_Success_LandsAndMarks()
        {
            SeedRewards();
            MailConfigMgr.InitForTest(Array.Empty<MailDef>());
            var svc = NewService(new InMemoryMailPersistence(), T0);
            long id = svc.Send(Draft(reward: RewardPool));
            var state = new MergeOrderState();

            var r = svc.Claim(id, state);
            Assert.AreEqual(ClaimStatus.Success, r.Status);
            Assert.Greater(r.Granted.Count, 0);
            Assert.AreEqual(RewardNum, state.Exp, "落点：Exp 增 = 库项 num");
            var m = svc.Find(id);
            Assert.IsTrue(m.Claimed);
            Assert.IsTrue(m.Read, "领后标已读");
        }

        [Test] // CL2：二次领 AlreadyClaimed 不重发；无奖励 NoReward；不存在 NotFound
        public void CL2_Claim_SecondAlreadyClaimed_NoReward_NotFound()
        {
            SeedRewards();
            MailConfigMgr.InitForTest(Array.Empty<MailDef>());
            var svc = NewService(new InMemoryMailPersistence(), T0);
            long withReward = svc.Send(Draft(reward: RewardPool));
            long noReward = svc.Send(Draft(reward: 0));
            var state = new MergeOrderState();

            Assert.AreEqual(ClaimStatus.Success, svc.Claim(withReward, state).Status);
            int expAfterFirst = state.Exp;
            var second = svc.Claim(withReward, state);
            Assert.AreEqual(ClaimStatus.AlreadyClaimed, second.Status);
            Assert.AreEqual(0, second.Granted.Count, "二次不发奖");
            Assert.AreEqual(expAfterFirst, state.Exp, "Exp 不再增");

            Assert.AreEqual(ClaimStatus.NoReward, svc.Claim(noReward, state).Status);
            Assert.AreEqual(ClaimStatus.NotFound, svc.Claim(999999, state).Status);
        }

        [Test] // CL3：ClaimAll 领所有有奖未领未过期；全部邮件标已读；Granted 汇总；已领/无奖不重发
        public void CL3_ClaimAll_GrantsUnclaimed_MarksAllRead()
        {
            SeedRewards();
            MailConfigMgr.InitForTest(Array.Empty<MailDef>());
            var svc = NewService(new InMemoryMailPersistence(), T0);
            long r1 = svc.Send(Draft(reward: RewardPool));
            long r2 = svc.Send(Draft(reward: RewardPool));
            long noReward = svc.Send(Draft(reward: 0));
            var state = new MergeOrderState();
            // 先单领 r1
            svc.Claim(r1, state);
            int expAfterR1 = state.Exp;

            var all = svc.ClaimAll(state);
            Assert.AreEqual(ClaimStatus.Success, all.Status);
            // 仅 r2 被一键领（r1 已领，noReward 无奖）→ 汇总 = r2 的产出
            Assert.Greater(all.Granted.Count, 0);
            Assert.AreEqual(expAfterR1 + RewardNum, state.Exp, "仅 r2 再发一次");
            Assert.IsTrue(svc.Find(r2).Claimed);
            // 全部邮件标已读（含无奖励）
            Assert.IsTrue(svc.Find(r1).Read);
            Assert.IsTrue(svc.Find(r2).Read);
            Assert.IsTrue(svc.Find(noReward).Read);
            Assert.AreEqual(0, svc.UnreadCount);
        }

        [Test] // CL4：state==null 仍 Success 且 Granted 含结构；库未登记 → Granted 空但仍 Success
        public void CL4_NullState_StillSuccess_UnregisteredPool_EmptyButSuccess()
        {
            SeedRewards();
            MailConfigMgr.InitForTest(Array.Empty<MailDef>());
            var svc = NewService(new InMemoryMailPersistence(), T0);
            long ok = svc.Send(Draft(reward: RewardPool));
            long unregistered = svc.Send(Draft(reward: 7777)); // 库未登记

            ClaimResult r1 = default;
            Assert.DoesNotThrow(() => r1 = svc.Claim(ok, null));
            Assert.AreEqual(ClaimStatus.Success, r1.Status);
            Assert.Greater(r1.Granted.Count, 0, "纯解析路径仍产出结构");

            ClaimResult r2 = default;
            Assert.DoesNotThrow(() => r2 = svc.Claim(unregistered, null));
            Assert.AreEqual(ClaimStatus.Success, r2.Status, "库未登记仍 Success");
            Assert.AreEqual(0, r2.Granted.Count, "Granted 空，不抛");
        }

        // ════════════ 删除 DEL ════════════

        [Test] // DEL1：已读+无奖励 → 删成功；已读+奖励已领 → 删成功
        public void DEL1_DeleteRead_ReadNoReward_ReadClaimed_Success()
        {
            SeedRewards();
            MailConfigMgr.InitForTest(Array.Empty<MailDef>());
            var svc = NewService(new InMemoryMailPersistence(), T0);
            long noReward = svc.Send(Draft(reward: 0));
            long withReward = svc.Send(Draft(reward: RewardPool));
            svc.MarkRead(noReward);
            svc.Claim(withReward, new MergeOrderState()); // 已读+已领

            int before = svc.Count;
            Assert.IsTrue(svc.DeleteRead(noReward));
            Assert.IsTrue(svc.DeleteRead(withReward));
            Assert.AreEqual(before - 2, svc.Count);
        }

        [Test] // DEL2：未读 → false 不删；已读+有奖未领 → false 不删
        public void DEL2_DeleteRead_Unread_ReadUnclaimed_Rejected()
        {
            SeedRewards();
            MailConfigMgr.InitForTest(Array.Empty<MailDef>());
            var svc = NewService(new InMemoryMailPersistence(), T0);
            long unread = svc.Send(Draft(reward: 0));
            long readUnclaimed = svc.Send(Draft(reward: RewardPool));
            svc.MarkRead(readUnclaimed); // 已读但奖励未领

            Assert.IsFalse(svc.DeleteRead(unread), "未读拒绝删");
            Assert.IsFalse(svc.DeleteRead(readUnclaimed), "有奖未领拒绝删");
            Assert.AreEqual(2, svc.Count);
        }

        // ════════════ 清理 CU ════════════

        [Test] // CU1：过期 SendTime+ExpireDays<today → 移除；未过期保留；ExpireDays<=0 用全局 RetainDays
        public void CU1_CleanupExpired_RemovesPastDue_KeepsFresh_UsesRetainDefault()
        {
            MailConfigMgr.InitForTest(Array.Empty<MailDef>(), new MailGlobalConfig { MaxCount = 100, RetainDays = 30 });
            var persist = new InMemoryMailPersistence();
            var svc = NewService(persist, T0);
            long expire14 = svc.Send(Draft(reward: 0, expireDays: 14));   // T0 + 14 天过期
            long expireDefault = svc.Send(Draft(reward: 0, expireDays: 0)); // 用全局 30 天

            // 推到 T0 + 20 天：expire14 过期、expireDefault（30 天）未过期
            svc.NowProvider = () => T0.AddDays(20);
            var list = svc.List(); // List 前清过期
            Assert.IsNull(FindIn(list, expire14), "14 天的应过期移除");
            Assert.IsNotNull(FindIn(list, expireDefault), "默认 30 天的应保留");
        }

        [Test] // CU2：连发 > MaxCount → 稳定在 MaxCount，留最新（删最早）
        public void CU2_CleanupOverflow_StaysAtMaxCount_KeepsNewest()
        {
            MailConfigMgr.InitForTest(Array.Empty<MailDef>(), new MailGlobalConfig { MaxCount = 3, RetainDays = 3650 });
            var svc = new MailboxService(new InMemoryMailPersistence()) { RngProvider = () => new System.Random(1) };
            var ids = new List<long>();
            for (int i = 0; i < 5; i++)
            {
                int day = i;
                svc.NowProvider = () => T0.AddDays(day);
                ids.Add(svc.Send(Draft(reward: 0, expireDays: 3650)));
            }
            Assert.AreEqual(3, svc.Count, "稳定在 MaxCount=3");
            // 留最新 3（ids[2],[3],[4]）；最早两封被删
            Assert.IsNull(svc.Find(ids[0]));
            Assert.IsNull(svc.Find(ids[1]));
            Assert.IsNotNull(svc.Find(ids[2]));
            Assert.IsNotNull(svc.Find(ids[4]), "最新的插尾保留");
        }

        [Test] // CU3：清理用注入 NowProvider：注入今天无过期，注入一月后全过期
        public void CU3_Cleanup_UsesInjectedClock()
        {
            MailConfigMgr.InitForTest(Array.Empty<MailDef>(), new MailGlobalConfig { MaxCount = 100, RetainDays = 30 });
            var persist = new InMemoryMailPersistence();
            // 同一存储，先用 T0 发两封 14 天邮件
            var sender = NewService(persist, T0);
            sender.Send(Draft(reward: 0, expireDays: 14));
            sender.Send(Draft(reward: 0, expireDays: 14));

            // 注入「今天」(T0)：无过期
            var today = NewService(persist, T0.AddHours(1));
            Assert.AreEqual(2, today.List().Count);

            // 注入「一月后」(T0+40 天)：全过期
            var later = NewService(persist, T0.AddDays(40));
            Assert.AreEqual(0, later.List().Count);
        }

        // ════════════ 红点 RDOT ════════════

        [Test] // RDOT1：未读 → true；有奖未领 → true；全已读且(无奖||已领) → false
        public void RDOT1_HasUnreadOrUnclaimed_StateMatrix()
        {
            SeedRewards();
            MailConfigMgr.InitForTest(Array.Empty<MailDef>());
            var svc = NewService(new InMemoryMailPersistence(), T0);

            long noReward = svc.Send(Draft(reward: 0));
            Assert.IsTrue(svc.HasUnreadOrUnclaimed, "未读 → true");
            svc.MarkRead(noReward);
            Assert.IsFalse(svc.HasUnreadOrUnclaimed, "已读无奖励 → false");

            long withReward = svc.Send(Draft(reward: RewardPool));
            svc.MarkRead(withReward);
            Assert.IsTrue(svc.HasUnreadOrUnclaimed, "已读但有奖未领 → true");
            svc.Claim(withReward, new MergeOrderState());
            Assert.IsFalse(svc.HasUnreadOrUnclaimed, "已读已领 → false");
        }

        [Test] // RDOT2：已读但奖励未领 HasRedDot==true；领后 false
        public void RDOT2_HasRedDot_ReadButUnclaimed_TrueThenFalse()
        {
            SeedRewards();
            MailConfigMgr.InitForTest(Array.Empty<MailDef>());
            var svc = NewService(new InMemoryMailPersistence(), T0);
            long id = svc.Send(Draft(reward: RewardPool));
            svc.MarkRead(id);
            Assert.IsTrue(svc.Find(id).HasRedDot, "读态不消红点，奖励态才消");
            svc.Claim(id, new MergeOrderState());
            Assert.IsFalse(svc.Find(id).HasRedDot);
        }

        // ════════════ 持久化 P ════════════

        [Test] // P1：跨实例往返（同 provider）→ List 含原邮件且读/领态保真
        public void P1_Persistence_CrossInstanceRoundtrip_StatePreserved()
        {
            SeedRewards();
            MailConfigMgr.InitForTest(Array.Empty<MailDef>());
            var persist = new InMemoryMailPersistence();
            var svc1 = NewService(persist, T0);
            long readId = svc1.Send(Draft(reward: 0));
            long claimedId = svc1.Send(Draft(reward: RewardPool));
            svc1.MarkRead(readId);
            svc1.Claim(claimedId, new MergeOrderState());

            // 新实例复用同 persist（模拟重启）
            var svc2 = NewService(persist, T0);
            Assert.AreEqual(2, svc2.Count);
            Assert.IsTrue(svc2.Find(readId).Read);
            Assert.IsTrue(svc2.Find(claimedId).Claimed);
            Assert.IsTrue(svc2.Find(claimedId).Read);
            Assert.AreEqual(T0.Ticks, svc2.Find(readId).SendTimeTicks, "时间保真");
        }

        [Test] // P2：反序列化保底——无键/空串/非法 JSON → Load 空列表不抛；空收件箱可正常 Send
        public void P2_MailPersistence_DefensiveDeserialize()
        {
            MailConfigMgr.InitForTest(Array.Empty<MailDef>());
            var provider = new InMemoryPersistenceProvider();
            Persistence.Provider = provider;
            var mp = new MailPersistence();

            // 无键 → 空列表
            Assert.DoesNotThrow(() => mp.Load());
            Assert.AreEqual(0, mp.Load().Count);
            // 空串 → 空列表
            provider.Set(MailPersistence.Key, "");
            Assert.AreEqual(0, mp.Load().Count);
            // 非法 JSON → 空列表不抛
            provider.Set(MailPersistence.Key, "{not valid json][");
            List<MailItem> loaded = null;
            Assert.DoesNotThrow(() => loaded = mp.Load());
            Assert.AreEqual(0, loaded.Count);

            // 空收件箱可正常 Send（用生产 MailPersistence 走 InMemory provider）
            var svc = NewService(mp, T0);
            Assert.DoesNotThrow(() => svc.Send(Draft(reward: 0)));
            Assert.AreEqual(1, svc.Count);
        }

        // ════════════ 接缝 SK ════════════

        [Test] // SK1：InertMailSource.Pull 返空且不抛、不连网
        public void SK1_InertMailSource_PullEmpty_NoThrow()
        {
            IMailSource source = new InertMailSource();
            IReadOnlyList<MailDraft> drafts = null;
            Assert.DoesNotThrow(() => drafts = source.Pull());
            Assert.IsNotNull(drafts);
            Assert.AreEqual(0, drafts.Count);
        }

        [Test] // SK2：IMailService.Send 真实可用（经接口引用调 Send 收件成功）
        public void SK2_IMailService_Send_RealUsableThroughInterface()
        {
            MailConfigMgr.InitForTest(Array.Empty<MailDef>());
            IMailService service = NewService(new InMemoryMailPersistence(), T0);
            long id = service.Send(Draft(reward: RewardPool));
            Assert.Greater(id, 0, "经对外接口收件成功，返有效 id");
        }

        // ════════════ 文案 textId（占位互不相同）════════════

        [Test] // 文案：每个结果码 TextIdFor 返非 0 且互不相同（同 19/20 占位约定）
        public void MailText_DistinctNonZeroTextIds()
        {
            var ids = new HashSet<int>();
            foreach (ClaimStatus s in Enum.GetValues(typeof(ClaimStatus)))
            {
                int id = MailText.TextIdFor(s);
                Assert.AreNotEqual(0, id, $"{s} textId 不应为 0");
                Assert.IsTrue(ids.Add(id), $"{s} textId 与其它重复");
            }
            Assert.AreEqual(5, ids.Count);
        }
    }
}
