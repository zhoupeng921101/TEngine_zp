# 自检队列(pipeline-lite)

> 客户端自检(batchmode 编译 + EditMode 单测)的待办队列,把 dev 交付与自检执行解耦(即时交付 + 批量补检)。
> **生产**:pipeline-lite 客户端实现段(pipeline-lite-dev)交付时不跑 unity,改在此追加一条自检条目。
> **消费**:用户手动 `/pipeline-lite-selftest` 时读本队列,跑一次 `dev-selftest.ps1`(全量,一次覆盖所有累积条目)。自检跑在 junction 孪生工程(`UnityProject_selftest`,自有 Library 与单实例锁),主 Editor 可全程开着。PASS 清空条目,FAIL 留队待修。
> **维护**:PASS 即删除本轮所有条目(工作状态层,不留过期);FAIL 时条目留队,修复后重跑。仅客户端 unity 自检入此队列;服务端 `dotnet build` 交付前内联跑,不入队。

## 条目格式

```
### [ ] <任务标题> · <YYYY-MM-DD>
- 基线:<git HEAD 短哈希>
- 改动文件:<相对路径清单>
- 关键 EditMode 测试:<本轮新增/受影响的测试类或用例名>(供 FAIL 时定位;门仍跑全量 EditMode)
- 期望:0 编译错误 + 全量 EditMode 绿
```

## 待检条目

(空)
