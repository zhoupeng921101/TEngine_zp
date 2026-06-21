---
name: project-live-vs-residual-asset-judging
description: 判同名近似两份资源谁现行:代码 grep 区分单复数精确边界/prefab guid 反向查/不信 .meta nameFileIdTable;权威是 ISpriteEditorDataProvider.GetSpriteRects 或 LoadAllAssetRepresentationsAtPath。
metadata:
  type: project
---

判「工程内同名近似的两份资源谁现行谁残留」:

① 代码字符串引用 grep 时**区分单复数/精确边界**(`Sheet_setting\b` 排除 `Sheet_settings`);
② prefab/scene 的引用查 `.meta` 的 **guid** 反向 grep(非文件名);
③ 「谁是 live 子图」**不能信 .meta 的 `nameFileIdTable`**——它为 GUID 稳定保留历史 fileId 残名(如 `Sheet_settings_0..25`),与 live sprite rects 不是一回事;权威读法是 Unity 内 `ISpriteEditorDataProvider.GetSpriteRects()` 或 `LoadAllAssetRepresentationsAtPath` 数实际子精灵(execute_code 跑)。

现行=被运行期代码 const + prefab GUID 双引用的那张,残留=只被测试当锚读的那张,删残留时把测试锚 repoint 到现行表。

**Why:** nameFileIdTable 是 Unity 的 GUID 持久化机制,保留历史名是为了不破坏外部引用,但被误读为「live 子图列表」会导致删错文件;grep 字符串若不加边界,单复数同名会互相污染统计(2026-06,border-override)。

**How to apply:** 判同名资源现行/残留:① grep 用 `\b` 边界 + 区分单复数;② prefab 引用查 .meta 的 guid;③ live 子图用 `LoadAllAssetRepresentationsAtPath` 或 SpriteEditorDataProvider 实读,不读 nameFileIdTable;④ 删残留前同步把测试锚 repoint 到现行。
