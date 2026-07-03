using System.Collections;
using System.Collections.Generic;
using GameConfig;
using GameConfig.audio;
using TEngine;
using UnityEngine;
using AudioType = TEngine.AudioType; // 消歧：与 UnityEngine.AudioType 同名

namespace GameLogic.Config
{
    /// <summary>
    /// 音频数据驱动配置管理器。
    /// 桥接 Luban 生成的 <c>GameConfig.audio.TbAudio</c>（id → <see cref="Audio"/> 行），按 id 播放，
    /// 骑在 TEngine <c>IAudioModule</c>（<c>GameModule.Audio</c>）之上：Type 决定 AudioType/通道池，
    /// Volume/Loop 透传，Cooldown 防连点门（专属 Cooldown&gt;0 优先，否则取全局 DefaultCooldown），
    /// FadeInOut 复用框架能力（淡出走 <c>AudioAgent.Stop(fadeout)</c>，淡入 best-effort 音量斜坡），
    /// Interruptible 复用框架池满抢占（无独立逐条优先级）。
    /// AudioGroup 是 Type 的子类型，加载期校验其落在 Type 允许子集内（越界记 Error 不阻断）。仿 <see cref="GlobalConfigMgr"/>。
    /// </summary>
    /// <remarks>
    /// 懒加载缓存；配置不可用（EditMode 无 ResourceModule / 资源未就绪）时灌空缓存而非抛，播放回退为无操作。
    /// 纯决策逻辑（<see cref="MapType"/> / <see cref="IsGroupValid"/> / <see cref="ResolveCooldown"/> / <see cref="IsOnCooldown"/>）
    /// 抽为 internal，不碰 Unity 运行时，供 EditMode 单测直接验证。
    /// </remarks>
    public static class AudioConfigMgr
    {
        /// <summary>淡入淡出默认时长（秒）。Audio 表仅记模式不记时长，统一取此默认。</summary>
        public const float DefaultFadeSeconds = 1.0f;

        private static Dictionary<int, Audio> _cache;                                          // id → 行（懒加载）
        private static readonly Dictionary<int, float> _lastPlayTime = new Dictionary<int, float>(); // id → 上次实际播放的 realtime（防连点）

        /// <summary>
        /// 运行期建缓存：经 <c>ConfigSystem</c>（YooAsset，需 Unity 运行时）。首次访问时灌入并做 Group 校验。
        /// ConfigSystem 不可用时灌空缓存而非抛：<see cref="Play"/> 缺行即回退（Warning + 返回 null）。
        /// </summary>
        public static void EnsureLoaded()
        {
            if (_cache != null) return;
            try
            {
                var table = ConfigSystem.Instance.Tables.TbAudio;
                _cache = new Dictionary<int, Audio>(table.DataList.Count);
                foreach (var row in table.DataList)
                {
                    _cache[row.Id] = row;
                }
                ValidateGroups(_cache.Values);
            }
            catch (System.Exception e)
            {
                Log.Warning($"[AudioConfigMgr] audio 配置表加载失败，播放回退为无操作。{e}");
                _cache = new Dictionary<int, Audio>();
            }
        }

        /// <summary>
        /// 按 id 播放。防连点门拦截过频触发（返回 null）；缺 id / 表未载记 Warning 返 null。
        /// FadeInOut 含淡入时先置 0 音量再 best-effort 斜坡到目标。
        /// </summary>
        /// <returns>底层 <c>AudioAgent</c>（被冷却拦截 / 缺配置 / 音频禁用时为 null）。</returns>
        public static AudioAgent Play(int audioId)
        {
            EnsureLoaded();
            if (!_cache.TryGetValue(audioId, out var row))
            {
                Log.Warning($"[AudioConfigMgr] 未找到音频配置 id={audioId}");
                return null;
            }

            float cd = ResolveCooldown(row.Cooldown, GlobalConfigMgr.AudioDefaultCooldownValue);
            float now = Time.realtimeSinceStartup;
            if (_lastPlayTime.TryGetValue(audioId, out var last) && IsOnCooldown(now, last, cd))
            {
                return null; // 冷却中，忽略本次触发（不刷新计时，保持原窗口）
            }
            _lastPlayTime[audioId] = now;

            AudioAgent agent = GameModule.Audio.Play(MapType(row.Type), row.ResourcePath, row.Loop, row.Volume, bAsync: true);

            if (agent != null && HasFadeIn(row.FadeInOut))
            {
                FadeIn(agent, row.Volume, DefaultFadeSeconds);
            }
            return agent;
        }

        /// <summary>
        /// 停止指定 agent。按该行 FadeInOut 是否含淡出决定是否渐消（复用框架 <c>AudioAgent.Stop(fadeout)</c>）。
        /// </summary>
        public static void Stop(AudioAgent agent, int audioId)
        {
            if (agent == null) return;
            EnsureLoaded();
            bool fadeout = _cache.TryGetValue(audioId, out var row) && HasFadeOut(row.FadeInOut);
            agent.Stop(fadeout);
        }

        // ───────────────────────── 纯决策逻辑（internal，EditMode 单测直验，无 Unity 运行时依赖） ─────────────────────────

        /// <summary>粗分类 → 框架 AudioType（决定通道池 / 混音总线）。BGM→Music，GlobalUI→UISound，InGame→Sound。</summary>
        internal static AudioType MapType(EAudioType type)
        {
            switch (type)
            {
                case EAudioType.BGM: return AudioType.Music;
                case EAudioType.GlobalUI: return AudioType.UISound;
                case EAudioType.InGame: return AudioType.Sound;
                default: return AudioType.Sound;
            }
        }

        // AudioGroup ⊆ Type 允许子集：Group 是 Type 的子类型，越界即配置错误。
        private static readonly Dictionary<EAudioType, EAudioGroup[]> AllowedGroups = new Dictionary<EAudioType, EAudioGroup[]>
        {
            { EAudioType.BGM, new[] { EAudioGroup.Global, EAudioGroup.BGM } },
            { EAudioType.GlobalUI, new[] { EAudioGroup.Global, EAudioGroup.UI } },
            { EAudioType.InGame, new[] { EAudioGroup.Global, EAudioGroup.Battle, EAudioGroup.Voice } },
        };

        /// <summary>校验 AudioGroup 是否落在 Type 允许子集内。</summary>
        internal static bool IsGroupValid(EAudioType type, EAudioGroup group)
        {
            return AllowedGroups.TryGetValue(type, out var allowed)
                   && System.Array.IndexOf(allowed, group) >= 0;
        }

        /// <summary>专属 Cooldown&gt;0 优先，否则取全局默认。</summary>
        internal static float ResolveCooldown(float clipCooldown, float globalDefault)
        {
            return clipCooldown > 0f ? clipCooldown : globalDefault;
        }

        /// <summary>距上次播放不足 cd 秒则处冷却。cd&lt;=0 恒不冷却。</summary>
        internal static bool IsOnCooldown(float now, float lastPlay, float cd)
        {
            return cd > 0f && now - lastPlay < cd;
        }

        internal static bool HasFadeIn(EAudioFade fade) => fade == EAudioFade.In || fade == EAudioFade.Both;
        internal static bool HasFadeOut(EAudioFade fade) => fade == EAudioFade.Out || fade == EAudioFade.Both;

        // ───────────────────────── 校验 / 淡入 ─────────────────────────

        private static void ValidateGroups(IEnumerable<Audio> rows)
        {
            foreach (var row in rows)
            {
                if (!IsGroupValid(row.Type, row.AudioGroup))
                {
                    Log.Error($"[AudioConfigMgr] 音频 id={row.Id} 的 AudioGroup={row.AudioGroup} 不在 Type={row.Type} 允许子集内，配置需修正。");
                }
            }
        }

        // 淡入 best-effort：帧级音量斜坡（非 IO，走框架全局协程）。防御性——agent 被池满抢占 / 停止即放弃。
        private static void FadeIn(AudioAgent agent, float target, float seconds)
        {
            if (seconds <= 0f)
            {
                agent.Volume = target;
                return;
            }
            agent.Volume = 0f;
            Utility.Unity.StartGlobalCoroutine("AudioConfigMgr.FadeIn", FadeInRoutine(agent, target, seconds));
        }

        private static IEnumerator FadeInRoutine(AudioAgent agent, float target, float seconds)
        {
            // 播放身份令牌：agent 被通道池抢占 / 重载时 AudioData 会换成新对象，引用随之变化。
            // 每帧核对令牌，防止斜坡写到抢占后播放的另一条音频（配置层复用不动框架内核下的最小防护）。
            var token = agent?.AudioData;
            float t = 0f;
            while (t < seconds)
            {
                if (agent == null || agent.IsFree || !ReferenceEquals(agent.AudioData, token)) yield break;
                t += Time.unscaledDeltaTime;
                agent.Volume = Mathf.Lerp(0f, target, t / seconds);
                yield return null;
            }
            if (agent != null && !agent.IsFree && ReferenceEquals(agent.AudioData, token)) agent.Volume = target;
        }

        // ───────────────────────── 测试注入口（绕 ConfigSystem，EditMode / 纯 C# 单测用） ─────────────────────────

        /// <summary>测试注入：直接灌 id→行 映射并清空防连点计时。</summary>
        public static void InitForTest(IEnumerable<Audio> rows)
        {
            _cache = new Dictionary<int, Audio>();
            _lastPlayTime.Clear();
            if (rows != null)
            {
                foreach (var r in rows) _cache[r.Id] = r;
            }
        }

        /// <summary>清空缓存与计时（测试隔离用，下次取值重走 <see cref="EnsureLoaded"/>）。</summary>
        public static void ResetForTest()
        {
            _cache = null;
            _lastPlayTime.Clear();
        }
    }
}
