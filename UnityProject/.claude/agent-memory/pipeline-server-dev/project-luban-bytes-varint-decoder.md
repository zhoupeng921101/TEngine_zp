---
name: project-luban-bytes-varint-decoder
description: 服务端核「客户端 Luban 配置与服务端权威表同源」时,写一次性 C# 解码器解 .bytes(Luban ByteBuf varint)取真实行值
metadata:
  type: project
---

客户端 Luban 配置(rank/gift_random 等)无 xlsx 源在 repo、运行期是 `Assets/AssetRaw/Configs/bytes/*.bytes`;要核服务端权威表与客户端同源,写个一次性 C# 解码器按行类(`GameConfig/Xxx.cs` 构造器读字段顺序)+ Luban ByteBuf varint 解 .bytes 取真实行值。

Luban ByteBuf varint 规则:
- `uint`:首字节 `<0x80` 单字节 / `<0xc0` 双字节 `(h&0x3f)<<8|b1` / `<0xe0` 三字节 `(h&0x1f)<<16|...`
- `long`:同 scheme,更多字节
- 表头:`ReadSize=行数 varint`

**Why:** 读客户端 POCO 缺省值会得到「字段默认值」而非「xlsx 真实值」(Luban 行类构造器是按 ByteBuf 读取的,POCO 字段无 default initializer),核同源要读真实值就得解 .bytes。

**How to apply:** 服务端 dev 想核「我建的 AuthoritativeDefs / GiftPoolSeeds 与客户端 Luban 表是否字节级同源」时,写一次性 console 探针:① 读 `<row class>.cs` 取字段顺序;② Read .bytes,按 varint 解出每行字段值;③ 与服务端权威值 diff。完事即扔(不进 repo)。
