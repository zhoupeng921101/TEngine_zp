using UnityEngine;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// PlayerPrefs 持久化实现(客户端)。Unity 依赖隔离在此独立文件,
    /// 使 <see cref="Persistence"/> 接口/入口本身 0 Unity 依赖(生成核心闭包可不链接 Unity)。
    /// 由客户端启动期(GameApp)注册为 <see cref="Persistence.DefaultProviderFactory"/>。
    /// </summary>
    public sealed class PlayerPrefsProvider : IPersistenceProvider
    {
        public bool TryGet(string key, out string value)
        {
            if (PlayerPrefs.HasKey(key))
            {
                value = PlayerPrefs.GetString(key);
                return true;
            }
            value = null;
            return false;
        }

        public void Set(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }

        public void Remove(string key)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
