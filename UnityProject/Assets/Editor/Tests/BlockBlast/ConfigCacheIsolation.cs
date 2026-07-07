using System.Collections.Generic;
using GameLogic.Config;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

// 本程序集每个测试用例前后统一归位所有配置管理器静态缓存,消除跨测试静态污染导致的顺序相关 flaky。
[assembly: GameLogic.BlockBlast.Tests.ResetConfigCaches]

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 静态污染跨测试隔离器（assembly 级，<see cref="ActionTargets.Test"/>）。
    /// 在本程序集每个测试用例前后重置全部 <c>*ConfigMgr</c> 静态缓存 + <c>BlockGameState</c> 玩法态钩子，
    /// 从执行顺序上摘除「前序状态泄漏给后一个测试」这一类 flaky。
    /// </summary>
    /// <remarks>
    /// EditMode 域内的静态字段跨测试留存，两类需归位：
    ///
    /// ① 配置管理器缓存：某测试经 <c>InitForTest</c> 注入、或触发真实 <c>ConfigSystem</c> 加载后，
    /// 后续未自行钉住配置的测试会读到污染值。典型受害者是依赖 EditMode 回退默认的纯逻辑用例
    /// （如 MergeOrderConfig 经 GlobalConfigMgr 取值的订单 / 体力用例），执行顺序不同即时绿时红。
    /// GlobalConfigMgr 单独处理:它被生产配置 <c>MergeOrderConfig</c> 隐式读取,且回退路径会问 ConfigSystem
    /// （交互式 Editor 跑过 Play 后 YooAsset 包可能已加载 → 读到真实表值而非默认）。故对它 InitForTest(空表)
    /// 钉成「一律回退默认」,从根上免疫 ConfigSystem 是否已加载;其余管理器仅经各自测试显式 InitForTest 访问、
    /// 不被生产代码隐式读取,ResetForTest 清缓存即可。
    ///
    /// ② BlockGameState 玩法态钩子（<c>OnMergeStateReady</c>/<c>OnMergeStateClosed</c>）：静态委托，由 GameApp
    /// 在 Play 模式装配（钩子体内注入 <c>ServerDeal</c> + 把活态切服务端权威订单）。交互式 Editor 跑过 Play 后
    /// 它们跨域存活，污染同域 EditMode 用例：纯逻辑单测依赖本地权威基线，钩子非空会使 <c>ResetForMergeOrder</c>
    /// 末尾重新注入 ServerDeal（翻 RefillPieces 到服务端发牌分支 → 候选空 → NRE）并置 ServerAuthoritativeOrders
    /// （翻 Deliver 到服务端分支 → 本地不发交付体力）。ServerDeal 唯一注入点即这两个钩子体内，清零钩子即从根免疫。
    ///
    /// 前后各重置一次:BeforeTest 护住当前测试不受前序污染,AfterTest 顺手清掉本测试自身注入。
    /// </remarks>
    public sealed class ResetConfigCachesAttribute : System.Attribute, ITestAction
    {
        public ActionTargets Targets => ActionTargets.Test;

        public void BeforeTest(ITest test) => Reset();

        public void AfterTest(ITest test) => Reset();

        private static void Reset()
        {
            // GlobalConfigMgr:钉空表 = 强制回退默认,免疫已加载的 ConfigSystem(被 MergeOrderConfig 隐式读取)。
            GlobalConfigMgr.InitForTest(new Dictionary<int, string>());
            ItemConfigMgr.ResetForTest();
            AvatarConfigMgr.ResetForTest();
            MailConfigMgr.ResetForTest();
            RankConfigMgr.ResetForTest();
            NumericConfigMgr.ResetForTest();
            AudioConfigMgr.ResetForTest();

            // BlockGameState 玩法态钩子:清零 Play 模式装配的静态委托,免疫「Editor 跑过 Play 后」泄漏(见 remarks ②)。
            GameLogic.BlockBlast.BlockGameState.OnMergeStateReady = null;
            GameLogic.BlockBlast.BlockGameState.OnMergeStateClosed = null;
        }
    }
}
