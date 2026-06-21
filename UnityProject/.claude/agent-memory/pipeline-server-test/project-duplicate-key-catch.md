---
name: project-duplicate-key-catch
description: Fantasy 服务端 MongoDB DuplicateKey 捕获口径:双 catch 缺一不可
metadata:
  type: project
---

Fantasy 服务端统一用以下两条 catch 捕获 MongoDB 重复键,缺一不可:

1. `catch (MongoWriteException e) when (e.WriteError?.Category == ServerErrorCategory.DuplicateKey)`
2. `catch (MongoCommandException e) when (e.Code == 11000)`

两条同时出现于:`RedeemDecisionHelper.TryWriteRecord`、`TryConsumeGlobalSlot`、`SeedSampleCodes`(返修后)。

**Why:** MongoDB 驱动在不同操作路径下可能抛出不同异常类型(WriteException vs CommandException),只捕其中一种会在特定操作模式下漏捕。

**How to apply:** 审查或实现任何「以 _id 唯一键作幂等依据」的 InsertOneAsync 或 FindOneAndUpdateAsync + upsert 时,确认两条 catch 均已覆盖。
