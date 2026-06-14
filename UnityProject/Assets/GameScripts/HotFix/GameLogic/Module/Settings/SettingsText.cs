namespace GameLogic.Settings
{
    /// <summary>
    /// 提示文案 textId 占位常量（设计 19 §3.4 / §七 O6）。spec 的「音乐/音效已打开/已关闭」共四条提示。
    /// 本轮给 textId 枚举 + 占位值，UI 接多语言文本表时查表替换（与 num/item/reward 的 NameTextId 现状一致）。
    /// </summary>
    /// <remarks>
    /// 四个 textId 须互不相同且非 0（验收 S3：四组各返不同的非 0 textId）。占位段选 190xxx，避开既有系统的文本段。
    /// </remarks>
    public static class SettingsText
    {
        /// <summary>音乐已打开。</summary>
        public const int MusicOn = 190001;

        /// <summary>音乐已关闭。</summary>
        public const int MusicOff = 190002;

        /// <summary>音效已打开。</summary>
        public const int SoundOn = 190003;

        /// <summary>音效已关闭。</summary>
        public const int SoundOff = 190004;
    }
}
