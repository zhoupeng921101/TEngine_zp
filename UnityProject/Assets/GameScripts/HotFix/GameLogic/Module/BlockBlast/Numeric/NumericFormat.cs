using System.Globalization;

namespace GameLogic.BlockBlast.Numeric
{
    /// <summary>
    /// 全局数值显示格式化（纯函数，与配置无关）。
    /// 0–999 原值 / K（千）/ M（百万），保留一位小数、向零截断、去尾随 0。
    /// 设计 15 §3.4：截断保证 999999 → "999.9K"（不进位、不误进 M），与 spec 示例逐字一致。
    /// </summary>
    public static class NumericFormat
    {
        // ── 可调旋钮（集中在顶部）──
        /// <summary>进入 K 缩写的下界。</summary>
        public const long K_THRESHOLD = 1000;
        /// <summary>进入 M 缩写的下界。</summary>
        public const long M_THRESHOLD = 1000000;
        /// <summary>缩写后保留小数位（spec 示例 999.9K → 1 位）。</summary>
        public const int DECIMALS = 1;
        /// <summary>true=向零截断；false=四舍五入。默认截断保 999999→999.9K。</summary>
        public const bool TRUNCATE = true;

        /// <summary>
        /// 把整数格式化成显示串：0–999 原值 / 1.5K / 999.9K / 1M / 100M。
        /// 负数保符号（防御性，数值理论非负但纯函数应稳）。
        /// </summary>
        public static string Abbreviate(long value)
        {
            // long.MinValue 取绝对值会溢出，单独兜底（数值理论不会到此，保纯函数稳）。
            if (value == long.MinValue)
            {
                return "-" + AbbreviateAbs(long.MaxValue);
            }

            string sign = value < 0 ? "-" : "";
            long abs = value < 0 ? -value : value;
            return sign + AbbreviateAbs(abs);
        }

        private static string AbbreviateAbs(long abs)
        {
            if (abs < K_THRESHOLD)
            {
                return abs.ToString(CultureInfo.InvariantCulture);
            }
            if (abs < M_THRESHOLD)
            {
                return Scale(abs, K_THRESHOLD) + "K";
            }
            // 超 M 档上界（spec 只到 9999999）默认续用 M，不新增 B 档（设计 §七 O6）。
            return Scale(abs, M_THRESHOLD) + "M";
        }

        /// <summary>
        /// 把 abs/unit 保留 DECIMALS 位（TRUNCATE 则向零截断），去掉无意义尾随 0。
        /// 全程整数运算，避免浮点误差让 999999 误进位。
        /// </summary>
        private static string Scale(long abs, long unit)
        {
            long pow = 1;
            for (int i = 0; i < DECIMALS; i++) pow *= 10;

            // scaled = abs / unit 保留 DECIMALS 位小数，放大成整数表示（值 ×10^DECIMALS）。
            long scaledTimesPow;
            if (TRUNCATE)
            {
                // floor(abs/unit * pow) —— 先乘后除，整数向零截断。
                scaledTimesPow = abs * pow / unit;
            }
            else
            {
                // round(abs/unit * pow) —— 四舍五入（half away from zero）。
                scaledTimesPow = (abs * pow + unit / 2) / unit;
            }

            long intPart = scaledTimesPow / pow;
            long fracPart = scaledTimesPow % pow;

            if (fracPart == 0)
            {
                // 整除（如 1000/1000=1）：去掉小数 → "1"
                return intPart.ToString(CultureInfo.InvariantCulture);
            }

            // 拼小数串并去尾随 0（如 1.50→1.5；DECIMALS=1 时不会有多位但保持通用）。
            string fracStr = fracPart.ToString(CultureInfo.InvariantCulture).PadLeft(DECIMALS, '0');
            fracStr = fracStr.TrimEnd('0');
            if (fracStr.Length == 0)
            {
                return intPart.ToString(CultureInfo.InvariantCulture);
            }
            return intPart.ToString(CultureInfo.InvariantCulture) + "." + fracStr;
        }
    }
}
