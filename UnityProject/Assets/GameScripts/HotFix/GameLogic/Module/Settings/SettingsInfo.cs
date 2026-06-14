namespace GameLogic.Settings
{
    /// <summary>
    /// 设置信息 getter（设计 19 §3.5）：版本号 / 用户 ID 两个薄查询。
    /// 经可注入 provider 使纯逻辑可测（版本号默认 <c>Application.version</c>，可注入固定值断言；
    /// 用户 ID 复用玩家信息（设计 18）的 <c>PlayerInfo.Id</c>）。
    /// </summary>
    public static class SettingsInfo
    {
        /// <summary>版本号 provider，默认 <c>Application.version</c>；测试可注入固定值。</summary>
        public static System.Func<string> VersionProvider = () => UnityEngine.Application.version;

        /// <summary>当前版本号显示文本（spec：当前版本号显示）。</summary>
        public static string Version() => VersionProvider();

        /// <summary>
        /// 用户 ID（spec：用户 ID 查看）。复用玩家信息（设计 18）的本地唯一 id；入参 null → 返空串（不抛）。
        /// 复制按钮 UI 接时直接调设计 18 既有 <c>ClipboardUtil.Copy</c>，本层不重复造剪贴板工具。
        /// </summary>
        public static string UserId(GameLogic.BlockBlast.Player.PlayerInfo p) => p?.Id ?? string.Empty;
    }
}
