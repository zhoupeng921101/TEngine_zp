using Cysharp.Threading.Tasks;
using GameLogic.Activity;
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
    /// 玩法态（棋盘 / 得分）仍归 <see cref="BlockGameState"/>，二者分层：
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

        /// <summary>四玩法货币(Soul/Piety/Exp/Energy)本地视图 ↔ 服务端权威对账器(P2 全栈迁移·客户端段)。</summary>
        public MetaCurrencySync MetaCurrency { get; private set; }

        /// <summary>normal 订单本地视图 ↔ 服务端权威投影器(P1 全栈迁移·客户端段:登录快照/推送应用 + 交付 RPC 编排)。</summary>
        public OrderSync OrderSync { get; private set; }

        /// <summary>云存档同步编排(P3 全栈迁移·客户端段):进主游戏下载冲突解决 + 存档边界节流上传(只搬非货币非身份切片)。</summary>
        public CloudSaveSync CloudSave { get; private set; }

        /// <summary>进主游戏编排(全栈协议改动·客户端段):进融合主游戏时发一次 C2G_EnterMainGameRequest,把订单快照 + 云存档同包回带统一应用。</summary>
        public EnterMainGameSync EnterMainGame { get; private set; }

        /// <summary>服务端权威发牌预测/对账引擎(M3 客户端段:开局 C2G_GameStart / 落子 C2G_Place 预测对账 / 重连 C2G_GameSnapshot)。</summary>
        public ServerDealSync ServerDeal { get; private set; }

        /// <summary>远程 ledger 服务(我的流水查询,设计 46 客户端段)。</summary>
        public RemoteAttrLedgerService AttrLedger { get; private set; }

        /// <summary>远程活动服务(累计 N 局类活动 fire-and-forget +1,设计 48 客户端段)。</summary>
        public RemoteActivityService Activity { get; private set; }

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

            // 四货币对账器(P2 客户端段):复用同一 RPC 接缝(RpcGatewayProd 经 Session 发 C2G_PropertyChangeRequest);
            // 登录快照 → ApplySnapshot 覆盖本地视图 + 基线;落盘边界 → ReportPending 聚合上报。接线在 GameApp.StartGameLogic。
            MetaCurrency = new MetaCurrencySync(new RpcGatewayProd());

            // 订单服务端权威投影器(P1 客户端段):生产用 OrderRpcGatewayProd(经 Session 发 C2G_DeliverOrderRequest);
            // 登录/刷新推送 → OnSnapshotPush 应用快照;开窗 → OnMergeStateReady 切权威 + 接交付钩子。接线在 GameApp.StartGameLogic。
            OrderSync = new OrderSync(new OrderRpcGatewayProd());

            // 云存档同步(P3 客户端段):生产用 CloudSaveGatewayProd(上传路径仍经 Session 发 C2G_CloudSaveUploadRequest);
            // 下载冲突解决由进主游戏响应同包回带驱动(EnterMainGame),存档边界 → TryUploadThrottled。接线在 GameApp.StartGameLogic。
            CloudSave = new CloudSaveSync(new CloudSaveGatewayProd());

            // 进主游戏编排(全栈协议改动·客户端段):生产用 EnterMainGameGatewayProd(经 Session 发 C2G_EnterMainGameRequest);
            // 进融合主游戏(MainMenuWindow 开始游戏)时发请求,响应回带订单快照 → OrderSync、云存档 → CloudSave 统一应用。
            EnterMainGame = new EnterMainGameSync(new EnterMainGameGatewayProd(), OrderSync, CloudSave);

            // 服务端权威发牌预测/对账引擎(M3 客户端段):生产用 BlockGameGatewayProd(经 Session 发 C2G_GameStart/Place/GameSnapshot);
            // 开窗经 OnMergeStateReady 注入到 BlockGameState.ServerDeal,接线在 GameApp.StartGameLogic。
            ServerDeal = new ServerDealSync(new BlockGameGatewayProd());

            // 远程 ledger 服务(设计 46 客户端段):生产用 RemoteAttrLedgerSource(经 FantasyNetwork.Session 发 C2G_QueryAttrLedger);
            // 服务端独占审计完整性(44 §5.4),客户端不持本地副本,每次打开窗实时拉真协议。
            AttrLedger = new RemoteAttrLedgerService(new RemoteAttrLedgerSource());

            // 远程活动服务(设计 48 客户端段):生产用 RemoteActivityIncrementSource(经 FantasyNetwork.Session 发 C2G_ActivityIncrement);
            // GameOver hook fire-and-forget 调,服务端 counter $inc 原子幂等;客户端不持本地状态(沿设计 48 §3.3 不本地放行)。
            Activity = new RemoteActivityService(new RemoteActivityIncrementSource());
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
        /// 落地服务端登录签发/认领的权威 playerId（P0 全栈迁移·客户端段）：用服务端值覆盖内存 <see cref="Player"/>.Id，
        /// 并经 <see cref="SavePlayer"/> 持久化到 <see cref="MergeMetaSave.playerId"/>。此后会话内 <see cref="Player"/>.Id 一律以服务端值为准。
        /// </summary>
        /// <remarks>
        /// 调用方为 <c>GameApp.StartGameLogic</c> 订阅 <c>FantasyNetwork.OnPlayerIdIssued</c>（仅 ErrorCode==0 且非空时触发）。
        /// 落盘判据锚到「磁盘上的 playerId」而非内存 <see cref="Player"/>.Id：首装时内存占位 guid 恰与服务端认领值相同，
        /// 但该 guid 尚未落盘（<see cref="LoadPlayer"/> 走 CreateDefault 仅置内存）；只比内存会漏掉这次落盘，
        /// 导致重启时盘上无 playerId、被迫重生成新 guid 再上交（虽最终仍收敛回服务端值，但违「首登后盘上即为服务端值」验收）。
        /// 故只要磁盘值 != 服务端值（含磁盘缺失），即覆盖内存并落盘；磁盘已是服务端值则跳过冗余写盘。
        /// Player 为 null（OnInit 尚未跑）时直接返回——实际时序中登录回包远晚于 OnInit，此守卫仅兜底。
        /// </remarks>
        public void ApplyServerPlayerId(string serverPlayerId)
        {
            if (Player == null || string.IsNullOrEmpty(serverPlayerId)) return;
            Player.Id = serverPlayerId; // 内存权威无条件以服务端值为准
            // 磁盘已是服务端值 → 跳过冗余落盘；否则（含磁盘缺 playerId 字段 / 无档）落盘。
            var dto = MergeMetaPersistence.Load();
            if (dto != null && dto.playerId == serverPlayerId) return;
            SavePlayer(fireSavedHook: false); // 回灌写:身份不入云存档 blob,不触发存档边界钩子(避免无意义上传 + version 空涨)
        }

        /// <summary>
        /// 落地服务端登录四货币权威快照(P2 全栈迁移·客户端段)：用服务端值覆盖本地缓存 + 对账器基线 + 已开的玩法态。
        /// 验收:登录后四货币显示 = 服务端快照值(非本地旧值);单纯改本地 PlayerPrefs 四货币重登录被服务端覆盖。
        /// </summary>
        /// <remarks>
        /// 三处一并覆盖,保证后续任一读取路径都拿服务端值:
        /// ① 缓存 <see cref="MergeMetaSave"/>(soul/piety/exp/energy 字段):玩法窗稍后开窗经 ImportMeta 从缓存读,
        ///    故必须先把权威值写进缓存,否则开窗会用本地旧缓存覆盖显示;
        /// ② 对账器 <see cref="MetaCurrency"/> 基线:钉到服务端值,后续产销 delta 从此基准算;
        /// ③ 若玩法窗已开(MergeState 非空,如挂后台登录重连),直接覆盖活态字段,即时反映。
        /// 体力字段连带把 lastEnergyRegenTime 置 now:服务端值是「此刻权威体力」,本地预测恢复应从此刻起算
        /// (避免用旧记录时刻把已结算过的离线恢复再补一遍)。
        /// </remarks>
        public void ApplyServerCurrencySnapshot(long soul, long piety, long exp, long energy)
        {
            var live = BlockGameState.Instance;
            var state = (live != null && live.MergeOrderMode) ? live.MergeState : null;

            // ① + ② 经对账器:set 基线,若活态在则一并覆盖活态字段。
            MetaCurrency?.ApplySnapshot(state, soul, piety, exp, energy);

            // ③ 缓存:把四货币权威值写进 MergeMetaSave(保留其余元层字段),供稍后开窗 ImportMeta 读取。
            //    energy 连带把 lastEnergyRegenTime 置 now(权威体力从此刻起算本地预测恢复)。
            var dto = MergeMetaPersistence.Load() ?? new MergeMetaSave { version = MergeMetaPersistence.CurrentVersion };
            dto.soul = (int)soul;
            dto.piety = (int)piety;
            dto.exp = (int)exp;
            dto.energy = (int)energy;
            dto.lastEnergyRegenTime = MergeMetaPersistence.NowUnixSec();
            // 回灌写:货币 + lastEnergyRegenTime 均不入云存档 blob,触发存档边界钩子只会空跑货币上报 +
            // 传一份内容未变的 blob 空涨 version,故 fireSavedHook=false 只落本地缓存、不惊动上报/上传。
            MergeMetaPersistence.SaveAsync(dto, fireSavedHook: false).Forget();
        }

        /// <summary>
        /// 把当前 <see cref="Player"/> 落盘（设计 25 §5.2 H1）：先 <see cref="MergeMetaPersistence.Load"/>
        /// 读回既有 DTO（保留玩法元层字段，仅覆写玩家字段），再 <see cref="PlayerInfo.ExportToMeta"/> 写入、
        /// 经异步外壳 <see cref="MergeMetaPersistence.SaveAsync"/> 即发即忘落盘。
        /// 无既有 DTO（首次）→ 现场新建一份 DTO（version 由 Save 内序列化承接）。
        /// <paramref name="fireSavedHook"/>=false 用于服务端→本地的身份回写(<see cref="ApplyServerPlayerId"/>):
        /// 只落盘、不触发存档边界钩子(playerId 不入云存档 blob,非玩法事件)。
        /// </summary>
        public void SavePlayer(bool fireSavedHook = true)
        {
            if (Player == null) return;
            var dto = MergeMetaPersistence.Load() ?? new MergeMetaSave { version = MergeMetaPersistence.CurrentVersion };
            Player.ExportToMeta(dto);
            MergeMetaPersistence.SaveAsync(dto, fireSavedHook).Forget();
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
        /// 测试 / 注入入口:用指定 RPC 接缝重建 <see cref="MetaCurrency"/>(沿 <see cref="InitPlayerAttrWith"/> 范式)。
        /// EditMode 经它灌入桩 <see cref="IRpcGateway"/>,断言四货币聚合上报 + 对账,不连网。
        /// </summary>
        public void InitMetaCurrencyWith(IRpcGateway gateway)
        {
            MetaCurrency = new MetaCurrencySync(gateway);
        }

        /// <summary>
        /// 测试 / 注入入口:用指定 RPC 接缝重建 <see cref="OrderSync"/>(沿 <see cref="InitMetaCurrencyWith"/> 范式)。
        /// EditMode 经它灌入桩 <see cref="IOrderRpcGateway"/>,断言交付 RPC 编排 + 快照应用 + 失败退还库存,不连网。
        /// </summary>
        public void InitOrderSyncWith(IOrderRpcGateway gateway)
        {
            OrderSync = new OrderSync(gateway);
        }

        /// <summary>
        /// 测试 / 注入入口:用指定接缝(+ 可注入毫秒时钟便于断言节流)重建 <see cref="CloudSave"/>(沿 <see cref="InitMetaCurrencyWith"/> 范式)。
        /// EditMode 经它灌入桩 <see cref="ICloudSaveGateway"/>,断言下载冲突解决 / 上传节流 / Stale 让位,不连网。
        /// </summary>
        public void InitCloudSaveWith(ICloudSaveGateway gateway, System.Func<long> nowMsProvider = null)
        {
            CloudSave = new CloudSaveSync(gateway, nowMsProvider);
        }

        /// <summary>
        /// 测试 / 注入入口:用指定接缝重建 <see cref="EnterMainGame"/>(沿 <see cref="InitCloudSaveWith"/> 范式),
        /// 关联当前 <see cref="OrderSync"/> / <see cref="CloudSave"/>。EditMode 经它灌入桩 <see cref="IEnterMainGameGateway"/>,
        /// 断言响应应用 + 每次进入重对齐 + 失败兜底,不连网。
        /// </summary>
        public void InitEnterMainGameWith(IEnterMainGameGateway gateway)
        {
            EnterMainGame = new EnterMainGameSync(gateway, OrderSync, CloudSave);
        }

        /// <summary>
        /// 测试 / 注入入口:用指定数据源重建 <see cref="AttrLedger"/>(沿 <see cref="InitMailWithSource"/> 范式)。
        /// EditMode 经它灌入桩 <see cref="IAttrLedgerSource"/>(注桩响应各分支),断言流水查询 + 错误码归一,不连网(设计 46 PV6 / PV10 / PV11)。
        /// </summary>
        public void InitAttrLedgerWith(IAttrLedgerSource source)
        {
            AttrLedger = new RemoteAttrLedgerService(source);
        }

        /// <summary>
        /// 测试 / 注入入口:用指定数据源重建 <see cref="Activity"/>(沿 <see cref="InitAttrLedgerWith"/> 范式)。
        /// EditMode 经它灌入桩 <see cref="IActivityIncrementSource"/>(注桩响应各分支),
        /// 断言 hook fire-and-forget + 错误码归一 + 防重计数,不连网(设计 48 PV6 / PV9 / PV11 / PV12)。
        /// </summary>
        public void InitActivityWith(IActivityIncrementSource source)
        {
            Activity = new RemoteActivityService(source);
        }
    }
}
