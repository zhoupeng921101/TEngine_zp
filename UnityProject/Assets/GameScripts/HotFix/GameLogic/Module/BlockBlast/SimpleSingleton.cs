namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 轻量单例基类。与 TEngine 的 Singleton&lt;T&gt; 不同：不依赖 SingletonSystem.Retain
    /// （后者在 EditMode 测试里会因 IUpdateDriver 缺失 NRE）。
    /// 业务上若需要 Update/Release 生命周期，再考虑迁回 TEngine.Singleton。
    /// </summary>
    /// <typeparam name="T">子类类型。</typeparam>
    public abstract class SimpleSingleton<T> where T : SimpleSingleton<T>, new()
    {
        protected static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new T();
                    _instance.OnInit();
                }
                return _instance;
            }
        }

        public static bool IsValid => _instance != null;

        protected virtual void OnInit() { }

        protected virtual void OnRelease() { }

        public void Release()
        {
            OnRelease();
            _instance = null;
        }
    }
}
