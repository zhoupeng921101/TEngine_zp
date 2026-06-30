using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using GameLogic.BlockBlast;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 「只测生成核心」跨运行时确定性 harness 的客户端(Mono/IL2CPP)侧驱动。
    ///
    /// 调用与 .NET 服务端实验项目同一份共享 harness <see cref="GenCoreDeterminismHarness"/>,
    /// 聚焦 Portable PRNG(生产路径),逐 seed 跑 <see cref="GenCoreDeterminismHarness.Seeds"/>:
    ///   - 同运行时两遍逐字符一致自检(每 seed 一例)
    ///   - 每 seed 落一份客户端 Portable trace 到 Fixtures(文件名带 seed),供与服务端 .NET trace 逐位 diff
    ///
    /// System.Random 模式(c3 跨运行时发散)已在前轮测完,此处保留一条同运行时可复现自检,不再多 seed。
    /// 玩家策略无 RNG,两端各自独立跑出同一对局,无需输入回放。
    /// </summary>
    [TestFixture]
    public class GenCoreDeterminismHarnessTests
    {
        private InMemoryPersistenceProvider _provider;

        [SetUp]
        public void SetUp()
        {
            // harness 自建逐局调度器实例(各持内存持久化),此处保留默认 provider 设置避免污染本地存储。
            _provider = new InMemoryPersistenceProvider();
            Persistence.Provider = _provider;
        }

        /// <summary>NUnit 用例参数源:多 seed 扩测的代表性种子组(单一事实源在共享 harness)。</summary>
        private static IEnumerable<int> Seeds => GenCoreDeterminismHarness.Seeds;

        [Test]
        public void GenCoreTrace_SameRuntime_IsReproducible_Portable([ValueSource(nameof(Seeds))] int seed)
        {
            string a = GenCoreDeterminismHarness.RunSeed(seed);
            string b = GenCoreDeterminismHarness.RunSeed(seed);
            if (!string.Equals(a, b, StringComparison.Ordinal))
            {
                int line = FirstDivergenceLine(a, b);
                Assert.Fail($"同运行时 Portable seed={seed} 两遍 trace 不一致,首个发散在第 {line} 行。");
            }
            Assert.Pass();
        }

        [Test]
        public void GenCoreTrace_SameRuntime_IsReproducible_SystemRandom()
        {
            string a = GenCoreDeterminismHarness.RunScriptedGame(
                GenCoreDeterminismHarness.Seeds[0], GenCoreDeterminismHarness.DefaultSteps,
                GenCoreDeterminismHarness.RngKind.SystemRandom);
            string b = GenCoreDeterminismHarness.RunScriptedGame(
                GenCoreDeterminismHarness.Seeds[0], GenCoreDeterminismHarness.DefaultSteps,
                GenCoreDeterminismHarness.RngKind.SystemRandom);
            if (!string.Equals(a, b, StringComparison.Ordinal))
            {
                int line = FirstDivergenceLine(a, b);
                Assert.Fail($"同运行时 System.Random 两遍 trace 不一致,首个发散在第 {line} 行。");
            }
            Assert.Pass();
        }

        [Test]
        public void GenCoreTrace_PortableTracesWrittenToFixtures()
        {
            foreach (int seed in GenCoreDeterminismHarness.Seeds)
            {
                string trace = GenCoreDeterminismHarness.RunSeed(seed);
                WriteTrace(trace, $"gencore_trace_client_portable_seed{seed}.txt");
            }
        }

        private void WriteTrace(string trace, string fileName)
        {
            string fixturesDir = Path.Combine(
                UnityEngine.Application.dataPath, "Editor", "Tests", "BlockBlast", "Fixtures");
            Directory.CreateDirectory(fixturesDir);
            string path = Path.Combine(fixturesDir, fileName);
            File.WriteAllText(path, trace, new UTF8Encoding(false));
            Assert.IsTrue(File.Exists(path), $"trace 应写到 {path}");
            Assert.Greater(new FileInfo(path).Length, 0, "trace 不应为空");
        }

        private static int FirstDivergenceLine(string a, string b)
        {
            var la = a.Split('\n');
            var lb = b.Split('\n');
            int n = Math.Min(la.Length, lb.Length);
            for (int i = 0; i < n; i++)
                if (!string.Equals(la[i], lb[i], StringComparison.Ordinal)) return i + 1;
            return n + 1;
        }
    }
}
