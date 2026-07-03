using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using TEngine;
using GameLogic.BlockBlast;

namespace GameLogic
{
    /// <summary>
    /// 主菜单（玩法融合单入口，设计 29 §3.1）：标题 + 「开始游戏」按钮 → 融合玩法窗口 + 历史最高分。
    /// 经典纯无尽（GameWindow）入口下线，融合主玩法 = 承载完整经济的 MergeOrderWindow。
    /// </summary>
    [Window(UILayer.UI, location: "MainMenuWindow", fullScreen: true)]
    public sealed class MainMenuWindow : UIWindowMono
    {
        /// <summary>进玩法入口等进主游戏响应对齐时的看门狗超时(毫秒)。慢网偶发短等;超时按本地兜底放行,绝不卡死。</summary>
        private const int EnterReadyTimeoutMs = 8000;

        /// <summary>进窗防重入。等待就绪期间二次点击「开始游戏」只进窗一次。</summary>
        private bool _entering;

        /// <summary>主榜(周榜)id：与 <see cref="GameLogic.UI.RankWindow"/> 默认展示榜一致，个人最佳分投影读此榜。</summary>
        private const int MainRankId = 1;

        protected override void OnCreate()
        {
            // 最高分权威源 = 排行榜个人最佳分（服务端 RankScoreDoc.BestScore，经 G2C_RankQueryResponse.MyScore 下发 →
            // RankService 本地展示缓存投影）。元层 highScore 不再是最高分权威载体，此处不再从元层读。
            int bestScore = LoadBestScoreFromRank();

            var content = UGuiFactory.CreateContentPanel(rectTransform);
            float cx = BlockLayout.DesignWidth / 2f;

            UGuiFactory.CreateImage(content, "Bg", cx, BlockLayout.DesignHeight / 2f,
                BlockLayout.DesignWidth, BlockLayout.DesignHeight, BlockLayout.BgColor);

            // 标题
            UGuiFactory.CreateText(content, "Title", cx, 461, 1008, 158, "BLOCK BLAST", 104,
                new Color32(0xff, 0xe0, 0x66, 0xFF));
            UGuiFactory.CreateText(content, "Sub", cx, 590, 1008, 72, "合成订单 · 经典无尽融合", 37,
                new Color32(0x88, 0xaa, 0xcc, 0xFF));

            // 「开始游戏」单入口 → 融合玩法（MergeOrderWindow 承载完整经济，设计 29 §3.1）
            var btn = UGuiFactory.CreateButton(content, "BtnStart", cx, 1037, 677, 216, "开始游戏", 75,
                new Color32(0x44, 0x77, 0xff, 0xFF), Color.white, out _, out _);
            UGuiFactory.CreateText(content, "StartSub", cx, 1138, 677, 58, "落子·消除·合成·订单 · 完成 5 单通关", 32,
                new Color32(0xdd, 0xee, 0xff, 0xFF));
            // 进玩法入口闸:进窗前先发进主游戏请求等订单快照对齐(常态点按钮时抢跑已完成 → 零等待;
            // 慢网偶发才短暂等,期间禁用按钮)。配看门狗超时,超时按本地兜底放行。
            btn.onClick.AddListener(() => EnterMergeOrder(btn).Forget());

            // BEST（读排行榜个人最佳分投影，非元层/blob）
            UGuiFactory.CreateText(content, "Best", cx, 1469, 677, 72, $"BEST  {bestScore}", 46,
                new Color32(0xaa, 0xbb, 0xdd, 0xFF));

            // 设置入口（设计 23 §八 B5：本轮入口落主菜单；玩法 HUD 齿轮入口后续轮次）
            var btnSettings = UGuiFactory.CreateButton(content, "BtnSettings", cx, 1642, 518, 138, "设置", 52,
                new Color32(0x77, 0x88, 0x99, 0xFF), Color.white, out _, out _);
            btnSettings.onClick.AddListener(() =>
            {
                GameModule.UI.ShowUIAsync<GameLogic.UI.SettingsWindow>();
            });

            // 个人信息入口（设计 25 §八 D6：本轮入口落主菜单，照 BtnSettings 做法；玩法 HUD 顶栏头像入口后续轮）
            var btnPlayerInfo = UGuiFactory.CreateButton(content, "BtnPlayerInfo", cx, 1800, 518, 138, "个人信息", 52,
                new Color32(0x99, 0x77, 0x88, 0xFF), Color.white, out _, out _);
            btnPlayerInfo.onClick.AddListener(() =>
            {
                GameModule.UI.ShowUIAsync<GameLogic.UI.PlayerInfoWindow>();
            });

            // 排行榜入口（设计 28 §八：本轮入口落主菜单，照 BtnPlayerInfo 做法；玩法 HUD / 结算窗入口后续轮）
            var btnRank = UGuiFactory.CreateButton(content, "BtnRank", cx, 1958, 518, 138, "排行榜", 52,
                new Color32(0x88, 0x99, 0x77, 0xFF), Color.white, out _, out _);
            btnRank.onClick.AddListener(() =>
            {
                GameModule.UI.ShowUIAsync<GameLogic.UI.RankWindow>();
            });

            // 背包入口（背包系统·客户端段）：左下角，开悬浮背包窗（非弹框，主菜单仍可见）。
            var btnBackpack = UGuiFactory.CreateButton(content, "BtnBackpack", 180, 1760, 260, 150, "背包", 56,
                new Color32(0x7B, 0x86, 0xC2, 0xFF), Color.white, out _, out _);
            btnBackpack.onClick.AddListener(() =>
            {
                GameModule.UI.ShowUIAsync<GameLogic.BackpackWindow>();
            });

        }

        /// <summary>
        /// 「开始游戏」进玩法编排:发一次进主游戏请求(EnterMainGame,决策②每次进入重新对齐),服务端一次性原子响应
        /// 回带订单快照(配看门狗超时)→ 关主菜单 + 开融合玩法窗。
        /// 由 Button.onClick 经 .Forget() 调用(等价 async void),故全程 try/catch 兜底、异常不外逃;
        /// _entering 防重入保证等待期二次点击只进窗一次。就绪/超时后再 Close+Show,MergeOrderWindow.OnCreate 读到的本地键已是服务端对齐后投影。
        /// </summary>
        private async UniTaskVoid EnterMergeOrder(Button btn)
        {
            if (_entering) return;
            _entering = true;
            if (btn != null) btn.interactable = false; // 等待期禁用,慢网时给轻量「不可再点」反馈

            try
            {
                var enter = GameContext.Instance?.EnterMainGame;
                if (enter != null)
                {
                    // 看门狗:进主游戏响应应用完成与超时谁先到都放行。超时→按本地兜底进入(ResetForMergeOrder 回落本地),绝不卡死。
                    // EnterAsync 自带防重入 + 失败降级(请求失败用本地兜底、不清空本地订单),故此处只需配超时。
                    await UniTask.WhenAny(enter.EnterAsync(), UniTask.Delay(EnterReadyTimeoutMs, ignoreTimeScale: true));
                }

                GameModule.UI.CloseUI<MainMenuWindow>();
                GameModule.UI.ShowUIAsync<MergeOrderWindow>();
            }
            catch (System.Exception e)
            {
                // 任何异常都不得让 async void 逃逸崩主菜单:本地兜底放行。
                Log.Warning($"[MainMenuWindow] 进玩法发进主游戏请求异常,按本地兜底放行:{e.Message}");
                GameModule.UI.CloseUI<MainMenuWindow>();
                GameModule.UI.ShowUIAsync<MergeOrderWindow>();
            }
            // 不重置 _entering / 按钮 interactable:成功路径下本窗已 Close 销毁,无需还原。
        }

        /// <summary>
        /// 读排行榜个人最佳分投影(主榜 <see cref="MainRankId"/>)作 BEST 展示。RankService 的本地展示缓存是服务端
        /// 权威最佳分(RankScoreDoc.BestScore / MyScore)的投影(离线可丢缓存,登录/查榜/提交时由服务端刷新),
        /// 非本地权威。同步读缓存(不阻塞、不发 RPC),无缓存 → 0。
        /// </summary>
        private static int LoadBestScoreFromRank()
        {
            var rank = GameContext.Instance?.Rank;
            if (rank == null) return 0;
            long best = rank.GetMyBest(MainRankId).score;
            return best > 0 ? (int)best : 0;
        }
    }
}
