using System.Collections.Generic;

namespace GameLogic.Mail
{
    /// <summary>
    /// 收件箱序列化 DTO（设计 21 §3.5）。<c>JsonUtility</c> 友好：把 <see cref="MailItem"/> 列表包在容器里可序列化
    /// （JsonUtility 不能直接序列化裸 List）。version 预留迁移（本轮恒 1，仿 14 MergeMetaSave）。
    /// </summary>
    [System.Serializable]
    public sealed class MailInboxSave
    {
        /// <summary>存档结构版本。</summary>
        public int version = 1;
        /// <summary>收件箱邮件列表。</summary>
        public List<MailItem> mails = new List<MailItem>();
    }

    /// <summary>
    /// 收件箱持久化接缝（设计 21 §3.5）。生产 / 测试两实现注入，使序列化层纯逻辑可单测。
    /// </summary>
    public interface IMailPersistence
    {
        /// <summary>读盘 / 反序列化；无键 / 空串 / 脏数据 → 空列表（不抛）。</summary>
        List<MailItem> Load();
        /// <summary>落盘 / 序列化整个收件箱。</summary>
        void Save(List<MailItem> inbox);
    }

    /// <summary>
    /// 生产持久化：经既有 <c>Persistence.Provider</c>（生产 PlayerPrefs / 测试 InMemory），
    /// 本系统专用键 <see cref="Key"/>，<c>JsonUtility</c> 序列化（设计 21 §3.5，复用既有接缝不另造存储栈）。
    /// </summary>
    /// <remarks>
    /// PlayerPrefs 为非阻塞内存级读写（不触「禁阻塞 IO」红线，同 14/19/20 口径）；
    /// <see cref="Load"/> 对无键 / 空串 / 非法 JSON 统一产出合法空列表（try/catch 包 FromJson，同 14 Deserialize 口径），
    /// 本地单机文件可被篡改，反序列化对任意输入不抛。
    /// </remarks>
    public sealed class MailPersistence : IMailPersistence
    {
        /// <summary>本系统专用键，不与框架 / 其它系统键冲突。</summary>
        public const string Key = "Mail.Inbox";

        public List<MailItem> Load()
        {
            try
            {
                if (!GameLogic.BlockBlast.Persistence.Provider.TryGet(Key, out var raw) || string.IsNullOrEmpty(raw))
                    return new List<MailItem>();
                var dto = UnityEngine.JsonUtility.FromJson<MailInboxSave>(raw);
                return dto?.mails ?? new List<MailItem>();
            }
            catch
            {
                return new List<MailItem>(); // 非 JSON / 截断 → 合法空列表，不抛
            }
        }

        public void Save(List<MailItem> inbox)
        {
            try
            {
                var dto = new MailInboxSave { mails = inbox ?? new List<MailItem>() };
                GameLogic.BlockBlast.Persistence.Provider.Set(Key, UnityEngine.JsonUtility.ToJson(dto));
            }
            catch { /* ignore：落盘失败不阻断玩法（仿 MergeMetaPersistence.SaveAsync） */ }
        }
    }

    /// <summary>
    /// 测试持久化：内存直存直取，往返断言不污染真实 PlayerPrefs（设计 21 §3.5）。
    /// 跨「实例」往返用同一 <see cref="InMemoryMailPersistence"/> 实例模拟（与 PersistenceRedeemStore 跨实例复用同 provider 同源）。
    /// </summary>
    public sealed class InMemoryMailPersistence : IMailPersistence
    {
        private List<MailItem> _store = new List<MailItem>();

        public List<MailItem> Load()
        {
            // 返回拷贝：避免服务持有的列表与本存储别名（服务 new 实例时应读到独立副本，模拟重启）
            return new List<MailItem>(_store);
        }

        public void Save(List<MailItem> inbox)
        {
            _store = new List<MailItem>(inbox ?? new List<MailItem>());
        }
    }
}
