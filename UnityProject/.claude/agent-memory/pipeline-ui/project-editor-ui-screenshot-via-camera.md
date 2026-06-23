---
name: project-editor-ui-screenshot-via-camera
description: Play 态自检/留证截 UI，用 UIRoot 的 UICamera 离屏渲染到固定尺寸 RenderTexture 再 ReadPixels；不要用 ScreenCapture/manage_scene screenshot 截 GameView（会报 "CaptureScreenshot 区域越界" + "CaptureScreenshotAsTexture failed before end of frame"）。
type: project
---

自检或留证截 UI 时，用 UIRoot 的 `UICamera` 离屏渲染到固定尺寸 `RenderTexture` 再 `ReadPixels`，不要用 `ScreenCapture.CaptureScreenshotAsTexture` 或 `manage_scene` 的 `screenshot` 去截 GameView。

**Why:** GameView 截图抓的是「当前激活的渲染目标（GameView backbuffer）」，其像素尺寸跟随 GameView 窗口实际大小（窗口被停靠成横条时是 1309×509 这类横向尺寸），不等于 Game 视图分辨率下拉框设定的逻辑分辨率。请求 1080×1920 而实际 backbuffer 是窗口物理像素时矩形越界，报 `CaptureScreenshot(L,B,W,H) requested a region that exceeds the active Render Target sized [...]`；且 `execute_code` 是同步调用、不在帧渲染结束（end of frame）时机，报 `CaptureScreenshotAsTexture() failed to generate texture!`。相机离屏渲染两点都绕开：输出尺寸由 `RenderTexture` 固定（与 GameView 窗口无关），`camera.Render()` 同步触发一次渲染（不依赖帧循环/帧尾）。本项目 UI 是 Screen Space - Camera（`UICanvas` 的 Canvas `m_RenderMode=1`，经 `UICamera` 渲染），相机离屏渲染能截到 UI；Screen Space - Overlay 不经相机，则截不到、只能截 GameView。

**How to apply:**
- 前提：在 Play 态执行。UIRoot 运行时实例化，编辑器非 Play 态场景里没有 `UICamera`。
- 截单个窗口时先只打开该窗口——`UICamera` 渲染其下所有可见 UI，多窗叠加会一起进图。
- `execute_code` 骨架：

```csharp
var cam = GameObject.Find("UICamera").GetComponent<Camera>();
// 兜底定位：System.Array.Find(Object.FindObjectsOfType<Canvas>(),
//   c => c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera != null).worldCamera
const int W = 1080, H = 1920;
var rt = new RenderTexture(W, H, 24);
var prevTarget = cam.targetTexture; var prevActive = RenderTexture.active;
cam.targetTexture = rt; cam.Render();
RenderTexture.active = rt;
var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
cam.targetTexture = prevTarget; RenderTexture.active = prevActive;
var path = System.IO.Path.Combine(Application.dataPath, "Screenshots/ui_check.png");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
Object.DestroyImmediate(tex); Object.DestroyImmediate(rt);
UnityEditor.AssetDatabase.Refresh();
```

- `UICamera` 背景透明（clear alpha=0），产出 PNG 背景透明；只看 UI 结构无妨。要白底自检可临时设 `cam.clearFlags=CameraClearFlags.SolidColor` + `cam.backgroundColor`，截完恢复。
- 成功判据：产出 PNG 尺寸 == 1080×1920 且字节数远大于空图，才算自检截图成功；否则按截图失败处理，不可把空图当「渲染正常」放过。

相关：[[project-bake-static-sprite-into-prefab-wysiwyg]]（同 UI 经验库；烤静态图进 prefab 与本条自检截图分属制作/验收两环节）。
