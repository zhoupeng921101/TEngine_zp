namespace GameLogic.Settings
{
    /// <summary>设置项类型（提示文案查表用）。</summary>
    public enum SettingKind
    {
        Music,
        Sound,
    }

    /// <summary>
    /// 设置服务（设计 19 §3.4）：编排「改模型 → 落盘 → 应用音频 → 出提示文案」。
    /// 纯逻辑可单测——持有 <see cref="AudioSettings"/> + 注入的 <see cref="ISettingsStore"/> + 可注入 <see cref="AudioSink"/>，
    /// 不引用真实音频模块、不碰真实 PlayerPrefs。
    /// </summary>
    /// <remarks>
    /// 落盘键直接引用 <c>TEngine.Constant.Setting.MusicMuted/SoundMuted</c>，不硬编码字面量（防与框架键漂移，设计 19 §3.2）。
    /// 键存「静音（muted）」语义、模型存「开（on）」语义，落盘/加载取反：muted=false 即 on=true。
    /// </remarks>
    public sealed class SettingsService
    {
        /// <summary>当前音频设置（默认全开，可被 <see cref="Load"/> 覆盖）。</summary>
        public AudioSettings Audio { get; private set; } = AudioSettings.CreateDefault();

        private readonly ISettingsStore _store;

        /// <summary>
        /// 应用接缝（副作用）：参数为 (musicOn, soundOn)。可空（未接 UI 时不应用）。
        /// 生产侧注入推 <c>GameModule.Audio.MusicEnable/SoundEnable</c> 的 lambda；测试侧注入记录调用的 lambda（设计 19 §3.3）。
        /// </summary>
        public System.Action<bool, bool> AudioSink;

        public SettingsService(ISettingsStore store) => _store = store;

        /// <summary>从存储加载（muted 取反为 on）。无键 → 默认全开。</summary>
        public void Load()
        {
            Audio.MusicOn = !_store.GetBool(TEngine.Constant.Setting.MusicMuted, false);
            Audio.SoundOn = !_store.GetBool(TEngine.Constant.Setting.SoundMuted, false);
        }

        /// <summary>设置音乐开关：改模型 → 落盘 → 应用音频。</summary>
        public void SetMusic(bool on)
        {
            Audio.MusicOn = on;
            Persist();
            Apply();
        }

        /// <summary>设置音效开关：改模型 → 落盘 → 应用音频。</summary>
        public void SetSound(bool on)
        {
            Audio.SoundOn = on;
            Persist();
            Apply();
        }

        /// <summary>翻转音乐开关，返回新值。</summary>
        public bool ToggleMusic()
        {
            SetMusic(!Audio.MusicOn);
            return Audio.MusicOn;
        }

        /// <summary>翻转音效开关，返回新值。</summary>
        public bool ToggleSound()
        {
            SetSound(!Audio.SoundOn);
            return Audio.SoundOn;
        }

        /// <summary>落盘到框架既有键（on → !muted 取反）。</summary>
        private void Persist()
        {
            _store.SetBool(TEngine.Constant.Setting.MusicMuted, !Audio.MusicOn);
            _store.SetBool(TEngine.Constant.Setting.SoundMuted, !Audio.SoundOn);
        }

        /// <summary>应用到音频模块（经可注入 sink，null 时不应用）。</summary>
        private void Apply() => AudioSink?.Invoke(Audio.MusicOn, Audio.SoundOn);

        /// <summary>
        /// 点击提示文案 textId（spec：「音乐/音效已打开/已关闭」共四条）。
        /// 多语言真实查表延后（设计 19 §七 O6，同 num/item/reward 的 NameTextId 现状）。
        /// </summary>
        public static int ToggleTipTextId(SettingKind kind, bool on)
        {
            switch (kind)
            {
                case SettingKind.Music: return on ? SettingsText.MusicOn : SettingsText.MusicOff;
                case SettingKind.Sound: return on ? SettingsText.SoundOn : SettingsText.SoundOff;
                default: return 0;
            }
        }
    }
}
