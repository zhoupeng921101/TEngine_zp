using Cysharp.Threading.Tasks;
using GameLogic.AttrLedger;
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

        /// <summary>排行榜服务（查榜 / 我的名次 / 每日 + 点赞领取 / 红点查询,设计 22 数据层;结算编排上移服务端,设计 33)。</summary>
        public RankService Rank { get; private set; }

        /// <summary>远程邮件服务（运营来源拉列表 + 领奖走服务端校验，设计 32 客户端段）。</summary>
        public RemoteMailService Mail { get; private set; }

        /// <summary>玩家元层属性服务(Coin/Diamond/Stamina 客户端账本视图,设计 38 客户端段)。</summary>
        public PlayerAttrService PlayerAttr { get; private set; }

        /// <summary>远程 ledger 服务(我的流水查询,设计 46 客户端段)。</summary>
        public RemoteAttrLedgerService AttrLedger { get; private set; }

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

            // 远程邮件服务（设计 32 客户端段）：运营来源拉列表 + 领奖走服务端校验。
            InitMail();

            // 玩家元层属性服务(设计 38 客户端段):生产用 RpcGatewayProd(经 FantasyNetwork.Session 发协议);
            // FantasyNetwork.On* 事件订阅在 GameApp.StartGameLogic 内挂(GameContext 不直接 using FantasyClient,
            // 沿设计 38 §五接线落点;Fantasy 程序集受 FANTASY_UNITY 约束,事件订阅须在 #if 内)。
            PlayerAttr = new PlayerAttrService(new RpcGatewayProd());

            // 远程 ledger 服务(设计 46 客户端段):生产用 RemoteAttrLedgerSource(经 FantasyNetwork.Session 发 C2G_QueryAttrLedger);
            // 服务端独占审计完整性(44 §5.4),客户端不持本地副本,每次打开窗实时拉真协议。
            AttrLedger = new RemoteAttrLedgerService(new RemoteAttrLedgerSource());
        }

        /// <summary>
        /// 装配远程邮件服务（生产接缝：远程来源 <see cref="RemoteMailSource"/>，设计 32 客户端段）。
        /// 运营邮件来源唯一在服务端（设计 32 读前必看第 1 条），客户端不持第二份运营来源；
        /// 领奖走服务端校验（防重 + 抽奖 + 过期），断服不本地放行（设计 32 §四）。
        /// </summary>
        private void InitMail()
        {
            Mail = new RemoteMailService(new RemoteMailSource());
        }

        /// <summary>
        /// 装配排行榜服务（生产接缝：本地源 + 生产持久化 + 邮件服务 + 默认配置源，设计 28 §四）。
        /// 本机成绩来自 <see cref="RankService.GetMyBest"/>（自身进度），陪榜由配置 / 注入基准分（无随机 NPC，设计 22 §3.6）；
        /// 本轮 filler=null，离线榜可能只本机一条（陪榜待运营内容，设计 28 §十一 BLK1）。
        /// </summary>
        /// <remarks>
        /// selfProvider 存在「服务引用源、源引用服务」的循环，用「先声明 svc、闭包捕获、后赋值」打破
        /// （同设计 22 测试 SK1 / 持久化往返用例写法）。邮件服务取真实生产实例
        /// <c>new MailboxService(new MailPersistence())</c>(设计 21 数据层的本机收件接口,设计 28 §四 B1 路②):
        /// 排名层每日 / 点赞奖经它真实下发进收件箱存档(结算奖经设计 33 服务端发奖入口投出,不走此接口);
        /// 邮件表现层落地后此处零改动复用同一持久化键 <c>Mail.Inbox</c>,奖即在邮件窗可见(MailboxService 类头已声明此接法)。
        /// </remarks>
        private void InitRank()
        {
            var persist = new RankPersistence();                       // 键 Rank.Progress，复用 Persistence.Provider
            var mail = new MailboxService(new MailPersistence());      // 真实发奖出口（键 Mail.Inbox，设计 21）
            RankService svc = null;
            var source = new LocalRankSource(
                id => svc.GetMyBest(id),                               // 本机成绩（闭包捕获后赋值的 svc）
                _ => null);                                            // 陪榜：本轮无（待运营内容，设计 28 §十一 BLK1）
            var remote = new RemoteRankSource();                       // 远程源（上后端，设计 31）：发上报 / 查榜 RPC、服务端权威排序
            // cfg 默认包 RankConfigMgr（运行期走 ConfigSystem）；remote 非 null → 异步入口优先 RPC、断服回退 source（设计 31 §四）。
            svc = new RankService(source, persist, mail, cfg: null, remote: remote);
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
        public void InitRankWithDeps(IRankSource source, IRankPersistence persist, IMailService mail,
                                     IRankConfigSource cfg = null, IRemoteRankSource remote = null)
        {
            Rank = new RankService(source, persist, mail, cfg, remote);
        }

        /// <summary>
        /// 测试 / 注入入口：用指定远程来源重建 <see cref="Mail"/>（仿 <see cref="InitRankWithDeps"/>）。
        /// EditMode 经它灌入桩 <see cref="IRemoteMailSource"/>（注桩响应各分支），断言拉列表 / 领奖分发，不连网（设计 32 §8.2 CV）。
        /// </summary>
        public void InitMailWithSource(IRemoteMailSource remote)
        {
            Mail = new RemoteMailService(remote);
        }

        /// <summary>
        /// 测试 / 注入入口：用指定 RPC 接缝重建 <see cref="PlayerAttr"/>(沿 <see cref="InitMailWithSource"/> 范式)。
        /// EditMode 经它灌入桩 <see cref="IRpcGateway"/>(注桩响应各分支),断言改名扣钻 + 余额刷新,不连网(设计 38 §9.1 CV9)。
        /// </summary>
        public void InitPlayerAttrWith(IRpcGateway gateway)
        {
            PlayerAttr = new PlayerAttrService(gateway);
        }

        /// <summary>
        /// 测试 / 注入入口:用指定数据源重建 <see cref="AttrLedger"/>(沿 <see cref="InitMailWithSource"/> 范式)。
        /// EditMode 经它灌入桩 <see cref="IAttrLedgerSource"/>(注桩响应各分支),断言流水查询 + 错误码归一,不连网(设计 46 PV6 / PV10 / PV11)。
        /// </summary>
        public void InitAttrLedgerWith(IAttrLedgerSource source)
        {
            AttrLedger = new RemoteAttrLedgerService(source);
        }
    }
}
