using System.Collections.Generic;
using GameLogic.BlockBlast.Algorithms;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 客户端预测发牌器的权重配置(服务端权威发牌的客户端镜像)。
    ///
    /// 服务端 GameSession 的权威发牌器用一份固定 2-tier 配置初始化(非 Luban 表)。客户端要做到
    /// 「同 seed 预测与服务端逐位一致」,预测发牌器必须用<b>完全相同</b>的配置——否则 tier 选择 / 算法
    /// 抽取分叉,预测与权威态在第一批补牌即发散,对账每手触发。故这份配置是单一事实源,逐字段对齐服务端。
    /// </summary>
    /// <remarks>
    /// 与服务端 <c>GenCoreDeterminismHarness.DefaultWeightConfig()</c> 同口径(两 tier,id/odds/hs/factor 逐字段相同)。
    /// 服务端那份是权威基线;本份是客户端为预测复刻的镜像。<b>改服务端权威配置必同步本份</b>,否则预测失准。
    /// 客户端生产历史上用的 Luban <c>TbWeightCfg</c> 不再驱动发牌(发牌已上服务端权威),只可能用于其它展示;
    /// 预测发牌一律用本配置,不读 Luban 表,以杜绝两份配置漂移导致的对账风暴。
    /// </remarks>
    public static class BlockGenWeightConfig
    {
        /// <summary>服务端权威发牌配置的客户端镜像(2 tier)。每次调用返回新列表,避免被调用方改动共享实例。</summary>
        public static List<WeightConfigEntry> ServerMirror()
        {
            return new List<WeightConfigEntry>
            {
                new WeightConfigEntry
                {
                    Id = 1,
                    FillBlankOdds = 80, RandomOdds = 20,
                    EntropyOdds = 0, EasyOdds = 0, HardOdds = 0,
                    IntuitionOdds = 0, Clearboard = 0, Allunite = 0,
                    HighScoreMin = 0, HighScoreMax = -1,
                    FactorLow = -100, FactorHigh = 0,
                },
                new WeightConfigEntry
                {
                    Id = 2,
                    FillBlankOdds = 0, RandomOdds = 10,
                    EntropyOdds = 10, EasyOdds = 10, HardOdds = 60,
                    IntuitionOdds = 10, Clearboard = 0, Allunite = 0,
                    HighScoreMin = 0, HighScoreMax = -1,
                    FactorLow = 0, FactorHigh = 200,
                },
            };
        }
    }
}
