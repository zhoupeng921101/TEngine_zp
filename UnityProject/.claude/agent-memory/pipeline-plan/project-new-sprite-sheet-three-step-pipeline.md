---
name: project-new-sprite-sheet-three-step-pipeline
description: 新精灵表换皮屏写清三步资产流程:ASCII 目录导入/Pack 产表/dev 先验一张子图
metadata:
  type: project
---

用设计 24 打表工具产**新**精灵表的换皮屏(前几屏复用 Sheet_settings、本屏首次产新表):资产流程写清三步——

① 切图导入 **ASCII 目录** `AssetRaw/UIRaw/Atlas/<screen>/`(中文目录致 location/子图名带中文、寻址/git 出问题)+ 九宫格图源 PNG 设 Border;

② 跑 `UIAtlasPacker.Pack("...<screen>")` 或菜单 `Tools/UI/打表(...)` 产 `Sheet_<screen>.png`(不覆盖已存在文件,重跑先删旧);

③ **dev 落地第一步先验取到一张子图**再铺满。本屏核心验证点 = 打表工具对新切图产出正确 + 新表能被 YooAsset 当 SubAssets 加载(V1)。

location=Sheet_<screen>(收集器 UIRaw 组自动收,不改收集器)。

**Why:** 2026-06 Sheet_tarot_mode 首用打表工具产新表先例。中文目录会让 location/子图名带中文,寻址和 git 都出问题。

**How to apply:** 新表屏稿单设「§资产流程」节,严格三步;dev 验收第一步=取一张子图断言非 null,再铺满。
