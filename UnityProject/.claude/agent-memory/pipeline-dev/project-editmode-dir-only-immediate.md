---
name: project-editmode-dir-only-immediate
description: EditMode 测里只建目录(空目录/无PNG)在同步方法块内可用;延迟只卡纹理导入,目录识别即时,V 组校验可运行时建目录+finally 删。
metadata:
  type: project
---

EditMode 测里只建**目录**(空目录/无PNG目录,做工具校验路径测试)在同步方法块内可用:`Directory.CreateDirectory`+`Refresh` 后 `AssetDatabase.IsValidFolder` 同步生效、`Directory.GetFiles` 也读得到——延迟只卡在「纹理资产导入」,目录识别即时。故 V 组校验类(树外/空/无PNG目录中止)可运行时建目录 + finally 删,不必走固定夹具。

**Why:** 与 PNG 夹具不同,空目录不涉及资产导入,Unity 即时识别;V 组测「工具中止行为」本不需要资产,固定夹具反而冗余(2026-06,ui-atlas-packer 返修)。

**How to apply:** 写 V 组校验测(无 PNG 路径/树外路径/空目录):① 运行时 `Directory.CreateDirectory` + `Refresh`;② 直接调工具断言中止;③ TearDown finally 删目录;④ 不为校验测建固定夹具。
