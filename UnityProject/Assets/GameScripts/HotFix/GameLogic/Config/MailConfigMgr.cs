using System.Collections.Generic;
using GameLogic.Mail;

namespace GameLogic.Config
{
    /// <summary>
    /// 通用邮件底层配置管理器（设计 21 §3.2）。
    /// 桥接 Luban 生成的 <c>GameConfig.mail.TbMail / TbMailGlobal</c>（行类 <c>GameConfig.Mail / GameConfig.MailGlobal</c>）→ POCO
    /// （<see cref="MailDef"/> / <see cref="MailGlobalConfig"/>），让业务侧不直接依赖 Luban 类型。仿 <see cref="ItemConfigMgr"/>。
    /// </summary>
    /// <remarks>
    /// 加法式：注册表只持有元数据，不读写 <c>MergeOrderState</c>。
    /// 加载分两路：运行期 <see cref="EnsureLoaded"/> 走 <c>ConfigSystem</c>（YooAsset，需 Unity 运行时）；
    /// EditMode 单测经 <see cref="InitForTest"/> 注入，绕 ConfigSystem（纯逻辑可测）。
    /// 全局配置 <see cref="Global"/> 表缺省时返默认 100/30（spec 默认值兜底，不抛）。
    /// </remarks>
    public static class MailConfigMgr
    {
        private static Dictionary<int, MailDef> _mails;
        private static MailGlobalConfig _global;

        // ── 单行桥接：Luban 行 → POCO ─────────────────────────────

        /// <summary>邮件模板行：Luban <c>GameConfig.Mail</c> → POCO <see cref="MailDef"/>。</summary>
        public static MailDef ToMailDef(GameConfig.Mail row)
        {
            return new MailDef
            {
                Id           = row.Id,
                TitleTextId  = row.Title,
                BodyTextId   = row.Desc,
                ExpireDays   = row.ExpireDays,
                RewardPoolId = row.RewardId,
            };
        }

        /// <summary>全局配置行：Luban <c>GameConfig.MailGlobal</c> → POCO <see cref="MailGlobalConfig"/>。</summary>
        public static MailGlobalConfig ToGlobal(GameConfig.MailGlobal row)
            => new MailGlobalConfig { MaxCount = row.MaxCount, RetainDays = row.RetainDays };

        // ── 运行期加载（经 ConfigSystem / YooAsset）────────────────

        /// <summary>
        /// 运行期建缓存：经 <c>ConfigSystem</c>（YooAsset，需 Unity 运行时）。首次访问时灌入。
        /// 已灌（含 InitForTest 注入）则直接返回。全局配置表取首行；表空则留默认。
        /// </summary>
        public static void EnsureLoaded()
        {
            if (_mails != null) return;
            var tables = ConfigSystem.Instance.Tables;

            var mailTable = tables.TbMail;
            _mails = new Dictionary<int, MailDef>(mailTable.DataList.Count);
            foreach (var row in mailTable.DataList)
                _mails[row.Id] = ToMailDef(row);

            var globalTable = tables.TbMailGlobal;
            if (globalTable.DataList.Count > 0)
                _global = ToGlobal(globalTable.DataList[0]);
            // 表空 → _global 留 null，Global getter 兜底返默认
        }

        // ── 查询 ───────────────────────────────────────────────

        /// <summary>按模板 id 查；查不到返 null（不抛）。</summary>
        public static MailDef GetMail(int id)
        {
            EnsureLoaded();
            return _mails.TryGetValue(id, out var d) ? d : null;
        }

        /// <summary>全局配置（总有值：表缺则返默认 100/30）。</summary>
        public static MailGlobalConfig Global
        {
            get
            {
                EnsureLoaded();
                return _global ??= new MailGlobalConfig();
            }
        }

        // ── 测试注入 / 隔离 ─────────────────────────────────────

        /// <summary>
        /// 测试注入口：绕开 ConfigSystem，直接灌 POCO（EditMode / 纯 C# 单测用）。
        /// 灌入后 <see cref="EnsureLoaded"/> 不再触发 ConfigSystem。<paramref name="global"/> 为 null 时 <see cref="Global"/> 返默认。
        /// </summary>
        public static void InitForTest(IEnumerable<MailDef> mails, MailGlobalConfig global = null)
        {
            _mails = new Dictionary<int, MailDef>();
            if (mails != null)
                foreach (var d in mails) _mails[d.Id] = d;
            _global = global; // null → Global getter 兜底默认
        }

        /// <summary>清空缓存（测试隔离用，下次查询会重新走 EnsureLoaded）。</summary>
        public static void ResetForTest()
        {
            _mails = null;
            _global = null;
        }
    }
}
