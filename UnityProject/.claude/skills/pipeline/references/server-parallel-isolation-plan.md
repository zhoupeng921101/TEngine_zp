# 服务端流水线并行隔离方案

> 状态:待实施(设计已定,代码未落地)。本文是实现文档(主题即代码),按 `.claude/rules/conventions.md` 例外条款写明代码锚点;具体 file:line 由实现时现场推导。

## 目标与范围

让多条 `target=server` 流水线在同机并发运行,各自独立的工作树、端口、数据库、state 信道互不干扰。

**只含服务端。** 客户端(`target=client`)并行不在本方案内:Unity 编辑器是单实例资源,Play 手验 / UI 搭建 / MCP Prefab 操作依赖实时编辑器,无法在同一工程路径上开第二个编辑器,加编排参数解不了。服务端无此约束——运行时单例(端口、数据库)都在配置文件里,可按 run 参数化。

**沿用流水线边界**:不 push、不发布;本地 checkpoint commit 与 worktree 合并属本方案内动作。

## 三类冲突与服务端解法

| 冲突层 | 现状 | 服务端解法 |
|--------|------|-----------|
| git 工作树 | Fantasy 是嵌套独立仓(toplevel = `D:/work/TEngine_block/Fantasy`),并发提交/清理互相覆盖 | 每条 run 在 Fantasy 仓建独立 worktree + 独立分支 |
| state 信道 | `pipeline/state/server-dev.md`、`server-test.md` 等路径硬编码、且被 git 跟踪,两条 run 读写同一组文件会串台 | `state/<runId>/` 命名空间 |
| 运行时单例 | `Fantasy.config` 写死端口 `20000/20001/20010` + `11001-11007`,库名 `fantasy_main1/main2`,均连 `127.0.0.1:27017` | 端口整体加 run 专属 offset;库名加 `_<runId>` 后缀;同一 mongod 不同库 |

服务端不触 Unity MCP(走 dotnet),故 MCP 传输配置(stdio/http)与本方案无关。

## 隔离设计

### 1. git 工作树(Fantasy 仓)

Fantasy 不被外层仓库跟踪(无 submodule、未 gitignore,是纯嵌套独立仓),须在 Fantasy 仓自身上建 worktree。

- 创建:`git -C D:/work/TEngine_block/Fantasy worktree add -b auto/<slot> D:/work/Fantasy-wt/<slot>`(放到仓库外的兄弟目录,避免嵌套进被跟踪树)
- 清理:`git -C D:/work/TEngine_block/Fantasy worktree remove <path>` 后 `worktree prune`
- Windows 注意:路径带引号、避开空格;`remove` 前确认该 worktree 无 dotnet 进程占用文件锁
- `Bin/obj` 已 gitignore,每个 worktree 各自独立构建产物,无需额外隔离

### 2. state 信道(runId 命名空间)

把硬编码的 `pipeline/state/<role>.md` 改为 `pipeline/state/<runId>/<role>.md`,`<runId>` 由 boss 每次发起时生成(增量名或时间戳)。

参数化的引用点:

- `pipeline-auto.js`:state 路径常量 + 各角色简报字符串里的 state 路径
- `SKILL.md`:恢复协议、关单事务(四件套归档)、全栈编排三处的 state 路径
- `pipeline-server-dev.md` / `pipeline-server-test.md` 角色卡的输入/产出/收尾节里的 state 路径

`state/boss.md`「最近关单」索引与 `archive/` 是单一信息源,**并发追加须串行**(见「并发约束」)。

### 3. 运行时单例(端口 + MongoDB 库)

唯一事实源 = `Fantasy/examples/Server/APP/Entity/Fantasy.config`(经 Entity.csproj 的 `AdditionalFiles` 拷进 Bin,启动时从 Bin 读)。CLI 无端口/库/配置路径覆盖项,只能改这份配置文件。

每条 run 在自己的 worktree 内改本份 config:

- **端口**:对所有非零 `outerPort`/`innerPort` 统一加 run 专属 offset(步长 ≥ 单实例端口跨度,建议 100 的倍数:run B +100、run C +200…)。`outerPort="0"` 表示无外网绑定,保持 0
- **库名**:`fantasy_main1` → `fantasy_main1_<runId>`,`fantasy_main2` 同理;`WorldB` 的 `doc` 库 `dbConnection` 为空(不连),不动
- **mongod**:所有 run 共用同一个 `127.0.0.1:27017` 实例,只是各连各的库;run 结束 `dropDatabase` 丢弃后缀库
- **本地连接串注意**:`dbConnection` 的 `mongodb://127.0.0.1:27017/` 是本机环境配置(按惯例不入库,见 local-mongodb-for-server-roundtrip 记忆);新建 worktree 时该本地改动不会随 git 带过去,每个 worktree 需各自补这份连接串改动

## 编排层改造

### 新增 args(boss 经 Workflow 传入)

| 字段 | 含义 | 透传目标 |
|------|------|---------|
| `runId` | state 命名空间前缀 + 库名后缀 | 所有 state 读写点;Fantasy.config 库名 |
| `serverRoot` | 该 run 的 Fantasy worktree 绝对路径 | 角色卡的 `cd`/`git -C`/`dotnet` 工作根;关单 commit |
| `portOffset` | 该 run 的端口偏移 | server-dev 启服前改 Fantasy.config |

服务端角色卡当前已用绝对路径 `cd` 进 Fantasy,把写死的 `D:\work\TEngine_block\Fantasy` 换成 `serverRoot` 即可,**无需 `agent()` 支持 cwd 透传**(这是服务端比客户端干净的关键)。

### 关单提交与合并

- 每条 run 在自己的 worktree 分支提交:关单 checkpoint commit 从 `git -C "D:\work\TEngine_block\Fantasy"` 改为 `git -C <serverRoot>`
- boss 把各 run 分支**串行 merge** 回 Fantasy 主分支(`block`):git index 非并发安全,合并必须串行
- merge 不跑 agent,耗时相对闭环可忽略;真正风险是两条 run 改同一批源文件产生内容冲突——boss 派活时按文件域切分 backlog(并发 run 不碰同一批文件)规避

## 改造清单与工作量

| 文件 | 改动 | 量级 |
|------|------|------|
| `pipeline-auto.js` | state 路径参数化(常量 + 简报)、透传 `runId`/`serverRoot`/`portOffset` | 中 |
| `SKILL.md` | 恢复协议 / 关单事务 / 全栈编排的 state 与仓库路径参数化;新增并发约束与文件域切分规约 | 中 |
| `pipeline-server-dev.md` | 工作根改 `serverRoot`;启服前按 `portOffset`/`runId` 改 Fantasy.config | 小 |
| `pipeline-server-test.md` | 工作根改 `serverRoot`;往返验证连对端口、验完 drop 对库 | 小 |
| 新增脚手架 | worktree 创建/清理 + offset/库名分配 + config 重写 | 小-中 |

合计约 **1-2 天**(不含本方案外的前置探针——服务端无前置)。

## 并发约束与上限

- **mongod**:单实例共享,不同库;不重复起第二个 mongod(会 `DBPathInUse` 锁)
- **端口**:区间充足,实际不构成并发上限
- **机器资源**:并发 = N 份 dotnet build + N 个跑服进程,受 CPU/内存约束,建议并发 ≤ 2-3
- **串行点**:① boss 关单 merge 回主分支;② `boss.md`「最近关单」索引与 `archive/` 的追加。这两处是设计上必须串行的汇聚点,不能并发

## 验收标准(行为级)

- 两条 server run 并发:各自起服在不相交端口、写不同库,往返验证互不干扰
- 各 run 提交到独立 worktree 分支;boss 串行 merge 回主分支,文件域不重叠时无内容冲突
- run 结束:worktree `remove` + `prune` 干净,测试库 `dropDatabase` 无残留
- 单条 server run(非并发)行为不变(runId 缺省时退回单 run 路径,向后兼容)

## 未决项(实施时定)

- `runId` 缺省的向后兼容形态:无 `runId` 时是否退回现有 `pipeline/state/<role>.md` 扁平路径,还是统一进 `state/default/`
- 文件域切分由 boss 人工判断还是加机械校验(派活前 grep 两 run 目标文件集是否相交)
- worktree 分支命名与清理时机(关单即清,还是保留到链终止统一清)
