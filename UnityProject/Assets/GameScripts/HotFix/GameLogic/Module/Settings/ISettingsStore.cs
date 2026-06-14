namespace GameLogic.Settings
{
    /// <summary>
    /// 设置存储接缝（设计 19 §3.2）。隔离 PlayerPrefs，使单测能注入 InMemory 实现断言往返、不碰真实 PlayerPrefs。
    /// 仿工程既有 <c>GameLogic.BlockBlast.IPersistenceProvider</c> 做法（生产 PlayerPrefs / 测试 InMemory）。
    /// </summary>
    public interface ISettingsStore
    {
        bool GetBool(string key, bool defaultValue);
        void SetBool(string key, bool value);
    }

    /// <summary>
    /// 生产实现：直写框架既有 <c>Constant.Setting</c> 键（经 <c>TEngine.Utility.PlayerPrefs</c>），
    /// 故启动流程 <c>ProcedureLaunch.InitSoundSettings()</c> 零改动即生效（设计 19 §2.2）。
    /// </summary>
    /// <remarks>
    /// <c>TEngine.Utility.PlayerPrefs</c> 为非阻塞 KV 读写，不触「禁同步阻塞 IO」红线（同 14 save-system 口径）。
    /// </remarks>
    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        public bool GetBool(string key, bool def) => TEngine.Utility.PlayerPrefs.GetBool(key, def);

        public void SetBool(string key, bool v)
        {
            TEngine.Utility.PlayerPrefs.SetBool(key, v);
            TEngine.Utility.PlayerPrefs.Save();
        }
    }

    /// <summary>测试实现：内存字典，往返断言不污染真实 PlayerPrefs（设计 19 §3.2 / §六 P）。</summary>
    public sealed class InMemorySettingsStore : ISettingsStore
    {
        private readonly System.Collections.Generic.Dictionary<string, bool> _m =
            new System.Collections.Generic.Dictionary<string, bool>();

        public bool GetBool(string key, bool def) => _m.TryGetValue(key, out var v) ? v : def;
        public void SetBool(string key, bool v) => _m[key] = v;
    }
}
