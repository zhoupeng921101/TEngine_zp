# pipeline-ui 共享经验库索引

> pipeline-ui / pipeline-lite-ui 开工前手动 Read 本目录。每条经验一文件，本文件只放一行指针（标题 + 钩子），不放经验内容。

- [editor 截 UI 用 UICamera 离屏渲染](project-editor-ui-screenshot-via-camera.md) — Play 态自检截图走 UICamera→RenderTexture，别截 GameView（否则 CaptureScreenshot 越界 + AsTexture failed）
- [老式内联窗加节点重生成低风险](project-old-inline-window-add-node-regen.md) — 无 UIBindComponent 的老窗按节点名绑定，加唯一命名节点重生成不移位；含重生成/绑定/CS8795 stub/AABB 取景截图要点
