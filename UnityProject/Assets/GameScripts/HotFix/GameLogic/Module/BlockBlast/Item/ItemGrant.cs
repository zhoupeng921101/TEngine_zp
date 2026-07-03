using System;
using System.Collections.Generic;
using GameLogic.BlockBlast.Player;
using GameLogic.Config;

namespace GameLogic.BlockBlast.Item
{
    /// <summary>礼包 / 道具产出的种类（设计 16 §3.7 + 设计 41 §3.4 EVENT 扩档）。</summary>
    public enum GrantKind
    {
        /// <summary>纯持有材料（进背包，无即时效果）。</summary>
        None,
        /// <summary>货币（落数值系统 num_id 对应字段）。</summary>
        Numeric,
        /// <summary>图案（落 MergeOrderState.AddDirect）。</summary>
        Pattern,
        /// <summary>自选礼包（返回候选列表交 UI 选）。</summary>
        GiftSelect,
        /// <summary>随机礼包（按权重抽）。</summary>
        GiftRandom,
        /// <summary>EVENT 头像/框解锁（TargetId=avatar id，Amount 恒 1，适配器忽略；设计 41 §3.4）。</summary>
        EventUnlock,
    }

    /// <summary>
    /// 「要发什么」的产出结构（设计 16 §3.7）。<c>ItemGrant.Resolve</c> 只产出此结构、不发奖，
    /// 由调用方 / 适配器接到既有系统——使解析逻辑可纯单测，不拉起 MergeOrderState。
    /// </summary>
    public readonly struct GrantPayload
    {
        /// <summary>产出种类。</summary>
        public readonly GrantKind Kind;
        /// <summary>目标 id：Numeric=num_id / Pattern=MergeElement key / Gift*=礼包 index。</summary>
        public readonly int TargetId;
        /// <summary>数量：货币 / 图案数量（已乘 count）。</summary>
        public readonly int Amount;
        /// <summary>图案等级（Pattern）。</summary>
        public readonly int Level;
        /// <summary>礼包开启次数（Gift*）。</summary>
        public readonly int Times;

        public GrantPayload(GrantKind kind, int targetId, int amount, int level, int times)
        {
            Kind = kind;
            TargetId = targetId;
            Amount = amount;
            Level = level;
            Times = times;
        }
    }

    /// <summary>
    /// UseEffect 解析 + 产出落点适配器（设计 16 §3.7）。
    /// </summary>
    /// <remarks>
    /// <see cref="Resolve"/> 把「道具 ×count」解析成 <see cref="GrantPayload"/>（不发奖）。
    /// 适配器 <see cref="ApplyNumeric"/> / <see cref="ApplyPattern"/> 把产出接到既有 MergeOrderState
    /// public 方法（RefundEnergy / AddPiety / Exp / AddDirect），不复制发奖逻辑（避免两处漂移）。
    /// 随机礼包递归（开出的道具再 Resolve）加深度上限防配置环（§八 风险表）。
    /// </remarks>
    public static class ItemGrant
    {
        /// <summary>随机礼包递归解析的深度上限（防配置自指环，设计 16 §八）。</summary>
        public const int MaxGiftDepth = 5;

        /// <summary>
        /// 判产出列表是否含 EVENT 解锁（设计 41 §3.5 D3：调用方据此决策是否触发 SavePlayer 落盘）。
        /// <c>list</c> 为 null / 空 → false（不抛）。
        /// </summary>
        public static bool ContainsEventUnlock(IReadOnlyList<GrantPayload> list)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Kind == GrantKind.EventUnlock) return true;
            return false;
        }

        /// <summary>
        /// 解析单个道具的产出（不发，只产出结构）。
        /// UseEffect：1 货币 / 2 图案 / 3 自选礼包 / 4 随机礼包 / 5 EVENT 头像/框解锁（设计 41 §3.4）/ 其余纯持有。
        /// </summary>
        public static GrantPayload Resolve(ItemDef def, int count)
        {
            if (def == null) return new GrantPayload(GrantKind.None, 0, count, 0, 0);
            switch (def.UseEffect)
            {
                case 1: return new GrantPayload(GrantKind.Numeric,     def.UseValue, def.UseNum * count, 0, 0);
                case 2: return new GrantPayload(GrantKind.Pattern,     def.UseValue, def.UseNum * count, def.UseLevel, 0);
                case 3: return new GrantPayload(GrantKind.GiftSelect,  def.UseValue, 0, 0, def.Param);
                case 4: return new GrantPayload(GrantKind.GiftRandom,  def.UseValue, 0, 0, def.Param);
                case 5: return new GrantPayload(GrantKind.EventUnlock, def.UseValue, count, 0, 0);            // 设计 41 §3.4 EVENT
                default: return new GrantPayload(GrantKind.None, def.Id, count, 0, 0); // 纯持有材料
            }
        }

        // ── 适配器：把产出接到既有系统（不复制发奖逻辑）──────────

        /// <summary>
        /// 货币产出落既有字段：经 num_id 映射到 MergeOrderState 既有 public 方法。
        /// 映射约定见 <see cref="NumericConfigMgr"/>（Exp=1 / Piety=2 / Diamond=3 / Energy=4）。
        /// 钻石（=3）数值系统尚未实装专门字段，本轮不落（去变现：不做购买），返 false。
        /// </summary>
        public static bool ApplyNumeric(MergeOrderState state, GrantPayload payload)
        {
            if (state == null || payload.Kind != GrantKind.Numeric || payload.Amount <= 0) return false;
            switch (payload.TargetId)
            {
                case NumericConfigMgr.Exp:    state.Exp += payload.Amount; return true;
                case NumericConfigMgr.Piety:  state.AddPiety(payload.Amount); return true;
                case NumericConfigMgr.Energy: state.RefundEnergy(payload.Amount); return true;
                default: return false; // 钻石(3)等无既有字段：本轮不落
            }
        }

        /// <summary>图案产出落既有 <c>MergeOrderState.AddDirect</c>（既有方法，不复制）。</summary>
        public static void ApplyPattern(MergeOrderState state, GrantPayload payload)
        {
            if (state == null || payload.Kind != GrantKind.Pattern) return;
            int level = payload.Level < 1 ? 1 : payload.Level;
            state.AddDirect((MergeElement)payload.TargetId, level, payload.Amount);
        }

        /// <summary>
        /// 获取一个道具时按 <see cref="ItemDef.Automatic"/> 分流（设计 16 §3.7）：
        /// automatic=1 → 立即结算（落既有系统）；automatic=0 → 进背包待用。
        /// 返回本次「立即结算」的产出列表（automatic=0 时返回空列表，调用方负责入背包）。
        /// state / rng 为 null 时仍返回产出结构（不落实际系统，供纯解析单测）。
        /// </summary>
        public static List<GrantPayload> GrantOnAcquire(ItemDef def, int count, MergeOrderState state, System.Random rng)
        {
            var produced = new List<GrantPayload>();
            if (def == null) return produced;
            if (def.Automatic != 1) return produced; // 进背包路径：调用方处理，不立即结算
            ResolveAndApply(def, count, state, rng, produced, 0);
            return produced;
        }

        /// <summary>
        /// 解析并落点（随机礼包递归展开）。把展开到的终末产出（Numeric/Pattern/None）记入 <paramref name="produced"/>。
        /// 深度超 <see cref="MaxGiftDepth"/> 停止递归（防配置环）。
        /// </summary>
        private static void ResolveAndApply(
            ItemDef def, int count, MergeOrderState state, System.Random rng,
            List<GrantPayload> produced, int depth)
        {
            if (def == null || depth > MaxGiftDepth) return;
            var payload = Resolve(def, count);
            switch (payload.Kind)
            {
                case GrantKind.Numeric:
                    if (state != null) ApplyNumeric(state, payload);
                    produced.Add(payload);
                    break;
                case GrantKind.Pattern:
                    if (state != null) ApplyPattern(state, payload);
                    produced.Add(payload);
                    break;
                case GrantKind.GiftRandom:
                    if (rng != null)
                    {
                        var rolled = GiftOpener.OpenRandom(payload.TargetId, payload.Times, rng);
                        foreach (var e in rolled)
                        {
                            var sub = ItemConfigMgr.GetItem(e.ItemId);
                            ResolveAndApply(sub, e.Num, state, rng, produced, depth + 1);
                        }
                    }
                    break;
                case GrantKind.GiftSelect:
                    // 自选礼包：交 UI 选，本轮不自动展开（UI 不接，O9）。记产出结构供调用方处理。
                    produced.Add(payload);
                    break;
                case GrantKind.EventUnlock:
                    // EVENT 头像/框解锁（设计 41 §3.5）：取 GameContext.Player + AvatarConfigMgr.GetAvatar 调
                    // AvatarUnlockService.GrantUnlock（Type 自动分流头像/框）。任何情形（成功 / Player null /
                    // entry null）均 produced.Add（供调用方按 D3 决策核 SavePlayer），不抛。
                    ApplyEventUnlock(payload);
                    produced.Add(payload);
                    break;
                default:
                    produced.Add(payload); // 纯持有材料：调用方入背包
                    break;
            }
        }

        /// <summary>
        /// EVENT 解锁适配器（设计 41 §3.5）：纯静态、查无即静默。落盘归调用方触发（D3）。
        /// PlayerInfo 来源：<c>GameContext.Instance.Player</c>（沿设计 18/25 范式，与 UIPlayerInfoPanel 同源）；
        /// 配置查询：<see cref="AvatarConfigMgr.GetAvatar"/>（查无返 null 不抛）；
        /// 写入分流：<see cref="AvatarUnlockService.GrantUnlock"/> 内 <c>AvatarEntry.Type</c> 自动分流头像/框集合 + AppendDistinct 幂等。
        /// </summary>
        private static void ApplyEventUnlock(GrantPayload payload)
        {
            var player = GameLogic.GameContext.IsValid ? GameLogic.GameContext.Instance.Player : null;
            if (player == null) return; // EditMode 单测 / 启动期未加载玩家 → 静默
            var entry = AvatarConfigMgr.GetAvatar(payload.TargetId);
            if (entry == null) return; // 配置错指 / 表未注入 → 静默（沿 40 §五崩法表）
            AvatarUnlockService.GrantUnlock(player, entry);
        }
    }
}
