---
name: feedback-editmode-png-fixture-not-runtime
description: EditMode 测「产出资源型工具」别在测试方法内运行时建 PNG:导入是延迟的,GetAtPath 立刻返 null;正解=提交进库的固定只读小夹具。
metadata:
  type: feedback
---

EditMode 测「产出资源型工具」别在测试方法内运行时建 PNG 源资产:单个同步测试方法块内,纹理资产的导入是延迟的——`File.Copy`+`Refresh` / `AssetDatabase.CopyAsset` / `ImportAsset(ForceSynchronousImport)`(对新建目录)后立刻 `AssetImporter.GetAtPath(副本)` 返 null → 设 importer 字段 NRE,Pack 没跑到。

正确做法=**提交进库的固定只读小夹具**:在收录树下建固定目录(如 `<atlasroot>/_uiap_test_fixture/`),放几张**唯一命名**(避开既有同名,否则触 YooAsset 收集器 `AddressByFileName` 同名收录冲突红错)PNG + 预设好 .meta(Sprite/Single/border)一并提交;测试直接对它跑工具,TearDown 只删产出表、夹具保留。资产已入库 → 工具内部 GetAtPath 即时可用、零时序 NRE。需对照真实全集(如 21 子图)的核心锚断言:读「已由稳定会话产出且入库的真实产物表」做只读断言,不在测试里重跑全集打表。

**Why:** Unity 的 AssetDatabase 是异步导入,EditMode 同步方法块内 Refresh 不等导入完成;固定夹具把「导入」前置到提交时间,测试只读已就绪资产(2026-06,ui-atlas-packer 返修)。

**How to apply:** 测产出资源类工具:① 不在测里 File.Copy 新建 PNG 跑工具;② 建固定 fixture 目录提交,PNG + .meta 全入库;③ 测里只针对 fixture 跑工具,TearDown 删产出不删 fixture;④ 真实全集结果做只读断言。
