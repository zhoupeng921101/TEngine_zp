using NUnit.Framework;
using UnityEngine;
using GameLogic.Settings;
using GameLogic.BlockBlast.Player;
// UnityEngine.AudioSettings 与 GameLogic.Settings.AudioSettings 同名（CS0104），别名消歧到本系统模型。
using AudioSettings = GameLogic.Settings.AudioSettings;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 通用设置系统数据逻辑层验收测试（设计 19 §六，17 条）。
    /// 全 EditMode 可测：模型 / 服务纯内存 new；存储往返注入 <see cref="InMemorySettingsStore"/>（不碰真实 PlayerPrefs）；
    /// 音频应用经可注入 <see cref="SettingsService.AudioSink"/> 记录（不触真实音频模块）；
    /// 版本号经可注入 <see cref="SettingsInfo.VersionProvider"/>（每例用后还原，避免静态字段跨例污染）。
    /// </summary>
    [TestFixture]
    public class SettingsSystemTests
    {
        // ───────────────────────── 模型 M（默认全开 / 字段初值 / 无 Unity 依赖）─────────────────────────

        [Test] // M1
        public void M1_CreateDefault_BothOn()
        {
            var a = AudioSettings.CreateDefault();
            Assert.IsTrue(a.MusicOn);
            Assert.IsTrue(a.SoundOn);
        }

        [Test] // M2
        public void M2_NewInstance_FieldsTrue()
        {
            var a = new AudioSettings();
            Assert.IsTrue(a.MusicOn, "字段初值应为 true，不依赖工厂");
            Assert.IsTrue(a.SoundOn, "字段初值应为 true，不依赖工厂");
        }

        [Test] // M3
        public void M3_AudioSettings_IsSerializable_NoUnityDependency()
        {
            // [Serializable] 标注存在；可纯 C# new（本测试方法已 new 多次即证不触 YooAsset / 音频模块）。
            Assert.IsTrue(typeof(AudioSettings).IsDefined(typeof(System.SerializableAttribute), false),
                "AudioSettings 应标 [Serializable]");
            var a = new AudioSettings();
            Assert.IsNotNull(a);
        }

        // ───────────────────────── 存储往返 P（注入 InMemorySettingsStore）─────────────────────────

        [Test] // P1
        public void P1_EmptyStore_Load_DefaultBothOn()
        {
            var svc = new SettingsService(new InMemorySettingsStore());
            svc.Load();
            Assert.IsTrue(svc.Audio.MusicOn, "无键应走默认全开");
            Assert.IsTrue(svc.Audio.SoundOn, "无键应走默认全开");
        }

        [Test] // P2
        public void P2_SetMusicFalse_StoreMutedTrue()
        {
            var store = new InMemorySettingsStore();
            var svc = new SettingsService(store);
            svc.SetMusic(false);
            // on→!muted：关音乐则 muted==true（取反正确）
            Assert.IsTrue(store.GetBool(TEngine.Constant.Setting.MusicMuted, false),
                "SetMusic(false) 应使 MusicMuted==true");
        }

        [Test] // P3
        public void P3_CrossInstance_LoadRoundTrip()
        {
            var store = new InMemorySettingsStore();
            new SettingsService(store).SetMusic(false);

            // 模拟「下次登录」：新建 service 复用同 store 调 Load
            var next = new SettingsService(store);
            next.Load();
            Assert.IsFalse(next.Audio.MusicOn, "跨实例往返应保真（关音乐沿用）");
        }

        [Test] // P4
        public void P4_SetSound_DoesNotAffectMusicKey_AndSoundMutedTrue()
        {
            var store = new InMemorySettingsStore();
            var svc = new SettingsService(store);
            svc.SetSound(false);
            Assert.IsTrue(store.GetBool(TEngine.Constant.Setting.SoundMuted, false),
                "SetSound(false) 应使 SoundMuted==true");
            // 两键独立：未碰 MusicMuted，仍取默认 false
            Assert.IsFalse(store.GetBool(TEngine.Constant.Setting.MusicMuted, false),
                "SetSound 不应影响 MusicMuted");
        }

        [Test] // P5
        public void P5_OffThenOn_BackToOn()
        {
            var store = new InMemorySettingsStore();
            var svc = new SettingsService(store);
            svc.SetMusic(false);
            svc.SetMusic(true);
            Assert.IsFalse(store.GetBool(TEngine.Constant.Setting.MusicMuted, true),
                "关后再开应使 MusicMuted==false");

            var next = new SettingsService(store);
            next.Load();
            Assert.IsTrue(next.Audio.MusicOn, "Load 应回 MusicOn==true");
        }

        // ───────────────────────── 服务 S（切换 / sink / 文案 / 键常量）─────────────────────────

        [Test] // S1
        public void S1_ToggleMusic_FlipsAndReturnsNewValue()
        {
            var svc = new SettingsService(new InMemorySettingsStore());
            bool start = svc.Audio.MusicOn;            // 默认 true
            bool after1 = svc.ToggleMusic();
            Assert.AreEqual(!start, after1, "Toggle 应翻转并返回新值");
            bool after2 = svc.ToggleMusic();
            Assert.AreEqual(start, after2, "连续两次回到原值");
        }

        [Test] // S2
        public void S2_SetMusic_InvokesSinkWithBothBools_NullSinkNoThrow()
        {
            var svc = new SettingsService(new InMemorySettingsStore());
            bool? lastMusic = null, lastSound = null;
            svc.AudioSink = (m, s) => { lastMusic = m; lastSound = s; };

            svc.SetMusic(false);
            Assert.AreEqual(false, lastMusic, "sink 应以当前 MusicOn=false 调用");
            Assert.AreEqual(svc.Audio.SoundOn, lastSound, "sink 应同时推当前 SoundOn");

            // null sink 不抛
            var svc2 = new SettingsService(new InMemorySettingsStore()) { AudioSink = null };
            Assert.DoesNotThrow(() => svc2.SetMusic(false));
        }

        [Test] // S3
        public void S3_ToggleTipTextId_FourDistinctNonZero()
        {
            int mOn = SettingsService.ToggleTipTextId(SettingKind.Music, true);
            int mOff = SettingsService.ToggleTipTextId(SettingKind.Music, false);
            int sOn = SettingsService.ToggleTipTextId(SettingKind.Sound, true);
            int sOff = SettingsService.ToggleTipTextId(SettingKind.Sound, false);

            int[] ids = { mOn, mOff, sOn, sOff };
            foreach (var id in ids)
                Assert.AreNotEqual(0, id, "四组提示 textId 均应非 0");
            CollectionAssert.AllItemsAreUnique(ids, "四组提示 textId 应互不相同");
        }

        [Test] // S4
        public void S4_PersistKey_EqualsFrameworkConstant_NotHardcoded()
        {
            // 服务落盘写入的 key 即框架常量本身（断言常量值，非硬编码字面量）
            Assert.AreEqual("Setting.MusicMuted", TEngine.Constant.Setting.MusicMuted);
            Assert.AreEqual("Setting.SoundMuted", TEngine.Constant.Setting.SoundMuted);

            // 落盘后用框架常量取键能取到值（证服务写的就是这两个键）
            var store = new InMemorySettingsStore();
            var svc = new SettingsService(store);
            svc.SetMusic(false);
            svc.SetSound(false);
            Assert.IsTrue(store.GetBool(TEngine.Constant.Setting.MusicMuted, false));
            Assert.IsTrue(store.GetBool(TEngine.Constant.Setting.SoundMuted, false));
        }

        // ───────────────────────── 信息 I（版本号 provider / 用户 ID）─────────────────────────

        [Test] // I1
        public void I1_VersionProvider_Injectable()
        {
            var saved = SettingsInfo.VersionProvider;
            try
            {
                SettingsInfo.VersionProvider = () => "9.9.9";
                Assert.AreEqual("9.9.9", SettingsInfo.Version());
            }
            finally
            {
                SettingsInfo.VersionProvider = saved; // 还原，避免跨例污染静态字段
            }
        }

        [Test] // I2
        public void I2_DefaultVersion_ReturnsApplicationVersion()
        {
            // 不注入时返 Application.version（EditMode 可读，非空）
            Assert.AreEqual(Application.version, SettingsInfo.Version());
            Assert.IsFalse(string.IsNullOrEmpty(SettingsInfo.Version()), "版本号不应为空");
        }

        [Test] // I3
        public void I3_UserId_ReturnsPlayerInfoId_NullSafe()
        {
            var p = new PlayerInfo { Id = "abc123" };
            Assert.AreEqual("abc123", SettingsInfo.UserId(p));
            Assert.AreEqual(string.Empty, SettingsInfo.UserId(null), "入参 null 应返空串、不抛");
        }
    }
}
