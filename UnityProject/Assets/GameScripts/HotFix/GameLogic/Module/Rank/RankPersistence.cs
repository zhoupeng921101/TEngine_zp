using System.Collections.Generic;

namespace GameLogic.Rank
{
    /// <summary>
    /// 一个榜的本机元层进度（设计 22 §3.8）。JsonUtility 友好（字段全部基础类型）。
    /// 只存本机自己的数据；全服他人成绩不进盘（离线无，陪榜由配置生成）。
    /// </summary>
    [System.Serializable]
    public sealed class RankBoardProgress
    {
        /// <summary>所属榜 id。</summary>
        public int rankId;
        /// <summary>本机最佳成绩。</summary>
        public long bestScore;
        /// <summary>达到最佳分的时间（DateTime.Ticks；并列排序用）。</summary>
        public long bestAchievedTicks;
        /// <summary>上次领每日奖的日期（DateTime.Date.Ticks；跨天比对重置；0 = 从未领）。</summary>
        public long dailyClaimDateBin;
        /// <summary>上次领点赞奖的日期（DateTime.Date.Ticks；0 = 从未领）。</summary>
        public long praiseClaimDateBin;
    }

    /// <summary>
    /// 排行榜元层进度存档 DTO（设计 22 §3.8）。version 预留迁移（本轮恒 1，仿 21 MailInboxSave）。
    /// </summary>
    [System.Serializable]
    public sealed class RankProgressSave
    {
        /// <summary>存档结构版本。</summary>
        public int version = 1;
        /// <summary>各榜本机进度。</summary>
        public List<RankBoardProgress> boards = new List<RankBoardProgress>();
    }

    /// <summary>
    /// 排行榜元层进度持久化接缝（设计 22 §3.8）。生产 / 测试两实现注入，使序列化层纯逻辑可单测。
    /// </summary>
    public interface IRankPersistence
    {
        /// <summary>读盘 / 反序列化；无键 / 空串 / 脏数据 → 合法空（不抛）。</summary>
        RankProgressSave Load();
        /// <summary>落盘 / 序列化整个进度。</summary>
        void Save(RankProgressSave save);
    }

    /// <summary>
    /// 生产持久化：经既有 <c>Persistence.Provider</c>（生产 PlayerPrefs / 测试 InMemory），
    /// 本系统专用键 <see cref="Key"/>，<c>JsonUtility</c> 序列化（设计 22 §3.8，复用既有接缝不另造存储栈）。
    /// </summary>
    /// <remarks>
    /// PlayerPrefs 为非阻塞内存级读写（不触「禁阻塞 IO」红线，同 14/19/20/21 口径）；
    /// <see cref="Load"/> 对无键 / 空串 / 非法 JSON 统一产出合法空 <see cref="RankProgressSave"/>（try/catch 包 FromJson），
    /// 本地单机文件可被篡改，反序列化对任意输入不抛。
    /// </remarks>
    public sealed class RankPersistence : IRankPersistence
    {
        /// <summary>本系统专用键，不与框架 / 其它系统键冲突。</summary>
        public const string Key = "Rank.Progress";

        public RankProgressSave Load()
        {
            try
            {
                if (!GameLogic.BlockBlast.Persistence.Provider.TryGet(Key, out var raw) || string.IsNullOrEmpty(raw))
                    return new RankProgressSave();
                var dto = UnityEngine.JsonUtility.FromJson<RankProgressSave>(raw);
                if (dto == null) return new RankProgressSave();
                if (dto.boards == null) dto.boards = new List<RankBoardProgress>();
                return dto;
            }
            catch
            {
                return new RankProgressSave(); // 非 JSON / 截断 → 合法空，不抛
            }
        }

        public void Save(RankProgressSave save)
        {
            try
            {
                var dto = save ?? new RankProgressSave();
                if (dto.boards == null) dto.boards = new List<RankBoardProgress>();
                GameLogic.BlockBlast.Persistence.Provider.Set(Key, UnityEngine.JsonUtility.ToJson(dto));
            }
            catch { /* ignore：落盘失败不阻断玩法（同 21 MailPersistence.Save） */ }
        }
    }

    /// <summary>
    /// 测试持久化：内存直存直取，往返断言不污染真实 PlayerPrefs（设计 22 §3.8）。
    /// 跨「实例」往返用同一 <see cref="InMemoryRankPersistence"/> 实例模拟（同 21 InMemoryMailPersistence）。
    /// </summary>
    public sealed class InMemoryRankPersistence : IRankPersistence
    {
        private string _json;

        public RankProgressSave Load()
        {
            // 经 JSON 往返返回独立副本：服务 new 实例时读到独立对象，模拟重启
            if (string.IsNullOrEmpty(_json)) return new RankProgressSave();
            var dto = UnityEngine.JsonUtility.FromJson<RankProgressSave>(_json);
            if (dto == null) return new RankProgressSave();
            if (dto.boards == null) dto.boards = new List<RankBoardProgress>();
            return dto;
        }

        public void Save(RankProgressSave save)
        {
            var dto = save ?? new RankProgressSave();
            if (dto.boards == null) dto.boards = new List<RankBoardProgress>();
            _json = UnityEngine.JsonUtility.ToJson(dto);
        }
    }
}
