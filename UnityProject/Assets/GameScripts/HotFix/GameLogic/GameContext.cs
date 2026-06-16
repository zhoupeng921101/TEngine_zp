using Cysharp.Threading.Tasks;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Player;
using GameLogic.Mail;
using GameLogic.Rank;
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
    /// 持有 <see cref="SettingsService"/>（设计 23）+ <see cref="PlayerInfo"/>（设计 25 兑现）+ <see cref="RankService"/>（设计 28 兑现末项）；
    /// item / mail 后续逐个挂入，接口预留薄而通用，不投机性预建成员。
    /// 启动接线（AudioSink + 首次 Load）在热更入口 <c>GameApp.StartGameLogic()</c> 完成
    /// （非热更区 ProcedureLaunch 引用不到本类，热更边界所致，设计 23 §五接线落点）。
    /// </remarks>
    public sealed class GameContext : SimpleSingleton<GameContext>
    {
        /// <summary>设置服务（音频开关 + 持久化 + 信息 getter，设计 19 数据层）。</summary>
        public SettingsService Settings { get; private set; }

        /// <summary>玩家个人信息（昵称 / 头像 / 等级 / 解锁集，设计 18 数据层）。</summary>
        public PlayerInfo Player { get; private set; }

        /// <summary>排行榜服务（查榜 / 我的名次 / 每日 + 点赞领取 / 结算编排，设计 22 数据层）。</summary>
        public RankService Rank { get; private set; }

        protected override void OnInit()
        {
            // 生产用框架键存储（PlayerPrefs），启动即从已保存的开关态加载。
            Settings = new SettingsService(new PlayerPrefsSettingsStore());
            Settings.Load();

            // 玩家信息：从既有存档 DTO 同步加载（设计 25 §5.2 B1）。
            // MergeMetaPersistence.Load 经 Persistence.Provider 同步读（PlayerPrefs 非阻塞内存级读，
            // 不触「禁阻塞 IO」红线，与 BlockGameState.Load 同口径），故不需异步外壳、无与异步加载的时序问题。
            LoadPlayer();

            // 排行榜服务（设计 22 数据层，设计 28 §四装配）。
            InitRank();
        }

        /// <summary>
        /// 装配排行榜服务（生产接缝：本地源 + 生产持久化 + 邮件服务 + 默认配置源，设计 28 §四）。
        /// 本机成绩来自 <see cref="RankService.GetMyBest"/>（自身进度），陪榜由配置 / 注入基准分（无随机 NPC，设计 22 §3.6）；
        /// 本轮 filler=null，离线榜可能只本机一条（陪榜待运营内容，设计 28 §十一 BLK1）。
        /// </summary>
        /// <remarks>
        /// selfProvider 存在「服务引用源、源引用服务」的循环，用「先声明 svc、闭包捕获、后赋值」打破
        /// （同设计 22 测试 SK1 / 持久化往返用例写法）。邮件服务取真实生产实例
        /// <c>new MailboxService(new MailPersistence())</c>（设计 21 数据层的发奖出口，设计 28 §四 B1 路②）：
        /// 排名层点赞 / 结算奖经它真实下发进收件箱存档；邮件表现层（遗留 #26）落地后此处零改动复用
        /// 同一持久化键 <c>Mail.Inbox</c>，奖即在邮件窗可见（MailboxService 类头已声明此接法）。
        /// </remarks>
        private void InitRank()
        {
            var persist = new RankPersistence();                       // 键 Rank.Progress，复用 Persistence.Provider
            var mail = new MailboxService(new MailPersistence());      // 真实发奖出口（键 Mail.Inbox，设计 21）
            RankService svc = null;
            var source = new LocalRankSource(
                id => svc.GetMyBest(id),                               // 本机成绩（闭包捕获后赋值的 svc）
                _ => null);                                            // 陪榜：本轮无（待运营内容，设计 28 §十一 BLK1）
            svc = new RankService(source, persist, mail);             // cfg 默认包 RankConfigMgr（运行期走 ConfigSystem）
            Rank = svc;
        }

        /// <summary>
        /// 从既有 <see cref="MergeMetaSave"/> 存档同步重建 <see cref="Player"/> + 加载经典最高分（设计 29 §5.4）：
        /// 有档 → <see cref="PlayerInfo.ImportFromMeta"/>（逐字段保底夹值，头像 id 越界退默认）；
        /// 无档 → <see cref="PlayerInfo.CreateDefault"/>（新 id + 系统名 + 默认头像/框）。
        /// 同一份 DTO 同时把 <see cref="BlockGameState.HighScore"/> 并入元层加载（与玩家信息同时机）。
        /// </summary>
        private void LoadPlayer()
        {
            var rng = new System.Random();
            var dto = MergeMetaPersistence.Load();
            Player = (dto != null)
                ? PlayerInfo.ImportFromMeta(dto, rng, avatarValid: AvatarIdValid)
                : PlayerInfo.CreateDefault(rng);
            // 经典最高分并入元层时机（设计 29 §5.4）：与玩家信息同读一份 DTO；无档则保持缺省（首次无最高分）。
            BlockGameState.Instance.ImportHighScoreFromMeta(dto);
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
        /// 把经典最高分 <see cref="BlockGameState.HighScore"/> 落盘到元层（设计 29 §5.4），与 <see cref="SavePlayer"/>
        /// 同口径：先读回既有 DTO 保留其余元层字段，仅覆写 highScore，再经异步外壳即发即忘落盘。
        /// 在「最高分可能刷新」的结束/退出时机调用，使经典遗产与元层进度同一落盘节点。
        /// </summary>
        public void SaveHighScore()
        {
            var dto = MergeMetaPersistence.Load() ?? new MergeMetaSave { version = MergeMetaPersistence.CurrentVersion };
            BlockGameState.Instance.ExportHighScoreToMeta(dto);
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

        /// <summary>
        /// 测试 / 注入入口：用指定接缝重建 <see cref="Rank"/>（仿 <see cref="InitSettingsWithStore"/> / <see cref="InitPlayerFromMeta"/>）。
        /// EditMode 经它灌入 <see cref="InMemoryRankPersistence"/> + fake <see cref="IMailService"/>（如 RecordingMailService）
        /// + fake 配置源（或先 <c>RankConfigMgr.InitForTest</c> 后用默认源），断言 <see cref="GetBoard"/> 数据贯通，
        /// 不污染真实 PlayerPrefs / 不连网（设计 28 §四 / §九 H2/H3）。
        /// </summary>
        public void InitRankWithDeps(IRankSource source, IRankPersistence persist, IMailService mail, IRankConfigSource cfg = null)
        {
            Rank = new RankService(source, persist, mail, cfg);
        }
    }
}
