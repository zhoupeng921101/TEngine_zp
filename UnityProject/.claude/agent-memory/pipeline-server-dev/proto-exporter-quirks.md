---
name: proto-exporter-quirks
description: Fantasy 协议导出工具与跑服的非显式坑点(枚举语法/跨仓库客户端路径/运行框架)
metadata:
  type: reference
---

Fantasy 服务端段实操中遇到的、fantasy-net references 与 CLAUDE.md 未明说的工具坑点:

1. **proto 枚举值必须逗号分隔,不能用分号。** `references/protocol/define-common.md` 示例用 `;` 结尾,但实际导出工具(Tools/ProtocolExportTool)对 `;` 报 "Invalid enum value format" / "Enum has no values"。正确写法见 `examples/Config/NetworkProtocol/Outer/TestEnum.proto`:`Success = 0,`(逗号,最后一项可省)。文档与工具有漂移,以 TestEnum.proto 实测为准。

2. **客户端协议生成物可由同一次导出直接落 UnityProject。** UnityProject 的 `Assets/Fantasy/Generate/NetworkProtocol/` 与 Fantasy 仓库的 `examples/Server/APP/Entity/Generate/NetworkProtocol` 是**同一份 proto 源**(`examples/Config/NetworkProtocol`)的两个输出。把 `examples/Tools/ProtocolExportTool/ExporterSettings.json` 的 `NetworkProtocolClientDirectory` 设为 UnityProject 绝对路径,一次 `dotnet Fantasy.ProtocolExportTool.dll export --silent` 即同时生成服务端 + 客户端、双端 opcode 一致,无需手工复制。注意原始 settings 的 `NetworkProtocolDirectory` 值带尾随空格(`"../../Config/NetworkProtocol "`),会破坏路径解析,需去掉。

3. **跑服必须指定 `--framework net9.0`。** 示例项目 multi-target net8.0;net9.0,`dotnet run` 不带 framework 会报错要求指定;且本机**未装 net8.0 运行时**(只有 9.0/10.0),用 net8.0 跑会 app-launch-failed。命令:`dotnet run --project examples/Server/APP/Main/Main.csproj -c Debug --framework net9.0 -- --m Develop`。

4. **跑服编译前先杀残留 Main 进程。** 上一次 `dotnet run` 起的 Main.exe 会锁 `examples/Bin/Debug/.../Fantasy-Net.dll`,导致下次 build 的拷贝步骤报 MSB3027/MSB3021(非编译错)。build 前 `Get-Process Main | Stop-Process -Force`。

5. **示例工程有一批既有 CS8618 nullable 警告**(Account.cs/Unit.cs/各 Controller/SphereEvent handler 等),非自己引入;Hotfix/Entity 工程未开 TreatWarningsAsErrors(只核心框架项目开)。自检"0 警告"针对**自己的文件**,既有警告不在范围、不去动(外科手术式改动)。

6. **`sint64` / `sint32` / `fixed*` / `sfixed*` 这些 proto3 变长整数类型 Fantasy LightProto 源生成器不识别**(直接当 C# 类型名传到生成代码里,编译报 CS0246 "未能找到类型或命名空间名 sint64")。需要有符号 long 字段直接用 `int64`(语义无差,只是变长编码下负值多占几个字节);需要无符号或固定宽度也只能用 `int32` / `int64`。同 enum 用逗号坑(导出工具不完全实现 proto3 标准),以实测为准、不照搬 protobuf 官方语法表。
