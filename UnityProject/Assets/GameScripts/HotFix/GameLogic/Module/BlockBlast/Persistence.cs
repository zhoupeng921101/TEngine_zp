using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 持久化接口。生产用 PlayerPrefs，测试用 InMemory 注入避免污染。
    /// 对应源项目 localStorage 的轻量替代。
    /// </summary>
    public interface IPersistenceProvider
    {
        bool TryGet(string key, out string value);
        void Set(string key, string value);
        void Remove(string key);
    }

    /// <summary>PlayerPrefs 实现。</summary>
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

    /// <summary>测试用 In-Memory 实现。</summary>
    public sealed class InMemoryPersistenceProvider : IPersistenceProvider
    {
        private readonly Dictionary<string, string> _store = new Dictionary<string, string>();

        public bool TryGet(string key, out string value) => _store.TryGetValue(key, out value);
        public void Set(string key, string value) => _store[key] = value;
        public void Remove(string key) => _store.Remove(key);
        public void Clear() => _store.Clear();
        public int Count => _store.Count;
    }

    /// <summary>全局持久化访问入口。默认 PlayerPrefs；测试可换。</summary>
    public static class Persistence
    {
        private static IPersistenceProvider _provider;
        public static IPersistenceProvider Provider
        {
            get => _provider ??= new PlayerPrefsProvider();
            set => _provider = value;
        }
    }
}
