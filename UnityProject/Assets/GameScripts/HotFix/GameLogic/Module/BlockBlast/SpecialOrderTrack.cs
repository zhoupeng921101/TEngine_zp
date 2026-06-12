using System.Collections.Generic;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 特殊订单类型。设计 11 §三：加急 / 剧情 / 黄金时段三类分时复用同一特殊槽。
    /// 占槽优先级（高→低）：剧情 &gt; 加急 &gt; 黄金时段。
    /// </summary>
    public enum SpecialOrderKind
    {
        None = 0,
        GoldenHour = 1, // 黄金时段（最低）
        Express = 2,    // 加急（带倒计时）
        Story = 3,      // 剧情/主线（最高，进度唯一钥匙）
    }

    /// <summary>
    /// 一张特殊订单：在 <see cref="Order"/>（类型,等级,数量）之上附 <see cref="SpecialOrderKind"/>。
    /// 加急单可带剩余倒计时（秒，&lt;0 表示无倒计时）。
    /// </summary>
    public readonly struct SpecialOrder
    {
        public readonly SpecialOrderKind Kind;
        public readonly Order Req;
        public readonly float CountdownSec; // &lt;0 = 无倒计时（剧情/黄金时段）

        public SpecialOrder(SpecialOrderKind kind, Order req, float countdownSec = -1f)
        {
            Kind = kind;
            Req = req;
            CountdownSec = countdownSec;
        }

        public bool IsValid => Kind != SpecialOrderKind.None && Req.IsValid;
    }

    /// <summary>
    /// 特殊订单轨（设计 11 §三·双轨并发模型的特殊轨）。0 或 1 个占槽 + 一条等待队列。
    /// 纯逻辑：占槽优先级仲裁、已占槽不被中途踢出、高优先级单进等待队列、交付/过期后从队列升起最高优先级单。
    ///
    /// 不变量：
    /// · 同屏只占 1 个特殊槽（<see cref="Occupied"/> 至多 1 张）；
    /// · 已占槽的订单不被更高优先级中途踢出——新请求一律入等待队列；
    /// · 槽空时，从等待队列取「优先级最高」的一张升入占槽（同优先级按入队先后 FIFO）。
    /// </summary>
    public sealed class SpecialOrderTrack
    {
        /// <summary>当前占槽订单（Kind==None 表示空槽）。</summary>
        public SpecialOrder Occupied;

        /// <summary>等待队列：占槽期间到来的特殊单暂存于此，按优先级升起。</summary>
        private readonly List<SpecialOrder> _waiting = new List<SpecialOrder>();

        /// <summary>槽是否被占。</summary>
        public bool HasOccupied => Occupied.IsValid;

        /// <summary>等待队列当前长度（测试/UI 可读）。</summary>
        public int WaitingCount => _waiting.Count;

        public void Reset()
        {
            Occupied = default;
            _waiting.Clear();
        }

        /// <summary>
        /// 请求一张特殊单占槽：槽空则直接占；槽已占则入等待队列（绝不踢出在占槽的单）。
        /// 返回 true 表示直接占槽，false 表示进了等待队列。
        /// </summary>
        public bool Request(SpecialOrder order)
        {
            if (!order.IsValid) return false;
            if (!HasOccupied)
            {
                Occupied = order;
                return true;
            }
            _waiting.Add(order);
            return false;
        }

        /// <summary>当前占槽单可交付（库存满足）时调用：腾空槽 → 从等待队列升起最高优先级单。</summary>
        public void OnDelivered()
        {
            Occupied = default;
            PromoteFromWaiting();
        }

        /// <summary>加急单倒计时到点（过期）时调用：腾空槽 → 升起等待队列。</summary>
        public void OnExpired()
        {
            Occupied = default;
            PromoteFromWaiting();
        }

        /// <summary>
        /// 推进加急单倒计时。返回 true 表示当前占槽的加急单本次到点过期（已腾空并升起队列）。
        /// 非加急 / 无倒计时 / 槽空 时不做任何事。
        /// </summary>
        public bool TickCountdown(float deltaSec)
        {
            if (!HasOccupied || Occupied.Kind != SpecialOrderKind.Express || Occupied.CountdownSec < 0f)
                return false;
            float left = Occupied.CountdownSec - deltaSec;
            if (left <= 0f)
            {
                OnExpired();
                return true;
            }
            Occupied = new SpecialOrder(Occupied.Kind, Occupied.Req, left);
            return false;
        }

        // 从等待队列取优先级最高（同级 FIFO）的一张升入占槽。
        private void PromoteFromWaiting()
        {
            if (_waiting.Count == 0) return;
            int best = 0;
            for (int i = 1; i < _waiting.Count; i++)
            {
                // Kind 数值越大优先级越高（Story=3 > Express=2 > GoldenHour=1）。
                // 严格大于才替换，保证同优先级取最早入队者（FIFO）。
                if ((int)_waiting[i].Kind > (int)_waiting[best].Kind) best = i;
            }
            Occupied = _waiting[best];
            _waiting.RemoveAt(best);
        }

        // ── 悔棋快照支持（浅拷贝即可：SpecialOrder/Order 均为不可变值类型）──
        public SpecialOrder[] SnapshotWaiting() => _waiting.ToArray();

        public void RestoreWaiting(SpecialOrder[] saved)
        {
            _waiting.Clear();
            if (saved != null) _waiting.AddRange(saved);
        }
    }
}
