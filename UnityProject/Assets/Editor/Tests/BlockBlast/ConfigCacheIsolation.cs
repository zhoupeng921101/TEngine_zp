using System.Collections.Generic;
using GameLogic.Config;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

// 本程序集每个测试用例前后统一归位所有配置管理器静态缓存,消除跨测试静态污染导致的顺序相关 flaky。
[assembly: GameLogic.BlockBlast.Tests.ResetConfigCaches]

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 配置管理器静态缓存跨测试隔离器（assembly 级，<see cref="ActionTargets.Test"/>）。
    /// 在本程序集每个测试用例前后重置全部 <c>*ConfigMgr</c> 静态缓存，从执行顺序上摘除
    /// 「前一个测试注入 / 加载的配置泄漏给后一个测试」这一类 flaky。
    /// </summary>
    /// <remarks>
    /// 配置管理器缓存是静态字段，在 EditMode 域内跨测试留存：某测试经 <c>InitForTest</c> 注入、
    /// 或触发真实 <c>ConfigSystem</c> 加载后，后续未自行钉住配置的测试会读到污染值。典型受害者是
    /// 依赖 EditMode 回退默认的纯逻辑用例（如 MergeOrderConfig 经 GlobalConfigMgr 取值的订单 / 体力用例），
    /// 执行顺序不同即时绿时红。
    ///
    /// GlobalConfigMgr 单独处理:它被生产配置 <c>MergeOrderConfig</c> 隐式读取,且回退路径会问 ConfigSystem
    /// （交互式 Editor 跑过 Play 后 YooAsset 包可能已加载 → 读到真实表值而非默认）。故对它 InitForTest(空表)
    /// 钉成「一律回退默认」,从根上免疫 ConfigSystem 是否已加载;其余管理器仅经各自测试显式 InitForTest 访问、
    /// 不被生产代码隐式读取,ResetForTest 清缓存即可。
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
        }
    }
}
