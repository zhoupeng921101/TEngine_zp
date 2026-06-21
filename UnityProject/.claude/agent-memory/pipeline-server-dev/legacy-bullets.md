---
name: legacy-bullets
description: pipeline-server-dev 历史经验整体归档(从 pipeline/memory/server-dev.md 迁入,后续触发时按条独立化)
metadata:
  type: project
---

> 本文件是 2026-06-21 迁移期的整体归档:把 `pipeline/memory/server-dev.md` 原 bullet list 一次性搬入,保留紧凑性。后续 server-dev 收尾沉淀新经验时按结构化格式独立写(`feedback-*.md` / `project-*.md` 等),遇本文件内重复或过时条目可独立化或删除。独立文件 [[proto-exporter-quirks]] 已与本文件互不重复(导出工具坑点专项)。

- 服务端工程**无 Luban 配置集成**(无 TbXxx / .bytes 加载链);需要「与客户端配置同源」的服务端权威配置时,按 redeem/rank 先例**播种 MongoDB 文档**(值手抄客户端 xlsx 口径),不建 Luban→服务端导出路径(那是不存在的大工程)。运营热改需求出现时才上数据库集合管理。
- 服务端裁决逻辑可在不起整服的前提下**对活 MongoDB 实跑验证**:独立 console 探针(`MongoDB.Driver` 版本对齐 Fantasy.Net 引用的 3.8.0)复刻 helper 的 MongoDB 调用,在隔离集合上断言 SV 行为(尤其并发取最优),用后 DropCollection 清理。比"仅编译 + 起服看 init 日志"强,且不需客户端 RPC harness。
- 本机 MongoDB 可能已有 mongod 在跑(便携版 D:\mongodb-portable);再起一个会 DBPathInUse(锁文件占用)。先 `Get-Process mongod` 探测 + `Test-NetConnection 127.0.0.1 27017`,已在跑就直接用,别重复起。便携版只带 mongod/mongos,无 mongosh/mongo 客户端壳——查库走 .NET 驱动探针,不靠 shell。
- 空 body 的 Outer 消息(`message C2G_X // IRequest,G2C_Y { }` 大括号内留空行)导出工具支持、生成可用的对象池类型;用于「把我的 X 给我」这类无业务参数请求(身份从会话取)。OuterMessage.proto 有现成先例(C2G_TestEmptyMessage / C2G_CreateAddressableRequest)。define-common.md 的消息示例都带字段,易误以为消息必须有字段。
- 「广播 + 定向」两路插入、一套领取的存储模型:广播来源(全服模板,缓存遍历)与定向来源(按账号集合 + Account 索引查)合并下发;对外标识用前缀区分来源(广播 "t{模板id}" / 定向 "d{guid}"),使按账号防重记录的 _id="{account}|{标识}" 两路键空间不重叠、同一套原子防重/抽奖兼容两路。供「运营广播 + 系统定向发奖入口」类需求复用(邮件先例)。
- 触发节律(`FTask.RepeatedTimer(scene, 间隔, Action)` 回调内 `helper.XxxAsync().Coroutine()`)放进 Awake 组件、Destroy 时 `FTask.RemoveTimer(scene, ref id)` 取消;节律快慢不影响正确性(幂等兜底),取最省间隔即可。
- 客户端 Luban 配置(rank/gift_random 等)无 xlsx 源在 repo、运行期是 `Assets/AssetRaw/Configs/bytes/*.bytes`;要核服务端权威表与客户端同源,写个一次性 C# 解码器按行类(`GameConfig/Xxx.cs` 构造器读字段顺序)+ Luban ByteBuf varint(uint:首字节 <0x80 单字节 / <0xc0 双字节 (h&0x3f)<<8|b1 / <0xe0 三字节 (h&0x1f)<<16|... ;long 同 scheme 更多字节;表头 ReadSize=行数 varint)解 .bytes 取真实行值。比读客户端 POCO 缺省值靠谱。
- bash 工具的 `/tmp` = `C:\Users\pc\AppData\Local\Temp`(git-bash usertemp 挂载),但 Write 工具写 `/tmp/...` 时两者路径解析可能不一致(Write 报成功 bash 却 ls 不到)。写「bash 要 dotnet run 的临时探针工程」时,Write 用解析后的绝对 Windows 路径 `C:\Users\pc\AppData\Local\Temp\...`,别用 `/tmp/...`,免得编译报 CS5001「找不到 Main」(其实是文件没落到 bash 看的目录)。
