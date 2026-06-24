using System;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using GameLogic.Activity;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 活动累加 · 客户端段测试(设计 48 PV6 / PV9 / PV10 / PV11 / PV12 / PV13 EditMode 部分)。
    /// 注桩 <see cref="IActivityIncrementSource"/> 覆盖各分支:Success / Success+TargetReached /
    /// InvalidRequest / NotCumulative / ServiceUnavailable / NetworkDown(异常 catch)/ 入参透传。
    /// 真往返 E1-E7 由 boss 后续 test-only 增量起服联调(本机 MongoDB 不可达判 BLOCKED)。
    /// </summary>
    /// <remarks>
    /// 异步同步驱动:桩只 <c>await UniTask.CompletedTask</c>,<c>UniTask&lt;T&gt;.GetAwaiter().GetResult()</c> 即同步完成
    /// (同 mail / redeem / rank / attrledger 客户端段做法)。
    /// 测试<b>不</b>引 Fantasy 协议类型(test asmdef 无 Fantasy.Unity 引用,memory「客户端段单测别给 test asmdef 加 Fantasy.Unity 引用」)。
    /// 防重 / 等价计数 / Tarot 共计三类 PV(PV6 / PV7) 锚在「同一函数 hook 调用次数 = 1」,
    /// 用 <c>FakeActivityIncrementSource.IncrementCalls</c> 计数 + 注入到 <see cref="GameContext.Activity"/> 直驱
    /// <see cref="RemoteActivityService.IncrementAndLogAsync"/>(单测不驱动 GameWindow OnCreate,沿 26 settlement-window 范式
    /// 「读源文件文本核对关键行」,UI 层薄壳留 Play 手验)。
    /// </remarks>
    [TestFixture]
    public class ActivityClientTests
    {
        // ── 桩源:可编程响应 + 抛异常 ─────────────────────────────

        /// <summary>桩:返预置 <see cref="ActivityIncrementResult"/> 或抛异常;记录 Increment 调用入参 / 次数。</summary>
        private sealed class FakeActivityIncrementSource : IActivityIncrementSource
        {
            public ActivityIncrementResult ResultToReturn =
                new ActivityIncrementResult(ActivityIncrementCode.Success, 1L, false);
            public Exception ToThrow;
            public int DelayMs;

            public int IncrementCalls;
            public int LastActivityId;
            public int LastDelta;

            public async UniTask<ActivityIncrementResult> IncrementAsync(int activityId, int delta)
            {
                IncrementCalls++;
                LastActivityId = activityId;
                LastDelta = delta;
                if (DelayMs > 0) await UniTask.Delay(DelayMs);
                else await UniTask.CompletedTask;
                if (ToThrow != null) throw ToThrow;
                return ResultToReturn;
            }
        }

        // ════════════ PV9 service 编排:Success 透传 + 字段对位 ════════════

        [Test]
        public void Service_Increment_Success_PassesThrough()
        {
            var stub = new FakeActivityIncrementSource
            {
                ResultToReturn = new ActivityIncrementResult(ActivityIncrementCode.Success, 42L, false),
            };
            var svc = new RemoteActivityService(stub);

            var result = svc.IncrementAsync(5, 1).GetAwaiter().GetResult();

            Assert.AreEqual(ActivityIncrementCode.Success, result.Code);
            Assert.AreEqual(42L, result.CurrentCounter);
            Assert.IsFalse(result.TargetReached);
        }

        [Test]
        public void Service_Increment_PassesActivityIdAndDelta()
        {
            var stub = new FakeActivityIncrementSource();
            var svc = new RemoteActivityService(stub);

            svc.IncrementAsync(5, 1).GetAwaiter().GetResult();

            Assert.AreEqual(5, stub.LastActivityId, "activityId 原样透传");
            Assert.AreEqual(1, stub.LastDelta, "delta 原样透传");
            Assert.AreEqual(1, stub.IncrementCalls);
        }

        [Test]
        public void Service_Increment_TargetReached_PassesThrough()
        {
            var stub = new FakeActivityIncrementSource
            {
                ResultToReturn = new ActivityIncrementResult(ActivityIncrementCode.Success, 100L, true),
            };
            var svc = new RemoteActivityService(stub);

            var result = svc.IncrementAsync(5, 1).GetAwaiter().GetResult();

            Assert.IsTrue(result.TargetReached, "达标位透传");
            Assert.AreEqual(100L, result.CurrentCounter);
        }

        // ════════════ PV9 错误码归一各档(EditMode 单测) ════════════

        [Test]
        public void Service_Increment_InvalidRequest_PassesThrough()
        {
            var stub = new FakeActivityIncrementSource
            {
                ResultToReturn = ActivityIncrementResult.InvalidRequest,
            };
            var svc = new RemoteActivityService(stub);
            var result = svc.IncrementAsync(5, 1).GetAwaiter().GetResult();
            Assert.AreEqual(ActivityIncrementCode.InvalidRequest, result.Code);
            Assert.AreEqual(0L, result.CurrentCounter, "失败分支 counter=0");
            Assert.IsFalse(result.TargetReached);
        }

        [Test]
        public void Service_Increment_NotCumulative_PassesThrough()
        {
            var stub = new FakeActivityIncrementSource
            {
                ResultToReturn = new ActivityIncrementResult(ActivityIncrementCode.NotCumulative),
            };
            var svc = new RemoteActivityService(stub);
            var result = svc.IncrementAsync(5, 1).GetAwaiter().GetResult();
            Assert.AreEqual(ActivityIncrementCode.NotCumulative, result.Code);
        }

        [Test]
        public void Service_Increment_ServiceUnavailable_PassesThrough()
        {
            var stub = new FakeActivityIncrementSource
            {
                ResultToReturn = ActivityIncrementResult.ServiceUnavailable,
            };
            var svc = new RemoteActivityService(stub);
            var result = svc.IncrementAsync(5, 1).GetAwaiter().GetResult();
            Assert.AreEqual(ActivityIncrementCode.ServiceUnavailable, result.Code);
        }

        [Test]
        public void Service_Increment_NetworkDown_PassesThrough()
        {
            var stub = new FakeActivityIncrementSource
            {
                ResultToReturn = ActivityIncrementResult.NetworkDown,
            };
            var svc = new RemoteActivityService(stub);
            var result = svc.IncrementAsync(5, 1).GetAwaiter().GetResult();
            Assert.AreEqual(ActivityIncrementCode.NetworkDown, result.Code);
        }

        // ════════════ PV12 source 抛异常的传播(生产实现 RemoteActivityIncrementSource 自 catch,
        //                              此处验「接缝层若抛,service 透传」契约 — 接缝契约文档已注明「不抛」,
        //                              桩抛是反契约,service 不吞,业务出口 .Forget() 兜底) ════════════

        [Test]
        public void Service_Increment_SourceThrows_PropagatesException()
        {
            var stub = new FakeActivityIncrementSource
            {
                ToThrow = new InvalidOperationException("source contract broken"),
            };
            var svc = new RemoteActivityService(stub);

            // service 不 catch(接缝契约「不抛」);桩反契约抛出,service 透传 — fire-and-forget
            // 业务出口由 .Forget() 在 UniTask 框架层捕获,不影响 GameOver 流程。
            Assert.Throws<InvalidOperationException>(
                () => svc.IncrementAsync(5, 1).GetAwaiter().GetResult(),
                "接缝契约外的抛由调用方 .Forget() 兜底,service 不吞");
        }

        // ════════════ PV9 协议码 → 客户端枚举映射(纯函数,非 FANTASY_UNITY-gated) ════════════

        [Test]
        public void MapResultCode_AllKnownCodes()
        {
            Assert.AreEqual(ActivityIncrementCode.Success,            RemoteActivityIncrementSource.MapResultCode(0));
            Assert.AreEqual(ActivityIncrementCode.InvalidRequest,     RemoteActivityIncrementSource.MapResultCode(1));
            Assert.AreEqual(ActivityIncrementCode.NotCumulative,      RemoteActivityIncrementSource.MapResultCode(2));
            Assert.AreEqual(ActivityIncrementCode.ServiceUnavailable, RemoteActivityIncrementSource.MapResultCode(3));
        }

        [Test]
        public void MapResultCode_UnknownCode_FallsBackToServiceUnavailable()
        {
            // 未知码兜底 ServiceUnavailable(不崩,沿 RemoteAttrLedgerSource 同范式)
            Assert.AreEqual(ActivityIncrementCode.ServiceUnavailable, RemoteActivityIncrementSource.MapResultCode(99));
            Assert.AreEqual(ActivityIncrementCode.ServiceUnavailable, RemoteActivityIncrementSource.MapResultCode(-1));
            Assert.AreEqual(ActivityIncrementCode.ServiceUnavailable, RemoteActivityIncrementSource.MapResultCode(int.MaxValue));
        }

        // ════════════ PV10 fire-and-forget 不阻塞:延迟桩 + .Forget() 立即返 ════════════

        [Test]
        public void IncrementAndLog_FireAndForget_DoesNotBlock()
        {
            var stub = new FakeActivityIncrementSource
            {
                DelayMs = 5000, // 模拟 5s 慢网,fire-and-forget 不等
                ResultToReturn = new ActivityIncrementResult(ActivityIncrementCode.Success, 1L, false),
            };
            var svc = new RemoteActivityService(stub);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            // fire-and-forget:.Forget() 不 await,立即返,调用线程不被 5s 延迟阻塞
            svc.IncrementAndLogAsync(5, 1).Forget();
            sw.Stop();

            // 桩已被调一次(同步入口立即进入)
            Assert.AreEqual(1, stub.IncrementCalls, "fire-and-forget 入口已触发桩调用");
            // 关键:调用线程未被 5s 阻塞 — 经验数 100ms 容忍是「机器抖动」,正常 <10ms
            Assert.Less(sw.ElapsedMilliseconds, 500,
                $"fire-and-forget 不阻塞调用方;实际 {sw.ElapsedMilliseconds}ms vs 桩延迟 5000ms");
        }

        // ════════════ PV6 防重等价:对同一桩 service 连续调 N 次 = N 次推送
        //                          (业务防重在 GameWindow._gameOverTriggered / MergeOrderWindow._finished 层,
        //                           service 层不做去重 — 沿设计 48 O8 决策。本测试验「service 不自带防重」,
        //                           防重依赖业务标记由 GameWindowTriggerHookSourceTests 静态文本核) ════════════

        [Test]
        public void Service_Increment_NoLocalDedup()
        {
            var stub = new FakeActivityIncrementSource();
            var svc = new RemoteActivityService(stub);

            svc.IncrementAsync(5, 1).GetAwaiter().GetResult();
            svc.IncrementAsync(5, 1).GetAwaiter().GetResult();
            svc.IncrementAsync(5, 1).GetAwaiter().GetResult();

            Assert.AreEqual(3, stub.IncrementCalls,
                "service 层不去重(沿 O8 防重靠业务层 _gameOverTriggered/_finished);3 次调用 = 3 次推送");
        }

        // ════════════ PV13 反证 service 不持本地状态(无字段、无缓存) ════════════

        [Test]
        public void Service_HasNoLocalStateFields()
        {
            // 反射核:RemoteActivityService 只有 _source 一个 readonly 字段(无 PendingIncrements / 无 LastResult / 无 PlayerPrefs)。
            var fields = typeof(RemoteActivityService).GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            Assert.AreEqual(1, fields.Length,
                "service 不持本地状态(沿 47 §四 服务端独占审计;48 §3.3 不缓存)");
            Assert.AreEqual("_source", fields[0].Name);
        }

        // ════════════ PV15 ⑤ 反证 source 接缝无本地状态字段 ════════════

        [Test]
        public void RemoteSource_HasNoLocalStateFields()
        {
            // 反射核:RemoteActivityIncrementSource 无任何实例字段(纯无状态生产实现)。
            var fields = typeof(RemoteActivityIncrementSource).GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            Assert.AreEqual(0, fields.Length, "生产 source 无本地状态(无 cache / pending queue)");
        }

        // ════════════ POCO 工厂方法 ════════════

        [Test]
        public void Result_ServiceUnavailable_FactoryHasCorrectCode()
        {
            var r = ActivityIncrementResult.ServiceUnavailable;
            Assert.AreEqual(ActivityIncrementCode.ServiceUnavailable, r.Code);
            Assert.AreEqual(0L, r.CurrentCounter);
            Assert.IsFalse(r.TargetReached);
        }

        [Test]
        public void Result_NetworkDown_FactoryHasCorrectCode()
        {
            var r = ActivityIncrementResult.NetworkDown;
            Assert.AreEqual(ActivityIncrementCode.NetworkDown, r.Code);
        }

        [Test]
        public void Result_InvalidRequest_FactoryHasCorrectCode()
        {
            var r = ActivityIncrementResult.InvalidRequest;
            Assert.AreEqual(ActivityIncrementCode.InvalidRequest, r.Code);
        }

        // ════════════ ActivityIds 常量 ════════════

        [Test]
        public void ActivityIds_AccumulatePlayCount_IsFive()
        {
            Assert.AreEqual(5, ActivityIds.AccumulatePlayCount,
                "本子单首套累计游戏 N 局 activity_id=5(对位 activity.xlsx,见 47 §3.4)");
        }

        // ════════════ GameContext 注入入口(沿 InitAttrLedgerWith 范式) ════════════

        [Test]
        public void GameContext_InitActivityWith_ReplacesService()
        {
            var stub = new FakeActivityIncrementSource();
            GameContext.Instance.InitActivityWith(stub);

            // 注入后 GameContext.Activity 非 null,可直驱
            Assert.IsNotNull(GameContext.Instance.Activity);
            GameContext.Instance.Activity.IncrementAsync(5, 1).GetAwaiter().GetResult();
            Assert.AreEqual(1, stub.IncrementCalls, "GameContext.Activity 已挂注桩源");
        }

        // ════════════ PV13 + PV14 + PV15 ⑧ 源码文本核:hook 落点 + 防重前置 + 不动玩法核心
        //              沿 26 settlement-window memory「整窗 hook 走读源文件 grep 关键行」范式 ════════════

        // 无尽模型（设计 49）善后:MergeOrderWindow 删 TriggerWin / TriggerGameOver(无通关 / 无 GameOver),
        // 「累计游戏 N 局」活动原挂这两个终点 hook 上,无「局」后失效——本窗不再触发该活动(善后 follow-up 交 boss/plan 重定)。
        // 原 Source_MergeOrderWindow_TriggerGameOver / TriggerWin _HasActivityHookAfterDedup 翻转为「不再存在」断言。
        [Test]
        public void Source_MergeOrderWindow_NoWinOrGameOverTrigger()
        {
            var src = System.IO.File.ReadAllText("Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MergeOrderWindow.cs");
            Assert.IsFalse(src.Contains("private void TriggerWin()"),
                "无尽模型:MergeOrderWindow 不再有 TriggerWin(无通关终点)");
            Assert.IsFalse(src.Contains("private void TriggerGameOver"),
                "无尽模型:MergeOrderWindow 不再有 TriggerGameOver(无软/硬 GameOver)");
        }

        [Test]
        public void Source_MergeOrderWindow_NoAccumulatePlayCountHook()
        {
            // 无尽模型善后:MergeOrderWindow 不再触发 AccumulatePlayCount 活动(原挂在 Win/GameOver 终点)。
            var src = System.IO.File.ReadAllText("Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MergeOrderWindow.cs");
            Assert.IsFalse(src.Contains("ActivityIds.AccumulatePlayCount"),
                "无尽模型:MergeOrderWindow 不再触发 AccumulatePlayCount(无「局」概念)");
        }

        [Test]
        public void Source_MergeOrderWindow_NoActivityHookAtAll()
        {
            // 无尽模型善后:MergeOrderWindow 删 Win/GameOver 终点后,活动 hook 字面 0 次(原 2 次 = TriggerGameOver + TriggerWin)。
            var src = System.IO.File.ReadAllText("Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MergeOrderWindow.cs");
            int count = 0; int pos = 0;
            while ((pos = src.IndexOf("Activity?.IncrementAndLogAsync(", pos, StringComparison.Ordinal)) >= 0)
            {
                count++;
                pos += 1;
            }
            Assert.AreEqual(0, count, "无尽模型:MergeOrderWindow.cs 不再有任何活动 hook(Win/GameOver 终点已删)");
        }

        // ════════════ PV14 反证 业务层不直引 Fantasy.* 命名空间 ════════════

        [Test]
        public void Source_BusinessLayer_DoesNotImportFantasy()
        {
            // RemoteActivityService / IActivityIncrementSource / MergeOrderWindow.cs 不引 Fantasy.*
            // (RemoteActivityIncrementSource 经 #if FANTASY_UNITY 引,沿 38 §五 IRpcGateway 范式)
            string[] businessFiles =
            {
                "Assets/GameScripts/HotFix/GameLogic/Module/Activity/RemoteActivityService.cs",
                "Assets/GameScripts/HotFix/GameLogic/Module/Activity/IActivityIncrementSource.cs",
                "Assets/GameScripts/HotFix/GameLogic/Module/Activity/ActivityIncrementResult.cs",
                "Assets/GameScripts/HotFix/GameLogic/Module/Activity/ActivityIncrementCode.cs",
                "Assets/GameScripts/HotFix/GameLogic/Module/Activity/ActivityIds.cs",
                "Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MergeOrderWindow.cs",
            };
            foreach (var path in businessFiles)
            {
                var src = System.IO.File.ReadAllText(path);
                Assert.IsFalse(src.Contains("using Fantasy"),
                    $"{path} 业务层不应 using Fantasy*(沿 38 §五 IRpcGateway 范式;FANTASY_UNITY-gated 只在 RemoteActivityIncrementSource.cs)");
            }
        }
    }
}
