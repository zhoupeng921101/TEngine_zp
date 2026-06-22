---
name: time-regen-carry-remainder
description: 实时驱动的资源恢复(体力时基/离线补算)纯方法:注入 now、首次初始化不补、负时差按 0、记录时刻只推进整除秒数(余秒留存)。
metadata:
  type: project
---

时基/离线资源恢复(如体力每 N 秒 +1)做成纯方法 `ApplyTimeRegen(long nowUnixSec)`,挂 MergeOrderState、注入 now 供单测、记录时刻字段(`LastEnergyRegenTime`,Unix 秒)进盘 + 进悔棋快照。四条不变量:① 首次无记录(==0)→ 以 now 初始化、本次不补;② 负时差(now<记录)→ 不倒扣/不抛/不更新记录;③ 应恢复=floor(Δ/interval)×perTick,封顶软上限不溢出、体力本就>软上限(订单溢出)则不动;④ **记录时刻只推进「已整除掉的秒数」(`+= ticks*interval`),余秒留到下次累计**。

**Why:** 第④条是最易漏的坑——若每次把 `LastEnergyRegenTime` 直接设成 now,则不满一个 tick 的余秒被吞;短间隔反复进入玩法窗(每次几秒)会让恢复永远卡在 0,玩家挂机也回不了血。只推进整除秒数,余秒跨多次调用累计,凑满一个 interval 才恢复 + 推进。设计 49 §3.2「无尽脱困保险」依赖恢复真能推进,这条是兜底成立的前提。

**How to apply:** 任何「按真实时间累计的资源」(体力/离线产出/冷却)都用此范式。持久化只动两字段(当前值 + 上次记录时刻),CurrentVersion 不升、平铺进既有元层 DTO(旧档缺省 0 → 判「无记录」夹回起始值,不信缺省 0)。进窗时机:Reset → ImportMeta(meta) → ApplyTimeRegen(NowUnixSec()),有档补离线、无档初始化。单测注入 `t0 + interval*k + 余秒` 断言「补 k tick 且记录只进 k*interval」。生产 now 源 `DateTimeOffset.UtcNow.ToUnixTimeSeconds()`(本地单机去变现,不做时钟防作弊,沿设计 14 §3.6 同口径)。
