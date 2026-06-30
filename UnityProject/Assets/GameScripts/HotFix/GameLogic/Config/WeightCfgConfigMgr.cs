using System.Collections.Generic;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Algorithms;

namespace GameLogic.Config
{
    /// <summary>
    /// Block Blast 动态权重表配置管理器。
    /// 桥接 Luban 生成的 GameConfig.block.TbWeightCfg → 数据层 WeightConfigEntry POCO，
    /// 让 DynamicWeightDiff 不直接依赖 Luban 类型。
    /// </summary>
    public static class WeightCfgConfigMgr
    {
        /// <summary>
        /// 从 Luban 表读出全部 tier 并转为 WeightConfigEntry 列表。
        /// 需在 ConfigSystem 可用（ProcedurePreload 之后）时调用。
        /// </summary>
        public static List<WeightConfigEntry> LoadEntries()
        {
            var table = ConfigSystem.Instance.Tables.TbWeightCfg;
            var list = new List<WeightConfigEntry>(table.DataList.Count);
            foreach (var row in table.DataList)
            {
                list.Add(ToEntry(row));
            }
            return list;
        }

        /// <summary>单行转换：Luban WeightCfg → WeightConfigEntry。</summary>
        public static WeightConfigEntry ToEntry(GameConfig.WeightCfg row)
        {
            return new WeightConfigEntry
            {
                Id = row.Id,
                FillBlankOdds = row.FillBlankOdds,
                RandomOdds = row.RandomOdds,
                EntropyOdds = row.EntropyOdds,
                EasyOdds = row.EasyOdds,
                HardOdds = row.HardOdds,
                IntuitionOdds = row.IntuitionOdds,
                Clearboard = row.Clearboard,
                Allunite = row.Allunite,
                HighScoreMin = row.HighScoreMin,
                HighScoreMax = row.HighScoreMax,
                FactorLow = row.FactorLow,
                FactorHigh = row.FactorHigh,
            };
        }

        /// <summary>
        /// 一键初始化：把 Luban 权重表灌入本局发牌调度器(BlockGameState 的逐局实例)。
        /// 在 GameApp 启动（ConfigSystem 就绪后）调用一次。
        /// </summary>
        public static void InitDynamicWeight()
        {
            BlockGameState.Instance.Dynamic.Init(LoadEntries());
        }
    }
}
