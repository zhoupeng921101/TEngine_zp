---
name: project-add-direct-single-chain-merge
description: AddDirect/AddToInventory 级联是单链向上:Lv2×4→Lv2×2+Lv3×1,不是 Lv3×2;断言别按"全部两两合并"算预期。
metadata:
  type: project
---

`AddDirect/AddToInventory` 的级联是**单链向上**:注入 N 个某等级后,沿一条链每满 2 合 1 上升,剩余的同级不再继续合(如 Lv2×4 → 合 1 次成 Lv2×2 + Lv3×1,不是 Lv3×2)。写「注入落收集区」类断言别按"全部两两合并"算预期,按单链语义或只断言总量增加。

**Why:** 单链语义是实现现状,直觉上的「批量两两合」会产出错误预期值,单测假阳常源于此(2026-06,item-system)。

**How to apply:** 测「注入 N 个 LvX 道具」时:① 算预期:每次只合一条链(剩下同级不再级联),② 或改成断「物品总数/总价值不变」类不依赖具体合并路径的断言。
