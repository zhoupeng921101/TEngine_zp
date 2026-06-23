using System.IO;
using NUnit.Framework;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 主玩法窗 GameWindow 塔罗换皮零回归测试（设计 27 §9.1 R/C 组）。
    ///
    /// 策略（同 SettlementWindowRegressionTests）：以静态读源核对 + 编译为主。
    /// GameWindow 玩法依赖 BlockGameState.Instance 运行期单例 + UIWindow 生命周期 + 拖拽指针事件，
    /// EditMode 反射驱动 OnCreate / 模拟拖拽落子成本高（设计 27 §9.1 warn）。
    /// 故 R 组主验收 = 编译 0 error（换皮未破坏类型/调用，本测试程序集能跑到即编译通过）
    /// + 逐条静态核对：玩法逻辑方法行 / BlockLayout 坐标常量 / 数据层字段 / 回调目标未改。
    /// 整局可玩（摆块→落子→消除→连击→补块→GameOver→分数滚动）须 Play/人眼手验，归 §9.2 V4/V5。
    ///
    /// 关键行定义：换皮前后逻辑不应变化的代码片段，一旦被删改则测试报告"换皮越界"。
    /// </summary>
    [TestFixture]
    public class GameWindowReskinRegressionTests
    {
        private const string GameWindowPath =
            "GameScripts/HotFix/GameLogic/UI/BlockBlastUI/GameWindow.cs";
        private const string BlockLayoutPath =
            "GameScripts/HotFix/GameLogic/UI/BlockBlastUI/BlockLayout.cs";
        private const string BlockGameStatePath =
            "GameScripts/HotFix/GameLogic/Module/BlockBlast/BlockGameState.cs";
        private const string MergeOrderWindowPath =
            "GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MergeOrderWindow.cs";

        private string _dataPath;

        [SetUp]
        public void SetUp()
        {
            _dataPath = UnityEngine.Application.dataPath;
        }

        private string ReadSource(string relPath)
        {
            var path = Path.Combine(_dataPath, relPath);
            Assert.IsTrue(File.Exists(path), $"源文件不存在: {path}");
            return File.ReadAllText(path);
        }

        // ── C1：源文件可读（程序集能编译才跑到这里，本例本身即编译自检）─────────────
        [Test]
        public void C1_SourceFilesExist_AndTestCanRun()
        {
            // 能执行到此处说明 BlockBlast.Tests 程序集已通过编译（GameWindow 换皮 0 error 约束）
            Assert.IsNotNull(ReadSource(GameWindowPath), "GameWindow.cs 可读");
            Assert.IsNotNull(ReadSource(BlockLayoutPath), "BlockLayout.cs 可读");
            Assert.IsNotNull(ReadSource(BlockGameStatePath), "BlockGameState.cs 可读");
        }

        // ── R1：玩法逻辑方法签名/关键行未被删改（只 BuildStaticUI 视觉部分变）───────────
        [Test]
        public void R1_GameWindow_GameplayMethodSignatures_Unchanged()
        {
            var src = ReadSource(GameWindowPath);

            // 所有动态玩法方法签名必须仍在（被删改 = 换皮越界）
            Assert.IsTrue(src.Contains("private void RenderBoard()"), "R1: RenderBoard 方法签名必须保留");
            Assert.IsTrue(src.Contains("private void RenderSlots()"), "R1: RenderSlots 方法签名必须保留");
            Assert.IsTrue(src.Contains("private void OnPieceBegin(int slotIdx)"), "R1: OnPieceBegin 必须保留");
            Assert.IsTrue(src.Contains("private void OnPieceDrag(int slotIdx, Vector2 containerAnchored)"), "R1: OnPieceDrag 必须保留");
            Assert.IsTrue(src.Contains("private void OnPieceEnd(int slotIdx)"), "R1: OnPieceEnd 必须保留");
            Assert.IsTrue(src.Contains("private void PlaceAndResolve(int slotIdx, BlockShape shape, int col, int row)"), "R1: PlaceAndResolve 必须保留");
            Assert.IsTrue(src.Contains("private void UpdateGhost(Vector2 containerAnchored)"), "R1: UpdateGhost 必须保留");
            Assert.IsTrue(src.Contains("private void InitGhostPool()"), "R1: InitGhostPool 必须保留");
            Assert.IsTrue(src.Contains("private (int col, int row) ComputeGridPos(Vector2 containerAnchored, BlockShape shape)"), "R1: ComputeGridPos 必须保留");
            Assert.IsTrue(src.Contains("private void TriggerGameOver()"), "R1: TriggerGameOver 必须保留");
            Assert.IsTrue(src.Contains("private void UpdateBest()"), "R1: UpdateBest 必须保留");
            Assert.IsTrue(src.Contains("protected override void OnUpdate()"), "R1: OnUpdate 分数滚动必须保留");
        }

        // ── R1b：落子/消除/补块/GameOver 关键调用链未改 ──────────────────────────────
        [Test]
        public void R1b_GameWindow_PlaceResolveChain_Unchanged()
        {
            var src = ReadSource(GameWindowPath);

            // 落子 + 消除 + 补块 + GameOver 全流程关键行
            Assert.IsTrue(src.Contains("_state.PlacePiece(slotIdx, _board, col, row)"), "R1b: 落子 PlacePiece 调用必须保留");
            Assert.IsTrue(src.Contains("_board.CanClearRowCols(true)"), "R1b: 消除判定 CanClearRowCols 必须保留");
            Assert.IsTrue(src.Contains("_state.ClearRowsAndCols(clear.Rows, clear.Cols)"), "R1b: 清行列 ClearRowsAndCols 必须保留");
            Assert.IsTrue(src.Contains("_state.RefillPieces(_board)"), "R1b: 补块 RefillPieces 必须保留");
            Assert.IsTrue(src.Contains("_board.CanPutBlock(shapeId, new Vec2Int(col, row))"), "R1b: 落点合法性 CanPutBlock 必须保留");
            Assert.IsTrue(src.Contains("!_board.CanPutAnyOf(remaining.ToArray())"), "R1b: GameOver 判定 CanPutAnyOf 必须保留");
            // 分数滚动逻辑
            Assert.IsTrue(src.Contains("_scoreText.text = _displayedScore.ToString()"), "R1b: 分数滚动写文本必须保留");
        }

        // ── R2：BlockLayout 棋盘坐标映射常量未改（换皮节点 position/size 沿用既有值）──────
        [Test]
        public void R2_BlockLayout_BoardCoordinateConstants_Unchanged()
        {
            var src = ReadSource(BlockLayoutPath);

            // 棋盘原点/格尺寸/棋盘像素（原生 1080 空间；动了落子对位偏移 = 玩法回归）
            Assert.IsTrue(src.Contains("public const int BoardSize = 8;"), "R2: BoardSize=8 不得改");
            Assert.IsTrue(src.Contains("public const float CellSize = 121f;"), "R2: CellSize=121 不得改");
            Assert.IsTrue(src.Contains("public const float BoardPixels = CellSize * BoardSize;"), "R2: BoardPixels 公式不得改");
            Assert.IsTrue(src.Contains("public const float BoardOriginX = (DesignWidth - BoardPixels) / 2f;"), "R2: BoardOriginX 公式不得改");
            Assert.IsTrue(src.Contains("public const float BoardOriginY = 432f;"), "R2: BoardOriginY=432 不得改");
            // 候选槽常量
            Assert.IsTrue(src.Contains("public const float SlotCell = 68f;"), "R2: SlotCell=68 不得改");
            Assert.IsTrue(src.Contains("public const float SlotY = 1584f;"), "R2: SlotY=1584 不得改");
            Assert.IsTrue(src.Contains("public const float SlotSpacing = 312f;"), "R2: SlotSpacing=312 不得改");
            Assert.IsTrue(src.Contains("public const float SlotCenterX = DesignWidth / 2f;"), "R2: SlotCenterX 不得改");
            // 设计分辨率坐标系（原生 1080×1920，Content 不缩放）
            Assert.IsTrue(src.Contains("public const float DesignWidth = 1080f;"), "R2: DesignWidth=1080 不得改");
            Assert.IsTrue(src.Contains("public const float DesignHeight = 1920f;"), "R2: DesignHeight=1920 不得改");
        }

        // ── R2b：GameWindow 换皮节点沿用 BlockLayout 既有坐标（不硬编码棋盘/槽位坐标）──────
        [Test]
        public void R2b_GameWindow_BoardNodesUseBlockLayoutCoords()
        {
            var src = ReadSource(GameWindowPath);

            // 棋盘外框中心仍由 BlockLayout 算（换皮只改贴图，不动位置/尺寸）
            Assert.IsTrue(src.Contains("float boardCx = BlockLayout.BoardOriginX + BlockLayout.BoardPixels / 2f;"),
                "R2b: 棋盘外框 cx 仍由 BlockLayout 既有常量算");
            Assert.IsTrue(src.Contains("float boardCy = BlockLayout.BoardOriginY + BlockLayout.BoardPixels / 2f;"),
                "R2b: 棋盘外框 cy 仍由 BlockLayout 既有常量算");
            // 格底仍用 CellCenterDesign（坐标映射不变）
            Assert.IsTrue(src.Contains("var center = BlockLayout.CellCenterDesign(c, r);"),
                "R2b: 格底位置仍由 CellCenterDesign 算");
            Assert.IsTrue(src.Contains("BlockLayout.BoardPixels + 16, BlockLayout.BoardPixels + 16"),
                "R2b: 棋盘外框尺寸沿用既有 BoardPixels+16");
        }

        // ── R3：数据层 BlockGameState 字段集未改（资源条/动作按钮未往数据层加状态）──────
        [Test]
        public void R3_BlockGameState_FieldSet_Unchanged()
        {
            var src = ReadSource(BlockGameStatePath);

            // Classic 数值字段集（换皮不得新增/删改）
            Assert.IsTrue(src.Contains("public int[][] SaveArr;"), "R3: SaveArr 字段不得改");
            Assert.IsTrue(src.Contains("public PendingPiece[] OperaArr"), "R3: OperaArr 字段不得改");
            Assert.IsTrue(src.Contains("public int Score;"), "R3: Score 字段不得改");
            Assert.IsTrue(src.Contains("public int HighScore;"), "R3: HighScore 字段不得改");
            Assert.IsTrue(src.Contains("public int Combo;"), "R3: Combo 字段不得改");

            // 反向：换皮不得为资源条/动作按钮加货币/资源/提示次数等字段（设计 27 D1/D5 → 占位/stub）
            Assert.IsFalse(src.Contains("public int Gold"), "R3: 不得为资源条加 Gold 字段（占位 D1）");
            Assert.IsFalse(src.Contains("public int Gem"), "R3: 不得为资源条加 Gem 字段（占位 D1）");
            Assert.IsFalse(src.Contains("public int Diamond"), "R3: 不得为资源条加 Diamond 字段（占位 D1）");
            Assert.IsFalse(src.Contains("public int HintCount"), "R3: 不得为提示按钮加 HintCount 字段（stub D5）");
        }

        // ── R3b：GameWindow 资源条 / 动作按钮接入断言(设计 42 接续后翻转:资源条改绑 PlayerAttrService 三属性,动作按钮仍 stub)──
        // 设计 42 已把资源条由「占位 + 第 1 条接 HighScore」改为绑 Coin/Diamond/Stamina 三属性,
        // 订阅 OnAttrChanged 实时刷新 + OnDestroy 解绑;故旧版「num = i==0 ? _initialHigh : "0"」断言已与设计意图对立(必删)。
        // 翻转后的断言对位设计 42 §七 SV1/SV3 + §7.3 W1/W2:订阅 / 解绑 / 三属性绑定字段命中。
        // 加号 Log 待建 + 动作按钮 stub 仍是去变现红线 + Tier 2+ 业务范围,保留不动。
        [Test]
        public void R3b_GameWindow_ResourceBarAndActions_ArePlaceholderStub()
        {
            var src = ReadSource(GameWindowPath);

            // 资源条加号 → Log 待建(去变现红线,不接购买;设计 42 §一 不守加号变购买入口)
            Assert.IsTrue(src.Contains("资源条加号：待建"), "R3b: 资源条加号点击应 Log 待建(去变现红线 D1)");
            // 动作按钮 → Log 待建(不实现机制;Tier 2+ 业务玩法阶段,设计 27 §六)
            Assert.IsTrue(src.Contains("动作按钮"), "R3b: 动作按钮点击应 Log 待建(stub D5)");
            Assert.IsTrue(src.Contains("待建（新玩法机制"), "R3b: 动作按钮应标注新玩法机制待建(不实现)");

            // 设计 42 接续:资源条数字绑 PlayerAttrService 三属性,IsReady=false 显「—」,订阅 OnAttrChanged + OnDestroy 解绑
            Assert.IsTrue(src.Contains("Attr.OnAttrChanged += OnAttrChangedDispatch"),
                "R3b/设计 42 W1: 资源条须订阅 PlayerAttrService.OnAttrChanged(注册命中)");
            Assert.IsTrue(src.Contains("Attr.OnAttrChanged -= OnAttrChangedDispatch"),
                "R3b/设计 42 W2: 资源条须在 OnDestroy 解绑 OnAttrChanged(防 GC root 泄漏)");
            Assert.IsTrue(src.Contains("AttrType.Coin") && src.Contains("AttrType.Diamond") && src.Contains("AttrType.Stamina"),
                "R3b/设计 42 §三: 资源条须绑 Coin/Diamond/Stamina 三属性");
            Assert.IsTrue(src.Contains("\"—\""),
                "R3b/设计 42 §三 W4: IsReady=false 时应显「—」加载中态(非 0 不误导玩家)");
        }

        // ── R4：退出回调目标 + 齿轮叠层未改 ──────────────────────────────────────────
        [Test]
        public void R4_GameWindow_ExitCallback_TargetsMainMenu()
        {
            var src = ReadSource(GameWindowPath);

            // 退出入口回调一字不改：CloseUI<GameWindow> + ShowUIAsync<MainMenuWindow>
            Assert.IsTrue(src.Contains("GameModule.UI.CloseUI<GameWindow>();"), "R4: 退出回调 CloseUI<GameWindow> 必须保留");
            Assert.IsTrue(src.Contains("GameModule.UI.ShowUIAsync<MainMenuWindow>();"), "R4: 退出回调 ShowUIAsync<MainMenuWindow> 必须保留");
        }

        [Test]
        public void R4b_GameWindow_GearOpensSettings_AsOverlay_NotClosingSelf()
        {
            var src = ReadSource(GameWindowPath);

            // 齿轮 → 叠层弹设置窗（不关本窗、不丢局）：只 ShowUIAsync<SettingsWindow>，附近无 CloseUI<GameWindow>
            Assert.IsTrue(src.Contains("GameModule.UI.ShowUIAsync<SettingsWindow>()"),
                "R4b: 齿轮应叠层弹 SettingsWindow");
            int gearIdx = src.IndexOf("ShowUIAsync<SettingsWindow>");
            Assert.Greater(gearIdx, 0, "R4b: 应找到齿轮接线");
            // 齿轮接线行本身不得连带 CloseUI<GameWindow>（同一 lambda 不关本窗）
            int lineStart = src.LastIndexOf('\n', gearIdx);
            int lineEnd = src.IndexOf('\n', gearIdx);
            string gearLine = src.Substring(lineStart + 1, lineEnd - lineStart - 1);
            Assert.IsFalse(gearLine.Contains("CloseUI<GameWindow>"),
                "R4b: 齿轮接线不得关本窗（叠层不丢局）");
        }

        // ── R4c：GameOver 调用点传参未改（结算窗范式 R1 调用点）───────────────────────
        [Test]
        public void R4c_GameWindow_GameOverCallSite_Unchanged()
        {
            var src = ReadSource(GameWindowPath);
            Assert.IsTrue(src.Contains("ShowUIAsync<GameOverWindow>(previousHigh)"),
                "R4c: GameOver 调用点应传 previousHigh（Classic 路径不得改传参）");
        }

        // ── R5：融合主体 MergeOrderWindow 承载 game_main 紫金皮（静态视觉烤进 prefab）+ 坐标常量与经济逻辑零回归 ────
        // 静态视觉壳（棋盘框/待选区/HUD 条/订单宝箱卡/货币 icon）的 sprite + tint 已烤进 prefab 绑定节点的 m_Sprite/m_Color，
        // prefab 为静态视觉唯一来源、编辑器内所见即所得；代码不再运行时 SetSprite 这些静态节点（避免覆盖美术在 prefab 的调整）。
        // 仍随状态换图的动态内容（棋盘格/候选块/元素图标）保持运行时 SetSprite。坐标/经济零回归断言保留。
        [Test]
        public void R5_MergeOrderWindow_CarriesReskin_CoordsUnchanged()
        {
            var src = ReadSource(MergeOrderWindowPath);

            // MergeOrderWindow 仍存在且 location 属性未改
            Assert.IsTrue(src.Contains("[Window(UILayer.UI, location: \"MergeOrderWindow\", fullScreen: true)]"),
                "R5: MergeOrderWindow location 属性不得改（改则运行时找不到窗口）");

            // 静态视觉烤进 prefab：代码不再对静态壳节点运行时 SetSprite（这些是 prefab 烤的，再 SetSprite 会覆盖美术调整）。
            Assert.IsFalse(src.Contains("SetSprite(\"方块背景\")"),
                "R5: 棋盘外框 sprite 已烤进 prefab（m_img_BoardOuter），代码不应再运行时 SetSprite(\"方块背景\")");
            Assert.IsFalse(src.Contains("SetSprite(\"待选区背景\")"),
                "R5: 待选区 sprite 已烤进 prefab（m_img_SlotBg），代码不应再运行时 SetSprite(\"待选区背景\")");
            Assert.IsFalse(src.Contains("SetSprite(\"消除道具\")"),
                "R5: 消除道具图标 sprite 已烤进 prefab（m_img_ClearToolIcon），代码不应再运行时 SetSprite(\"消除道具\")");
            // 静态壳复用 prefab 绑定节点（避免与动态创建同名节点双份）。
            Assert.IsTrue(src.Contains("transform.Find(\"Content\")"),
                "R5: _content 应复用 prefab 既有 Content 节点（不再 UGuiFactory 动态创建）");
            Assert.IsTrue(src.Contains("_boardLayer = (RectTransform)_content.Find(\"BoardLayer\")"),
                "R5: 空层节点应从 prefab Content 下 Find 获取");
            // ClearTool gate 动态染色仍运行时执行（_clearToolBtnBg 指向图标 Image，按体力门控染色）。
            Assert.IsTrue(src.Contains("_clearToolBtnBg = m_img_ClearToolIcon"),
                "R5: 消除道具 gate 染色目标仍指向图标 Image（动态门控染色保留）");
            // 动态换皮（棋盘格/候选块/元素图标）仍走运行时 SetSprite。
            Assert.IsTrue(src.Contains("img.SetSprite(mono ? monoLoc : BlockSkinCatalog.ColoredSpriteName(colorIdx))"),
                "R5: 棋盘格动态换皮必须保留运行时 SetSprite（随皮肤态换图）");

            // 零回归红线：棋盘坐标仍由 BlockLayout 既有常量决定（prefab 已按同口径摆放，代码不硬编码、不动落子对位）。
            Assert.IsFalse(src.Contains("BlockLayout.BoardOriginX = ") || src.Contains("BlockLayout.BoardOriginY = "),
                "R5: 不得改写 BlockLayout 棋盘原点常量（动了落子对位偏）");
            // 经济逻辑零回归：落子结算链关键行仍在。
            Assert.IsTrue(src.Contains("_state.PlacePiece(slotIdx, _board, col, row)"),
                "R5: 落子 PlacePiece 调用必须保留（换皮不碰经济逻辑）");
            Assert.IsTrue(src.Contains("ClearSettlement.Settle"),
                "R5: 结算 ClearSettlement.Settle 必须保留（换皮不碰经济逻辑）");
        }

        // ── C2：换皮红线核对（五条编码红线）──────────────────────────────────────────
        [Test]
        public void C2_GameWindow_CodeRedlines()
        {
            var src = ReadSource(GameWindowPath);

            // 资源释放：SetSubSprite 自管引用计数，无裸 LoadAssetAsync<Sprite>
            Assert.IsFalse(src.Contains("LoadAssetAsync<Sprite>"),
                "C2: GameWindow 不得有裸 LoadAssetAsync<Sprite>（应走 SetSubSprite，自管引用计数）");
            // 事件解耦：按钮回调用 onClick.AddListener
            Assert.IsTrue(src.Contains("onClick.AddListener"), "C2: 按钮回调应用 onClick.AddListener（UI 内部事件）");
            // 模块访问：GameModule.UI
            Assert.IsTrue(src.Contains("GameModule.UI"), "C2: 模块访问应用 GameModule.UI");
        }

        // ── V1 代理：换皮代码已写入（Sheet_tarot_mode + 关键子图名）───────────────────
        [Test]
        public void V1_GameWindow_SetSubSprite_PresentForReskin()
        {
            var src = ReadSource(GameWindowPath);

            // 精灵表常量
            Assert.IsTrue(src.Contains("Sheet_tarot_mode"), "V1: GameWindow 应引用 Sheet_tarot_mode 精灵表");
            // 关键子图：背景/外框/资源条底/齿轮/动作按钮底
            Assert.IsTrue(src.Contains("\"chessboard\""), "V1: 背景应贴 chessboard 子图");
            Assert.IsTrue(src.Contains("\"chess\""), "V1: 棋盘外框应贴 chess 子图");
            Assert.IsTrue(src.Contains("\"resourcebar2\""), "V1: 资源条底应贴 resourcebar2 子图");
            Assert.IsTrue(src.Contains("\"icon_setting\""), "V1: 齿轮应贴 icon_setting 子图");
            Assert.IsTrue(src.Contains("\"Rectangle\""), "V1: 动作按钮底应贴 Rectangle 子图");
            Assert.IsTrue(src.Contains("SetSubSprite(Atlas, "), "V1: 应用 SetSubSprite 链取子图");
        }

        // ── 类属性未改：location 不得改（改则运行时找不到窗口）──────────────────────────
        [Test]
        public void NoChange_GameWindow_ClassAttributePresent()
        {
            var src = ReadSource(GameWindowPath);
            Assert.IsTrue(src.Contains("[Window(UILayer.UI, location: \"GameWindow\", fullScreen: true)]"),
                "GameWindow location 属性不得改（改则运行时找不到窗口）");
        }
    }
}
