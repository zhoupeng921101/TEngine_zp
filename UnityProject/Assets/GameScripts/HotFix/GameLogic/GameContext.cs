using Cysharp.Threading.Tasks;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Player;
using GameLogic.Settings;

namespace GameLogic
{
    /// <summary>
    /// 运行期通用服务上下文（单例，设计 23 §五 方案 B）。
    /// 持有「数据层已建、需运行期持有者」的无主系统服务，提供统一初始化 + 统一接存档接缝。
    /// 玩法态（棋盘 / 得分 / 悔棋）仍归 <see cref="BlockGameState"/>，二者分层：
    /// 玩法态随开局 Reset，通用服务随会话长存。
    /// </summary>
    /// <remarks>
    /// 持有 <see cref="SettingsService"/>（设计 23）+ <see cref="PlayerInfo"/>（设计 25 兑现）；
    /// item / mail / rank 后续逐个挂入，接口预留薄而通用，不投机性预建成员。
    /// 启动接线（AudioSink + 首次 Load）在热更入口 <c>GameApp.StartGameLogic()</c> 完成
    /// （非热更区 ProcedureLaunch 引用不到本类，热更边界所致，设计 23 §五接线落点）。
    /// </remarks>
    public sealed class GameContext : SimpleSingleton<GameContext>
    {
        /// <summary>设置服务（音频开关 + 持久化 + 信息 getter，设计 19 数据层）。</summary>
        public SettingsService Settings { get; private set; }

        /// <summary>玩家个人信息（昵称 / 头像 / 等级 / 解锁集，设计 18 数据层）。</summary>
        public PlayerInfo Player { get; private set; }

        protected override void OnInit()
        {
            // 生产用框架键存储（PlayerPrefs），启动即从已保存的开关态加载。
            Settings = new SettingsService(new PlayerPrefsSettingsStore());
            Settings.Load();

            // 玩家信息：从既有存档 DTO 同步加载（设计 25 §5.2 B1）。
            // MergeMetaPersistence.Load 经 Persistence.Provider 同步读（PlayerPrefs 非阻塞内存级读，
            // 不触「禁阻塞 IO」红线，与 BlockGameState.Load 同口径），故不需异步外壳、无与异步加载的时序问题。
            LoadPlayer();
        }

        /// <summary>
        /// 从既有 <see cref="MergeMetaSave"/> 存档同步重建 <see cref="Player"/>：
        /// 有档 → <see cref="PlayerInfo.ImportFromMeta"/>（逐字段保底夹值，头像 id 越界退默认）；
        /// 无档 → <see cref="PlayerInfo.CreateDefault"/>（新 id + 系统名 + 默认头像/框）。
        /// </summary>
        private void LoadPlayer()
        {
            var rng = new System.Random();
            var dto = MergeMetaPersistence.Load();
            Player = (dto != null)
                ? PlayerInfo.ImportFromMeta(dto, rng, avatarValid: AvatarIdValid)
                : PlayerInfo.CreateDefault(rng);
        }

        /// <summary>
        /// 头像 id 是否在配置表内（接 <see cref="GameLogic.Config.AvatarConfigMgr"/>）。
        /// 配置经 ConfigSystem / YooAsset 加载，若启动时尚未就绪 / 表缺失会抛——此处吞异常退「视作有效」，
        /// 不因配置时序卡死启动（保底夹值的越界退默认是「正确性兜底」，配置不可达时宁可保留原 id）。
        /// </summary>
        private static bool AvatarIdValid(int id)
        {
            try { return GameLogic.Config.AvatarConfigMgr.GetAvatar(id) != null; }
            catch { return true; }
        }

        /// <summary>
        /// 把当前 <see cref="Player"/> 落盘（设计 25 §5.2 H1）：先 <see cref="MergeMetaPersistence.Load"/>
        /// 读回既有 DTO（保留玩法元层字段，仅覆写玩家字段），再 <see cref="PlayerInfo.ExportToMeta"/> 写入、
        /// 经异步外壳 <see cref="MergeMetaPersistence.SaveAsync"/> 即发即忘落盘。
        /// 无既有 DTO（首次）→ 现场新建一份 DTO（version 由 Save 内序列化承接）。
        /// </summary>
        public void SavePlayer()
        {
            if (Player == null) return;
            var dto = MergeMetaPersistence.Load() ?? new MergeMetaSave { version = MergeMetaPersistence.CurrentVersion };
            Player.ExportToMeta(dto);
            MergeMetaPersistence.SaveAsync(dto).Forget();
        }

        /// <summary>
        /// 测试 / 注入入口：用指定存储重建 <see cref="Settings"/> 并加载。
        /// 单测经 <see cref="InMemorySettingsStore"/> 断言往返，不污染真实 PlayerPrefs（设计 23 §9.1 H/W 组）。
        /// </summary>
        public void InitSettingsWithStore(ISettingsStore store)
        {
            Settings = new SettingsService(store);
            Settings.Load();
        }

        /// <summary>
        /// 测试 / 注入入口：用指定 DTO + rng 重建 <see cref="Player"/>（仿 <see cref="InitSettingsWithStore"/>）。
        /// dto 为 null → 走 <see cref="PlayerInfo.CreateDefault"/>；非 null → <see cref="PlayerInfo.ImportFromMeta"/>。
        /// 单测断言往返不碰真实 PlayerPrefs（设计 25 §9.1 H2）。<paramref name="avatarValid"/> 可选注入头像 id 校验。
        /// </summary>
        public void InitPlayerFromMeta(MergeMetaSave dto, System.Random rng, System.Func<int, bool> avatarValid = null)
        {
            Player = (dto != null)
                ? PlayerInfo.ImportFromMeta(dto, rng, avatarValid)
                : PlayerInfo.CreateDefault(rng);
        }
    }
}
