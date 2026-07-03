namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 背包批次轨投影元素(背包系统·客户端段):有有效期道具的一批,服务端权威、客户端只持投影。
    /// 与堆叠轨(<see cref="GameLogic.BlockBlast.Item.ItemBag"/>,itemId→数量)并列。
    /// 过期时刻 <see cref="ExpireMs"/> 为服务端权威绝对 Unix 毫秒;客户端倒计时用它减去「服务端时间基准 + 本地流逝」,不信本地墙钟。
    /// </summary>
    public readonly struct InventoryLot
    {
        public readonly string LotId;
        public readonly int ItemId;
        public readonly long Count;
        public readonly long AcquireMs;
        public readonly long ExpireMs;

        public InventoryLot(string lotId, int itemId, long count, long acquireMs, long expireMs)
        {
            LotId = lotId ?? string.Empty;
            ItemId = itemId;
            Count = count;
            AcquireMs = acquireMs;
            ExpireMs = expireMs;
        }
    }

    /// <summary>
    /// 使用道具裁决结果码(客户端中立枚举,镜像服务端 UseItemResultCode + 客户端专属 NetworkDown)。
    /// 枚举值不要求与服务端一致:由 <see cref="InventoryRpcGatewayProd"/> 映射,业务侧按语义分支。
    /// </summary>
    public enum UseItemCode
    {
        Success = 0,
        NotLoggedIn = 1,
        UnknownItem = 2,
        NotEnough = 3,
        Expired = 4,
        NotUsable = 5,
        Duplicate = 6,
        ServiceUnavailable = 7,
        InvalidRequest = 8,
        /// <summary>客户端侧未连接 / 发不出 / 超时(服务端无此码)。</summary>
        NetworkDown = 9,
    }

    /// <summary>
    /// 使用道具结果(框架中立)。成功携带实际消耗量 + 产出货币(本轮效果只有货币);
    /// 失败 <see cref="Code"/> 非 Success 且消耗/产出为 0。背包投影不由本结果直接更新——
    /// 成功/重复由服务端背包推送整份覆盖,货币产出由属性推送刷新(沿 <see cref="PlayerAttrService"/> 权威覆盖语义)。
    /// </summary>
    public readonly struct UseItemResult
    {
        public readonly UseItemCode Code;
        public readonly int ItemId;
        public readonly long ConsumedCount;
        public readonly bool HasProduce;
        public readonly AttrType ProducedType;
        public readonly long ProducedAmount;

        public UseItemResult(UseItemCode code, int itemId, long consumedCount = 0,
            bool hasProduce = false, AttrType producedType = default, long producedAmount = 0)
        {
            Code = code;
            ItemId = itemId;
            ConsumedCount = consumedCount;
            HasProduce = hasProduce;
            ProducedType = producedType;
            ProducedAmount = producedAmount;
        }

        public bool IsSuccess => Code == UseItemCode.Success;

        /// <summary>纯失败结果(无消耗无产出)。</summary>
        public static UseItemResult Fail(UseItemCode code, int itemId) => new UseItemResult(code, itemId);
    }
}
