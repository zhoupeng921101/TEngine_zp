using NUnit.Framework;
using GameLogic;
using GameLogic.Settings;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 运行期上下文 GameContext 验收测试（设计 23 §9.1 H/W 组）。
    /// 全 EditMode 可测：GameContext 是 SimpleSingleton（不依赖 IUpdateDriver），
    /// 经 <see cref="GameContext.InitSettingsWithStore"/> 注入 <see cref="InMemorySettingsStore"/> 断言往返，
    /// 不碰真实 PlayerPrefs / 音频模块。每例 Teardown Release，避免单例跨例污染。
    /// </summary>
    [TestFixture]
    public class GameContextTests
    {
        [TearDown]
        public void TearDown()
        {
            if (GameContext.IsValid) GameContext.Instance.Release();
        }

        // ── H1：单例持有，多次 Instance 返回同一 SettingsService 实例 ──
        [Test]
        public void H1_Instance_SameSettingsService_AcrossAccess()
        {
            var ctx1 = GameContext.Instance;
            var ctx2 = GameContext.Instance;
            Assert.AreSame(ctx1, ctx2, "GameContext.Instance 应返回同一上下文单例");
            Assert.IsNotNull(ctx1.Settings, "Settings 应在 OnInit 时建好，非空");
            Assert.AreSame(ctx1.Settings, ctx2.Settings, "多次访问应是同一 SettingsService 实例（持有，不每次新建）");
        }

        // ── H2：首次 Instance 触发 OnInit + Load；无键时默认全开 ──
        [Test]
        public void H2_FirstInstance_LoadsDefaults_BothOn()
        {
            // 注入空 InMemory store 模拟「无键」（OnInit 默认走 PlayerPrefs，此处隔离到内存）。
            GameContext.Instance.InitSettingsWithStore(new InMemorySettingsStore());
            Assert.IsTrue(GameContext.Instance.Settings.Audio.MusicOn, "无键应默认音乐开");
            Assert.IsTrue(GameContext.Instance.Settings.Audio.SoundOn, "无键应默认音效开");
        }

        // ── H3a：同实例态保真（SetSound(false) 后再读为 false）──
        [Test]
        public void H3a_SetSoundFalse_SameInstance_StaysFalse()
        {
            GameContext.Instance.InitSettingsWithStore(new InMemorySettingsStore());
            GameContext.Instance.Settings.SetSound(false);
            Assert.IsFalse(GameContext.Instance.Settings.Audio.SoundOn, "同实例态应保真");
        }

        // ── H3b：持久化往返——Release 后重取 Instance，从同一 store 读回上次值 ──
        [Test]
        public void H3b_PersistRoundTrip_AcrossReleaseReacquire()
        {
            var store = new InMemorySettingsStore();   // 模拟持久存储（跨实例存活）
            GameContext.Instance.InitSettingsWithStore(store);
            GameContext.Instance.Settings.SetMusic(false);   // 落盘到 store
            GameContext.Instance.Release();                  // 丢弃当前实例

            // 重取 Instance（OnInit 会用 PlayerPrefs）后改注入同一 store 复 Load，验证读回上次值。
            GameContext.Instance.InitSettingsWithStore(store);
            Assert.IsFalse(GameContext.Instance.Settings.Audio.MusicOn,
                "Release 后从同一存储 Load 应读回上次落盘的 false（持久化往返）");
        }

        // ── W2：窗口开关切换贯通数据层落盘 ──
        // 窗口 OnMusicToggled/OnSoundToggled 直接调 GameContext.Instance.Settings.SetMusic/SetSound，
        // 此处断言该数据层路径：切换后内存态 + 存储键（muted 取反）双双更新。
        [Test]
        public void W2_ToggleSound_ViaDataLayer_PersistsMuted()
        {
            var store = new InMemorySettingsStore();
            GameContext.Instance.InitSettingsWithStore(store);

            // 等价于窗口 OnSoundToggled(false)
            GameContext.Instance.Settings.SetSound(false);

            Assert.IsFalse(GameContext.Instance.Settings.Audio.SoundOn, "内存态 SoundOn 应为 false");
            Assert.IsTrue(store.GetBool(TEngine.Constant.Setting.SoundMuted, false),
                "存储中 SoundMuted 应为 true（on=false → muted=true 取反落盘）");
        }

        [Test]
        public void W2_ToggleMusic_ViaDataLayer_PersistsMuted()
        {
            var store = new InMemorySettingsStore();
            GameContext.Instance.InitSettingsWithStore(store);

            // 等价于窗口 OnMusicToggled(false)
            GameContext.Instance.Settings.SetMusic(false);

            Assert.IsFalse(GameContext.Instance.Settings.Audio.MusicOn, "内存态 MusicOn 应为 false");
            Assert.IsTrue(store.GetBool(TEngine.Constant.Setting.MusicMuted, false),
                "存储中 MusicMuted 应为 true（on=false → muted=true 取反落盘）");
        }

        // ── AudioSink：窗口切换贯通到应用接缝（验证 sink 被调，值正确）──
        [Test]
        public void AudioSink_InvokedOnToggle_WithCurrentState()
        {
            GameContext.Instance.InitSettingsWithStore(new InMemorySettingsStore());
            bool? lastMusic = null, lastSound = null;
            GameContext.Instance.Settings.AudioSink = (m, s) => { lastMusic = m; lastSound = s; };

            GameContext.Instance.Settings.SetSound(false);
            Assert.AreEqual(true, lastMusic, "sink 收到当前音乐态");
            Assert.AreEqual(false, lastSound, "sink 收到切换后音效态");
        }
    }
}
