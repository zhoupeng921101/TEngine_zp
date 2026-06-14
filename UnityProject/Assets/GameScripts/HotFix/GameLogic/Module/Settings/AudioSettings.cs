namespace GameLogic.Settings
{
    /// <summary>
    /// 音频设置数据模型（设计 19 §3.1）。纯 POCO、两个 bool、默认全开（spec：默认全部打开）。
    /// 无 Unity 运行时依赖，单测可直接 <c>new</c>（不触 YooAsset / 音频模块）。
    /// </summary>
    /// <remarks>
    /// 「开（on）」语义。存储层落盘到框架 <c>Constant.Setting.MusicMuted/SoundMuted</c>「静音（muted）」键时取反映射，
    /// 使启动流程 <c>ProcedureLaunch.InitSoundSettings()</c>（读 <c>!GetBool(MusicMuted,false)</c>）零改动即生效（设计 19 §3.2）。
    /// 本轮不建音量滑条：spec 只给开/关，框架虽有 <c>MusicVolume</c> 字段，不投放（设计 19 §七 O7）。
    /// </remarks>
    [System.Serializable]
    public sealed class AudioSettings
    {
        /// <summary>音乐开关，默认打开（spec：默认全部打开）。</summary>
        public bool MusicOn = true;

        /// <summary>音效开关，默认打开（spec：默认全部打开）。</summary>
        public bool SoundOn = true;

        /// <summary>默认全开（无存档时的初始态）。</summary>
        public static AudioSettings CreateDefault() => new AudioSettings();
    }
}
