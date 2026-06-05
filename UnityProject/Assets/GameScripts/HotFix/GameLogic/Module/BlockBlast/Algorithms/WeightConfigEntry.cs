namespace GameLogic.BlockBlast.Algorithms
{
    /// <summary>
    /// weightcfg.json 单项 —— P2 接 Luban 表时此 POCO 直接对接表行类型。
    /// </summary>
    public sealed class WeightConfigEntry
    {
        public int Id;
        public int FillBlankOdds;
        public int RandomOdds;
        public int EntropyOdds;
        public int EasyOdds;
        public int HardOdds;
        public int IntuitionOdds;
        public int Clearboard;
        public int Allunite;
        /// <summary>HighScoreRange[0]=min, [1]=max；max=-1 表示无上限。</summary>
        public int HighScoreMin;
        public int HighScoreMax;
        /// <summary>FactorRange 内会取 min/max（顺序无关）。</summary>
        public int FactorLow;
        public int FactorHigh;

        /// <summary>按 OddsFields.Order 顺序取 odds 数组。</summary>
        public int[] OddsArray() => new[]
        {
            FillBlankOdds, RandomOdds, EntropyOdds, EasyOdds,
            HardOdds, IntuitionOdds, Clearboard, Allunite,
        };
    }

    /// <summary>每种算法的"调权因子"。</summary>
    public sealed class WeightFactor
    {
        /// <summary>首次/换向时的增量。</summary>
        public int Basic;
        /// <summary>同向连续时的增量。</summary>
        public int Consecutive;

        public WeightFactor(int basic, int consecutive)
        {
            Basic = basic;
            Consecutive = consecutive;
        }
    }
}
