---
name: project-config-xlsx-path-outside-unityproject
description: 配置表源 xlsx 在 repo/Configs/GameConfig/Datas(UnityProject 同级),bytes 产物在 UnityProject/Assets/AssetRaw/Configs/bytes
metadata:
  type: project
---

配置表源 xlsx 仓在 `<repo>/Configs/GameConfig/Datas/`(`UnityProject` 的同级兄弟目录,**不在** UnityProject 内);bytes 产物在 `UnityProject/Assets/AssetRaw/Configs/bytes/`。dev 交接区写 xlsx 路径常按 Configs 仓根相对,核「xlsx 是否存在」别在 UnityProject 内找,用绝对路径 `<repo>/Configs/...` 或 `git status` 看 `?? ../Configs/...`。

**Why:** 2026-06 mail 实测,误在 UnityProject 内找 xlsx 报「文件不存在」,实则在同级 Configs 仓;dev 写交接区路径常省略 ../。

**How to apply:** 核 xlsx 改动:①用绝对路径或 cd 到 repo 根 ②或 `git status` 看 `?? ../Configs/` 前缀;别盲信 dev 给的相对路径就当成 UnityProject-相对。
