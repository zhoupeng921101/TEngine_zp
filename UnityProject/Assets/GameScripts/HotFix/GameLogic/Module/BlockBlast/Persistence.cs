using System.Collections.Generic;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 持久化接口。生产用 PlayerPrefs(<see cref="PlayerPrefsProvider"/>,独立文件,Unity 依赖隔离在那),
    /// 测试用 InMemory 注入避免污染。对应源项目 localStorage 的轻量替代。
    /// 本文件 0 Unity 依赖:生成核心若注入持久化只引用此接口,服务端无需 Unity stub 即可链接。
    /// </summary>
    public interface IPersistenceProvider
    {
        bool TryGet(string key, out string value);
        void Set(string key, string value);
        void Remove(string key);
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

    /// <summary>
    /// 全局持久化访问入口(客户端用)。默认工厂由客户端在启动期注册为 PlayerPrefs;
    /// 未注册时退回 InMemory(无 Unity),使纯逻辑/服务端链接本类不强拉 Unity。
    /// </summary>
    public static class Persistence
    {
        private static IPersistenceProvider _provider;

        /// <summary>
        /// 默认 provider 工厂。客户端启动期(GameApp.StartGameLogic)注册 PlayerPrefs 工厂,
        /// 把 Unity 依赖留在注册侧;本类自身不引用 PlayerPrefsProvider,保持 0 Unity 依赖。
        /// </summary>
        public static System.Func<IPersistenceProvider> DefaultProviderFactory;

        public static IPersistenceProvider Provider
        {
            get => _provider ??= (DefaultProviderFactory != null
                ? DefaultProviderFactory()
                : new InMemoryPersistenceProvider());
            set => _provider = value;
        }
    }
}
