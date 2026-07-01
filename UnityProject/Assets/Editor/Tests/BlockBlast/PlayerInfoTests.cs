using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GameLogic.Config;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Player;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 玩家信息系统数据逻辑层验收测试（设计 18 §六，30 条）。
    /// 配置表(C) = AssetDatabase 直读 avatar_tbavatar.bytes（仿 ItemSystemTests，绕 YooAsset）；
    /// 其余(N/R/P/L/U/D/S/B) = 纯逻辑 new / 静态调用 / InitForTest 注入（Z1 隔离即证未触 ConfigSystem）。
    /// </summary>
    [TestFixture]
    public class PlayerInfoTests
    {
        private const string AvatarBytes = "Assets/AssetRaw/Configs/bytes/avatar_tbavatar.bytes";

        // ───────────────────────── 直读 .bytes 辅助 ─────────────────────────

        private static GameConfig.avatar.TbAvatar LoadAvatarTable()
        {
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(AvatarBytes);
            Assert.IsNotNull(ta, $"找不到 {AvatarBytes}，请先运行 Luban 导表");
            return new GameConfig.avatar.TbAvatar(new Luban.ByteBuf(ta.bytes));
        }

        // 样例头像/框 POCO（与 avatar.xlsx 同值），供纯逻辑测试用。
        private static List<AvatarEntry> SampleAvatars() => new List<AvatarEntry>
        {
            new AvatarEntry { Id = 1,   Type = AvatarType.Avatar, Image = "avt_robot",   UnlockText = 300001, UnlockCond = UnlockCond.Level, UnlockParam = 1 },
            new AvatarEntry { Id = 2,   Type = AvatarType.Avatar, Image = "avt_cat",     UnlockText = 300002, UnlockCond = UnlockCond.Level, UnlockParam = 5 },
            new AvatarEntry { Id = 3,   Type = AvatarType.Avatar, Image = "avt_star",    UnlockText = 300003, UnlockCond = UnlockCond.Event, UnlockParam = 9001 },
            new AvatarEntry { Id = 101, Type = AvatarType.Frame,  Image = "frm_default", UnlockText = 300101, UnlockCond = UnlockCond.Level, UnlockParam = 1 },
            new AvatarEntry { Id = 102, Type = AvatarType.Frame,  Image = "frm_gold",    UnlockText = 300102, UnlockCond = UnlockCond.Level, UnlockParam = 10 },
            new AvatarEntry { Id = 103, Type = AvatarType.Frame,  Image = "frm_event",   UnlockText = 300103, UnlockCond = UnlockCond.Event, UnlockParam = 9002 },
        };

        [SetUp]
        public void Setup() => AvatarConfigMgr.ResetForTest();

        [TearDown]
        public void Cleanup() => AvatarConfigMgr.ResetForTest();

        // ───────────────────────── 名字生成（N1–N2） ─────────────────────────

        [Test]
        public void N1_NameStructure()
        {
            var name = PlayerNameGenerator.Generate(new System.Random(42));
            Assert.IsTrue(name.StartsWith("Player"), "以 Player 开头");
            Assert.AreEqual(PlayerNameGenerator.PREFIX.Length + PlayerNameGenerator.SUFFIX_LEN, name.Length,
                "总长 = 前缀6 + 后缀6 = 12");
            string suffix = name.Substring(PlayerNameGenerator.PREFIX.Length);
            Assert.AreEqual(6, suffix.Length);
            foreach (char c in suffix)
                Assert.IsTrue(PlayerNameGenerator.CHARSET.IndexOf(c) >= 0, $"后缀字符 '{c}' 应落在 62 字符集内");
        }

        [Test]
        public void N2_NameRandomness()
        {
            // 同种子两次一致（确定性）
            var a = PlayerNameGenerator.Generate(new System.Random(7));
            var b = PlayerNameGenerator.Generate(new System.Random(7));
            Assert.AreEqual(a, b, "同种子应确定复现");
            // 异种子后缀不全等（防写死）
            var c = PlayerNameGenerator.Generate(new System.Random(99999));
            Assert.AreNotEqual(a.Substring(6), c.Substring(6), "异种子后缀应不全等");
        }

        // 通用测试玩家构造(供改名 / 经验 / 解锁三态各组共用)。
        private static PlayerInfo NewPlayer(int renameCount = 0, string name = "PlayerInit01")
            => new PlayerInfo { Id = "id123", Name = name, RenameCount = renameCount, Exp = 0,
                CurrentAvatarId = 1, CurrentFrameId = 101,
                UnlockedAvatarIds = new[] { 1 }, UnlockedFrameIds = new[] { 101 } };

        // ───────────────────────── 改名本地校验 + 费用预告（R1–R5） ─────────────────────────
        //
        // 改名改走服务端权威 RPC(客户端段):费用 / 扣钻 / 写名 / 计数全由服务端一次原子做完。
        // 客户端 PlayerRenameService 只剩纯本地校验(减一次无谓往返)+ 费用预告(RenamePriceConfig 供 UI 预告)。
        // 本组只测「本地校验判定」与「费用预告数值」,不再测本地扣钻 / 本地写名(那已迁服务端)。

        [Test]
        public void R1_FirstRenameFree_PricePreviewZero()
        {
            // 首次改名(RenameCount=0)费用预告 = 0(免费)。
            Assert.AreEqual(0, RenamePriceConfig.PriceFor(0), "首次预告 Cost=0");
        }

        [Test]
        public void R2_NonFirstRename_PricePreviewFixed()
        {
            // 非首次改名费用预告 = 固定价(默认 100)。
            Assert.AreEqual(RenamePriceConfig.RENAME_PRICE, RenamePriceConfig.PriceFor(1), "二次预告读配置价（默认100）");
            Assert.AreEqual(RenamePriceConfig.RENAME_PRICE, RenamePriceConfig.PriceFor(5), "多次预告仍固定价");
        }

        [Test]
        public void R3_LegalName_ValidatePasses()
        {
            // 合法名(空词表)→ 本地校验通过(None)。
            Assert.AreEqual(RenameReject.None, PlayerRenameService.ValidateLocal("新名字", null),
                "合法名本地校验应通过");
        }

        [Test]
        public void R4_ProfanityRejected()
        {
            // 屏蔽字命中 → Profanity(空词表则不触发)。
            Assert.AreEqual(RenameReject.Profanity, PlayerRenameService.ValidateLocal("superADMIN", new[] { "admin" }),
                "含屏蔽词应拒");
            Assert.AreEqual(RenameReject.None, PlayerRenameService.ValidateLocal("superADMIN", System.Array.Empty<string>()),
                "空词表不触发 Profanity");
        }

        [Test]
        public void R5_EmptyAndTooLongReject()
        {
            Assert.AreEqual(RenameReject.Empty, PlayerRenameService.ValidateLocal("", null), "空串");
            Assert.AreEqual(RenameReject.Empty, PlayerRenameService.ValidateLocal("   ", null), "全空白当空");

            string tooLong = new string('a', PlayerRenameService.MaxLen + 1);
            Assert.AreEqual(RenameReject.TooLong, PlayerRenameService.ValidateLocal(tooLong, null), "超长");
        }

        // ───────────────────────── 屏蔽字（P1–P2） ─────────────────────────

        [Test]
        public void P1_ProfanityCaseInsensitive()
        {
            var list = new[] { "admin" };
            Assert.IsFalse(ProfanityFilter.IsClean("superADMIN", list), "子串含 admin（大小写不敏感）");
            Assert.IsTrue(ProfanityFilter.IsClean("Player2dfgKL", list), "不含屏蔽词");
        }

        [Test]
        public void P2_EmptyWordListNeverBlocks()
        {
            Assert.IsTrue(ProfanityFilter.IsClean("任意名", new string[0]), "空表不拦");
            Assert.IsTrue(ProfanityFilter.IsClean("任意名", null), "null 表不拦");
        }

        // ───────────────────────── 等级（L1–L4） ─────────────────────────

        [Test]
        public void L1_LevelForBuckets()
        {
            Assert.AreEqual(1, PlayerLevelConfig.LevelFor(0));
            Assert.AreEqual(1, PlayerLevelConfig.LevelFor(99));
            Assert.AreEqual(2, PlayerLevelConfig.LevelFor(100));
            Assert.AreEqual(3, PlayerLevelConfig.LevelFor(250));
        }

        [Test]
        public void L2_ExpSlotProgress()
        {
            Assert.AreEqual(50, PlayerLevelConfig.ExpIntoLevel(300), "3 级内积累 50（CumExp(3)=250）");
            Assert.AreEqual(150, PlayerLevelConfig.ExpToNext(300), "差 150 升 4 级（CumExp(4)=450）");
        }

        [Test]
        public void L3_LevelCap()
        {
            int huge = int.MaxValue;
            Assert.AreEqual(PlayerLevelConfig.MAX_LEVEL, PlayerLevelConfig.LevelFor(huge), "封顶 60");
            Assert.AreEqual(0, PlayerLevelConfig.ExpToNext(huge), "封顶 ExpToNext=0");
            Assert.DoesNotThrow(() => PlayerLevelConfig.ExpIntoLevel(huge), "不溢出/不抛");
        }

        [Test]
        public void L4_AddExpOnlyIncreases()
        {
            var p = NewPlayer();
            PlayerExpService.AddExp(p, 50);
            Assert.AreEqual(50, p.Exp);
            PlayerExpService.AddExp(p, -10);
            Assert.AreEqual(50, p.Exp, "负数不减");
            PlayerExpService.AddExp(p, 0);
            Assert.AreEqual(50, p.Exp, "0 不变");
        }

        // ───────────────────────── 解锁三态（U1–U6） ─────────────────────────

        private static AvatarEntry Entry(int id, int type, int cond, int param)
            => new AvatarEntry { Id = id, Type = type, UnlockCond = cond, UnlockParam = param };

        [Test]
        public void U1_LevelConditionRealtimeUnlock()
        {
            var p = NewPlayer();
            PlayerExpService.AddExp(p, PlayerLevelConfig.CumExp(5)); // Level=5
            Assert.AreEqual(5, p.Level);
            var e = Entry(2, AvatarType.Avatar, UnlockCond.Level, 5);
            Assert.IsTrue(AvatarUnlockService.IsUnlocked(p, e), "Level5≥5 实时解锁（集合未含）");

            var p4 = NewPlayer();
            PlayerExpService.AddExp(p4, PlayerLevelConfig.CumExp(4)); // Level=4
            Assert.IsFalse(AvatarUnlockService.IsUnlocked(p4, e), "Level4<5 未达标");
        }

        [Test]
        public void U2_StateEquipped()
        {
            var p = NewPlayer(); // CurrentAvatarId=1, 集合含 1
            var e = Entry(1, AvatarType.Avatar, UnlockCond.Level, 1);
            Assert.AreEqual(AvatarState.Equipped, AvatarUnlockService.StateOf(p, e));
        }

        [Test]
        public void U3_StateUnlockedAndLocked()
        {
            var p = NewPlayer();
            PlayerExpService.AddExp(p, PlayerLevelConfig.CumExp(5)); // Level=5
            var unlockedNotEquipped = Entry(2, AvatarType.Avatar, UnlockCond.Level, 5);
            Assert.AreEqual(AvatarState.Unlocked, AvatarUnlockService.StateOf(p, unlockedNotEquipped),
                "已解锁 + 非当前 → Unlocked");
            var locked = Entry(99, AvatarType.Avatar, UnlockCond.Level, 50);
            Assert.AreEqual(AvatarState.Locked, AvatarUnlockService.StateOf(p, locked),
                "未达条件且集合不含 → Locked");
        }

        [Test]
        public void U4_EventNotJudgedThisRound()
        {
            var p = NewPlayer();
            var ev = Entry(3, AvatarType.Avatar, UnlockCond.Event, 9001);
            Assert.AreEqual(AvatarState.Locked, AvatarUnlockService.StateOf(p, ev), "EVENT 集合不含 → Locked");
            // 钩子可达性：手动（将来活动）把 id 写进集合后 → Unlocked
            AvatarUnlockService.GrantUnlock(p, ev);
            Assert.AreEqual(AvatarState.Unlocked, AvatarUnlockService.StateOf(p, ev), "写集合后视为已解锁");
        }

        [Test]
        public void U5_TryEquipOnlyUnlocked()
        {
            var p = NewPlayer();
            PlayerExpService.AddExp(p, PlayerLevelConfig.CumExp(5)); // Level=5
            var ok = Entry(2, AvatarType.Avatar, UnlockCond.Level, 5);
            Assert.IsTrue(AvatarUnlockService.TryEquip(p, ok), "已解锁可佩戴");
            Assert.AreEqual(2, p.CurrentAvatarId, "CurrentAvatarId 改为目标");

            var locked = Entry(50, AvatarType.Avatar, UnlockCond.Level, 50);
            Assert.IsFalse(AvatarUnlockService.TryEquip(p, locked), "未解锁不可佩戴");
            Assert.AreEqual(2, p.CurrentAvatarId, "未解锁佩戴失败不改");
        }

        [Test]
        public void U6_SyncLevelUnlocksDedup()
        {
            var p = NewPlayer();
            PlayerExpService.AddExp(p, PlayerLevelConfig.CumExp(10)); // Level=10
            AvatarUnlockService.SyncLevelUnlocks(p, SampleAvatars());
            // Level10 达标的 LEVEL 头像：id 1(≥1)、id 2(≥5) 进头像集合；框 101(≥1)、102(≥10) 进框集合
            Assert.Contains(2, p.UnlockedAvatarIds, "id2 达标进集合");
            Assert.Contains(102, p.UnlockedFrameIds, "框102 达标进集合");
            // EVENT 项不进
            CollectionAssert.DoesNotContain(p.UnlockedAvatarIds, 3, "EVENT 项不进");
            // 去重：再调一次不重复加
            int avatarCountBefore = p.UnlockedAvatarIds.Length;
            AvatarUnlockService.SyncLevelUnlocks(p, SampleAvatars());
            Assert.AreEqual(avatarCountBefore, p.UnlockedAvatarIds.Length, "重复调用不重复加");
        }

        // ───────────────────────── 头像表（C1–C2，直读 .bytes） ─────────────────────────

        [Test]
        public void C1_AvatarTable_DirectRead()
        {
            var table = LoadAvatarTable();
            Assert.AreEqual(6, table.DataList.Count, "样例 6 行");
            var row1 = table.GetOrDefault(1);
            Assert.IsNotNull(row1);
            Assert.AreEqual(AvatarType.Avatar, (int)row1.Type);
            Assert.AreEqual("avt_robot", row1.Image);
            Assert.AreEqual(300001, row1.UnlockText);
            Assert.AreEqual(UnlockCond.Level, (int)row1.UnlockCond);
            Assert.AreEqual(1, row1.UnlockParam);

            var row102 = table.GetOrDefault(102);
            Assert.AreEqual(AvatarType.Frame, (int)row102.Type);
            Assert.AreEqual(10, row102.UnlockParam, "框102 解锁等级 10");

            var row3 = table.GetOrDefault(3);
            Assert.AreEqual(UnlockCond.Event, (int)row3.UnlockCond);
            Assert.AreEqual(9001, row3.UnlockParam, "EVENT 项留活动 id");
        }

        [Test]
        public void C2_ConfigMgrBridge()
        {
            var table = LoadAvatarTable();
            var entries = new List<AvatarEntry>();
            foreach (var row in table.DataList) entries.Add(AvatarConfigMgr.ToEntry(row));
            AvatarConfigMgr.InitForTest(entries);

            var a1 = AvatarConfigMgr.GetAvatar(1);
            Assert.IsNotNull(a1);
            Assert.AreEqual("avt_robot", a1.Image);
            Assert.AreEqual(AvatarType.Avatar, a1.Type);

            var frames = AvatarConfigMgr.GetByType(AvatarType.Frame);
            Assert.AreEqual(3, frames.Count, "框类型 3 行（101/102/103）");
            foreach (var f in frames) Assert.AreEqual(AvatarType.Frame, f.Type);

            Assert.IsNull(AvatarConfigMgr.GetAvatar(999999), "未命中返 null 不抛");
        }

        // ───────────────────────── 默认 / id（D1–D2） ─────────────────────────

        [Test]
        public void D1_CreateDefault()
        {
            var p = PlayerInfo.CreateDefault(new System.Random(1));
            Assert.IsFalse(string.IsNullOrEmpty(p.Id), "Id 非空");
            Assert.IsTrue(p.Name.StartsWith("Player"));
            Assert.AreEqual(0, p.RenameCount);
            Assert.AreEqual(0, p.Exp);
            Assert.AreEqual(PlayerInfo.DefaultAvatarId, p.CurrentAvatarId);
            Assert.AreEqual(PlayerInfo.DefaultFrameId, p.CurrentFrameId);
            Assert.Contains(PlayerInfo.DefaultAvatarId, p.UnlockedAvatarIds, "解锁集合含默认头像");
            Assert.Contains(PlayerInfo.DefaultFrameId, p.UnlockedFrameIds, "解锁集合含默认框");
        }

        // ───────────────────────── 持久化（S1–S3） ─────────────────────────

        [Test]
        public void S1_PersistRoundTrip()
        {
            var p = new PlayerInfo
            {
                Id = "abc123", Name = "PlayerRoundTrip", RenameCount = 2, Exp = 555,
                CurrentAvatarId = 2, CurrentFrameId = 102,
                UnlockedAvatarIds = new[] { 1, 2 }, UnlockedFrameIds = new[] { 101, 102 },
            };
            var dto = new MergeMetaSave();
            p.ExportToMeta(dto);
            // 经磁盘序列化往返（与既有 MergeMetaPersistence 同口径）。
            string json = MergeMetaPersistence.Serialize(dto);
            var back = MergeMetaPersistence.Deserialize(json);
            var p2 = PlayerInfo.ImportFromMeta(back, new System.Random(1));

            Assert.AreEqual(p.Id, p2.Id);
            Assert.AreEqual(p.Name, p2.Name);
            Assert.AreEqual(p.RenameCount, p2.RenameCount);
            Assert.AreEqual(p.Exp, p2.Exp);
            Assert.AreEqual(p.CurrentAvatarId, p2.CurrentAvatarId);
            Assert.AreEqual(p.CurrentFrameId, p2.CurrentFrameId);
            CollectionAssert.AreEquivalent(p.UnlockedAvatarIds, p2.UnlockedAvatarIds);
            CollectionAssert.AreEquivalent(p.UnlockedFrameIds, p2.UnlockedFrameIds);
        }

        [Test]
        public void S2_OldSaveMissingPlayerFieldsFallback()
        {
            // 旧档：只含设计14 字段，无玩家字段。
            var oldDto = new MergeMetaSave
            {
                version = MergeMetaPersistence.CurrentVersion,
                soul = 10, piety = 20, exp = 30, unlockedChapter = 4,
            };
            string json = MergeMetaPersistence.Serialize(oldDto);
            var back = MergeMetaPersistence.Deserialize(json);

            // 既有字段不丢
            Assert.AreEqual(10, back.soul);
            Assert.AreEqual(20, back.piety);
            Assert.AreEqual(30, back.exp);
            Assert.AreEqual(4, back.unlockedChapter);

            // 玩家字段走缺省，不抛
            PlayerInfo p = null;
            Assert.DoesNotThrow(() => p = PlayerInfo.ImportFromMeta(back, new System.Random(1)));
            Assert.IsFalse(string.IsNullOrEmpty(p.Id), "id 现场生成");
            Assert.IsTrue(p.Name.StartsWith("Player"), "name 现场生成");
            Assert.AreEqual(PlayerInfo.DefaultAvatarId, p.CurrentAvatarId, "头像退默认");
            Assert.AreEqual(PlayerInfo.DefaultFrameId, p.CurrentFrameId, "框退默认");
            Assert.Contains(PlayerInfo.DefaultAvatarId, p.UnlockedAvatarIds, "集合重建含默认");
            Assert.Contains(PlayerInfo.DefaultFrameId, p.UnlockedFrameIds);
        }

        [Test]
        public void S3_TamperedValuesClamped()
        {
            var dto = new MergeMetaSave
            {
                playerId = "stillvalid", playerName = "PlayerX",
                playerRenameCount = -5, playerExp = -100,
                curAvatarId = 0, curFrameId = -3,
                unlockedAvatarIds = null, unlockedFrameIds = null,
            };
            var p = PlayerInfo.ImportFromMeta(dto, new System.Random(1));
            Assert.AreEqual(0, p.RenameCount, "renameCount -5 → 0");
            Assert.AreEqual(0, p.Exp, "exp -100 → 0");
            Assert.AreEqual(PlayerInfo.DefaultAvatarId, p.CurrentAvatarId, "curAvatarId 0 → 默认1");
            Assert.AreEqual(PlayerInfo.DefaultFrameId, p.CurrentFrameId, "curFrameId -3 → 默认101");
            Assert.Contains(PlayerInfo.DefaultAvatarId, p.UnlockedAvatarIds, "null → 含默认数组");
            Assert.Contains(PlayerInfo.DefaultFrameId, p.UnlockedFrameIds);
        }

        // ───────────────────────── 剪贴板（B1） ─────────────────────────

        [Test]
        public void B1_ClipboardInjectableSink()
        {
            var original = ClipboardUtil.Sink;
            try
            {
                string captured = null;
                ClipboardUtil.Sink = text => captured = text;
                ClipboardUtil.Copy("id-xyz");
                Assert.AreEqual("id-xyz", captured, "Copy 写入捕获 sink");

                captured = null;
                ClipboardUtil.Copy("");
                Assert.IsNull(captured, "空串不写");
                ClipboardUtil.Copy(null);
                Assert.IsNull(captured, "null 不写");
            }
            finally
            {
                ClipboardUtil.Sink = original; // 测后还原，不污染其他测试 / 真实剪贴板
            }
        }

        // ───────────────────────── 隔离（Z1，纯逻辑无 ConfigSystem） ─────────────────────────

        [Test]
        public void Z1_PureLogic_NoConfigSystem()
        {
            // 全链 N/R/P/L/U/D/S/B 不触发 ConfigSystem / YooAsset。本测试在 EditMode 跑通即证未访问 Tables。
            var p = PlayerInfo.CreateDefault(new System.Random(1));
            PlayerExpService.AddExp(p, PlayerLevelConfig.CumExp(5));
            var e = Entry(2, AvatarType.Avatar, UnlockCond.Level, 5);
            Assert.IsTrue(AvatarUnlockService.IsUnlocked(p, e));
            Assert.IsTrue(ProfanityFilter.IsClean(p.Name, new[] { "zzz" }));
            var dto = new MergeMetaSave();
            p.ExportToMeta(dto);
            var p2 = PlayerInfo.ImportFromMeta(dto, new System.Random(2));
            Assert.AreEqual(p.Id, p2.Id);
        }
    }
}
