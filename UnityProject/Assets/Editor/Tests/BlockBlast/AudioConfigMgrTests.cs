using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GameConfig.audio;
using GameLogic.Config;
using TEngine;
using AudioType = TEngine.AudioType; // 消歧：与 UnityEngine.AudioType 同名

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// AudioConfigMgr 验收测试。
    /// 纯决策逻辑（MapType / IsGroupValid / ResolveCooldown / IsOnCooldown）= 无 Unity 运行时依赖；
    /// 配置表 = AssetDatabase 直读 audio_tbaudio.bytes（仿 NumericSystemTests，绕 YooAsset），验样板行加载 + Group 全合法。
    /// Play/Stop/FadeIn 依赖运行时 AudioModule，不在 EditMode 覆盖。
    /// </summary>
    [TestFixture]
    public class AudioConfigMgrTests
    {
        private const string BytesPath = "Assets/AssetRaw/Configs/bytes/audio_tbaudio.bytes";

        // ───────────────────────── 纯决策逻辑 ─────────────────────────

        [Test] public void MapType_BGM_ToMusic() => Assert.AreEqual(AudioType.Music, AudioConfigMgr.MapType(EAudioType.BGM));
        [Test] public void MapType_GlobalUI_ToUISound() => Assert.AreEqual(AudioType.UISound, AudioConfigMgr.MapType(EAudioType.GlobalUI));
        [Test] public void MapType_InGame_ToSound() => Assert.AreEqual(AudioType.Sound, AudioConfigMgr.MapType(EAudioType.InGame));

        [Test] public void Group_BGM_UnderBGM_Valid() => Assert.IsTrue(AudioConfigMgr.IsGroupValid(EAudioType.BGM, EAudioGroup.BGM));
        [Test] public void Group_UI_UnderBGM_Invalid() => Assert.IsFalse(AudioConfigMgr.IsGroupValid(EAudioType.BGM, EAudioGroup.UI));
        [Test] public void Group_Battle_UnderInGame_Valid() => Assert.IsTrue(AudioConfigMgr.IsGroupValid(EAudioType.InGame, EAudioGroup.Battle));
        [Test] public void Group_Battle_UnderGlobalUI_Invalid() => Assert.IsFalse(AudioConfigMgr.IsGroupValid(EAudioType.GlobalUI, EAudioGroup.Battle));
        [Test] public void Group_Global_UnderAny_Valid() => Assert.IsTrue(AudioConfigMgr.IsGroupValid(EAudioType.GlobalUI, EAudioGroup.Global));

        [Test] public void Cooldown_ClipPositive_TakesClip() => Assert.AreEqual(0.2f, AudioConfigMgr.ResolveCooldown(0.2f, 0.05f), 1e-6f);
        [Test] public void Cooldown_ClipZero_TakesGlobal() => Assert.AreEqual(0.05f, AudioConfigMgr.ResolveCooldown(0f, 0.05f), 1e-6f);

        [Test] public void OnCooldown_WithinWindow_True() => Assert.IsTrue(AudioConfigMgr.IsOnCooldown(1.00f, 0.99f, 0.05f));
        [Test] public void OnCooldown_AfterWindow_False() => Assert.IsFalse(AudioConfigMgr.IsOnCooldown(1.10f, 0.99f, 0.05f));
        [Test] public void OnCooldown_ZeroCd_NeverBlocks() => Assert.IsFalse(AudioConfigMgr.IsOnCooldown(1.00f, 1.00f, 0f));

        [Test] public void Fade_In_And_Both_HaveFadeIn()
        {
            Assert.IsTrue(AudioConfigMgr.HasFadeIn(EAudioFade.In));
            Assert.IsTrue(AudioConfigMgr.HasFadeIn(EAudioFade.Both));
            Assert.IsFalse(AudioConfigMgr.HasFadeIn(EAudioFade.Out));
            Assert.IsFalse(AudioConfigMgr.HasFadeIn(EAudioFade.None));
        }

        [Test] public void Fade_Out_And_Both_HaveFadeOut()
        {
            Assert.IsTrue(AudioConfigMgr.HasFadeOut(EAudioFade.Out));
            Assert.IsTrue(AudioConfigMgr.HasFadeOut(EAudioFade.Both));
            Assert.IsFalse(AudioConfigMgr.HasFadeOut(EAudioFade.In));
            Assert.IsFalse(AudioConfigMgr.HasFadeOut(EAudioFade.None));
        }

        // ───────────────────────── 配置表（AssetDatabase 直读 .bytes） ─────────────────────────

        private static GameConfig.audio.TbAudio LoadTableFromBytes()
        {
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(BytesPath);
            Assert.IsNotNull(ta, $"未找到 {BytesPath}，需先导表（gen_code_bin_to_project_lazyload）");
            return new GameConfig.audio.TbAudio(new Luban.ByteBuf(ta.bytes));
        }

        [Test]
        public void Bytes_SampleRow1001_FieldsMatch()
        {
            var tb = LoadTableFromBytes();
            var bgm = tb.GetOrDefault(1001);
            Assert.IsNotNull(bgm, "样板行 id=1001 缺失");
            Assert.AreEqual(EAudioType.BGM, bgm.Type);
            Assert.AreEqual(EAudioGroup.BGM, bgm.AudioGroup);
            Assert.AreEqual(EAudioFade.Both, bgm.FadeInOut);
            Assert.IsTrue(bgm.Loop);
            Assert.AreEqual("Audio/BGM/main_theme", bgm.ResourcePath);
        }

        [Test]
        public void Bytes_AllSampleGroupsValid()
        {
            var tb = LoadTableFromBytes();
            Assert.AreEqual(4, tb.DataList.Count, "样板数据应为 4 行");
            foreach (var row in tb.DataList)
            {
                Assert.IsTrue(AudioConfigMgr.IsGroupValid(row.Type, row.AudioGroup),
                    $"id={row.Id} Type={row.Type} AudioGroup={row.AudioGroup} 越界");
            }
        }
    }
}
