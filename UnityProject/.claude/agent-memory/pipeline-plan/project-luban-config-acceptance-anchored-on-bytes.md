---
name: project-luban-config-acceptance-anchored-on-bytes
description: Luban 配置表验收锚在直读 .bytes 的 EditMode 测试上,运行期 ConfigSystem 走 YooAsset 纯 C# 跑不通
metadata:
  type: project
---

Luban 配置表类设计:运行期 `ConfigSystem.Instance.Tables` 走 YooAsset + ModuleSystem,纯 C#/EditMode 跑不通;配置表验收点要锚在「`AssetDatabase.LoadAssetAtPath<TextAsset>(.../xxx.bytes)` → `new TbXxx(ByteBuf)`」直读二进制的 EditMode 测试上(绕 YooAsset,WeightCfgLubanTests 先例),纯逻辑(格式化/桥接)再单独给 InitForTest 注入路径。

Luban 行务必桥接成 POCO(业务侧只认 POCO,隔离生成类型,WeightCfgConfigMgr 先例)。导表工具链不可达列 BLOCKED 不判 FAIL。

源 xlsx 在仓库根 `Configs/GameConfig/Datas`(与 UnityProject 同级),schema 写数据 xlsx 表头四行(##var/##type/##group/##),planner 备注类字段设 group=e 不导出运行期。

**Why:** 2026-06 numeric-system 先例。

**How to apply:** Luban 表设计稿验收节:① EditMode 直读 .bytes 用例;② POCO 桥接测试;③ 工具链 BLOCKED 标识。schema 严格四行表头;planner 私用字段 group=e。
