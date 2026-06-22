---
name: project-drag-gain-offset-breaks-aim
description: BlockBlast 拖拽落子「空位也弹回」根因=DragGain≠1 放大位移 + DragFingerOffsetY 抬升叠加，落点远离手指；运行期模拟 PointerDown/Drag/Up 实测定位，gain=1.0 根治。
metadata:
  type: project
---

BlockBlast（`MergeOrderWindow` / 退役 `GameWindow` 共用 `BlockPieceDragger`）拖候选块「明显空位也弹回」的根因是 `BlockLayout.DragGain`（位移增益）≠1 与 `DragFingerOffsetY`（块浮在手指上方）叠加：块的实际落点 = 起点 + 上方 offset + 触控位移×gain，离手指越远（越靠棋盘上方）gain 放大越狠，块飞出棋盘 → `inBounds=False` 静默回弹。`gain=1.0` 让拖拽 1:1 跟手、所见即所落是根治；offset 可保留（块浮在指尖上方避遮挡，块本身即视觉真相，落点判定读的就是块的 anchoredPosition，与块视觉重合）。

**Why:** 落点链路 `container.anchoredPosition → ComputeGridPos` 数学逐行自洽，纯读码定不出 off-by；off-by 来自手势层把「手指位置」经 offset+gain 变换成「块位置」，gain≠1 破坏 1:1 映射且随距离非线性恶化。读码穷尽时必须进 play mode 拿运行期数据。

**How to apply:** ① 拖拽类交互 bug 在 play mode 用 `execute_code` 模拟真实手势：构造 `PointerEventData{position,pressPosition}`，按序 invoke `IPointerDownHandler.OnPointerDown / IDragHandler.OnDrag / IPointerUpHandler.OnPointerUp`，屏幕点用 `slotLayer.TransformPoint(local)→WorldToScreenPoint(cam)` 从设计坐标反算（ScreenSpaceCamera 用 `canvas.worldCamera`）。② 拿活窗口实例：`FindObjectsOfType(draggerType)` 取槽 GO → 经 `BlockPieceDragger.OnEnd` 委托的 `.Target` 即 UIWindow 实例（UIWindow 非 MonoBehaviour，FindObjectsOfType 找不到）。③ 验证落点 = 块视觉格而非手指格：成功落子后块落在「块中心对准的格」，故 play 验证要把块（=手指+offset）对准目标格，不是手指对准。④ 反馈缺失常配套：`GameConfigBB.ShowInvalidGhost=false` 会让无效落点连红 ghost 都不画，开为 true 补「红 footprint = 放不下」可见性；落子失败用 `BurstText.Spawn`（瞬时弹字，与窗内既有落子反馈同口径）按门槛分因（体力/越界/占用），别占用 clear-tool 的常驻提示条 `_clearToolHintBg`。
