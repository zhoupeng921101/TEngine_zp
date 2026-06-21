---
name: project-mongo-probe-console
description: MongoDB 临时查询探针 = D:\tmp 下 dotnet new console + MongoDB.Driver 3.0.0,类声明放文件末尾
metadata:
  type: project
---

无 mongosh 时,在 `D:\tmp` 下 `dotnet new console` + `dotnet add package MongoDB.Driver --version 3.0.0` + 用 `MongoClientSettings.ConnectTimeout/ServerSelectionTimeout=5s` 防卡死。

**Why:** 本机便携版 MongoDB 包不带 mongosh,SV12/SV13 类「mongodb 存储持久层」验证需独立查库手段;.NET 探针比临时安装 mongosh 轻。

**How to apply:** 新建项目默认 TargetFramework 为 net10.0(本机),运行时用 `dotnet run`(**不**加 `--framework net9.0`)。探针代码:类声明必须置于顶级语句之前(C# CS8803),把类定义放文件末尾,入口逻辑封装为 `static async Task Run()` 函数,顶级语句只写 `await Run();`。
