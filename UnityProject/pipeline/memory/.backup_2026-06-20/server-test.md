# 角色记忆:服务端测试(跨任务经验)

> 开工先读本文件;收尾把新的可复用经验沉淀进来(一条一行,过时即删)。
> 准入:只记跨任务可复用、且 agent 定义 / fantasy-net skill / Fantasy CLAUDE.md / conventions 未覆盖的经验。

- **多 Gate Scene 并发播种陷阱**:Fantasy.config 中配多个同类型 Scene(如两个 Gate)时,各 Scene 的 AwakeSystem 并发执行。若初始化包含「先 AnyAsync 后 InsertOneAsync」的幂等播种逻辑,必须捕获 DuplicateKey(11000),否则第二个 Scene 的 InsertOneAsync 抛异常,导致后续 ReloadCache 不执行、缓存为空、功能路径失效。
- **SG 产物验证手段**:服务端无独立测试项目,验证 AwakeSystem/DestroySystem/MessageRPC Handler 是否被 SG 注册,最直接手段是用 `-p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=<临时目录>` Rebuild 后 Grep `*EntitySystemRegistrar*.g.cs` 确认类型注册;编译 0 error 0 warning 是次级证明。
- **dotnet build 编译验证命令**:服务端示例工程本机只有 net9.0 运行时,需显式加 `--framework net9.0`,否则 net8.0 目标框架找不到运行时报错(但不影响编译产物核验)。
- **服务端内部触发特性可实现真往返 PASS**:进程内定时器触发(非客户端 RPC)的结算特性,无需客户端触发器:起服 + 活 MongoDB + 等节律间隔(60s)+ 日志观测 + MongoDB 直接查询(临时 .NET 控制台项目 + MongoDB.Driver 3.0.0,无需 mongosh)。第二次节律无新日志 = 幂等生效(SV6);重启后已结标记仍在 = SV7 生效。
- **MongoDB 临时查询探针**:无 mongosh 时,在 `D:\tmp` 下 `dotnet new console` + `dotnet add package MongoDB.Driver --version 3.0.0` + 用 `MongoClientSettings.ConnectTimeout/ServerSelectionTimeout=5s` 防卡死。注意新建项目默认 TargetFramework 为 net10.0(本机),运行时用 `dotnet run`(不加 `--framework net9.0`)。探针代码注意:类声明必须置于顶级语句之前(C# CS8803 规定),可将类定义放文件末尾,入口逻辑封装为 `static async Task Run()` 函数,顶级语句只写 `await Run();`。
- **便携版 mongod 起服**:mongod 不在运行时可直接启动 `D:\mongodb-portable\mongodb-win32-x86_64-windows-8.3.4\bin\mongod.exe --dbpath D:\mongodb-portable\data --port 27017 --logpath D:\mongodb-portable\mongod.log --logappend`,后台运行(Bash & 方式),3s 后检查 27017 端口即可。无需每次依赖已有 mongod 进程。
