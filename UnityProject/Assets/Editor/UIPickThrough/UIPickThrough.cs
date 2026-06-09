using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace EditorTools.UIPickThrough
{
    /// <summary>
    /// Scene 视图里直接左键点击重叠的 UGUI Graphic：
    ///  - 第一次点：选中点击位置最上层那张
    ///  - 同一位置再点：往下钻一层，到底循环回顶层
    /// 性能：只在 MouseDown 那一帧做一次命中查询，平时（移动/重绘）零分配零查询。
    /// 通过菜单 Tools/UI Pick-Through 开关，关掉即恢复 Scene 默认点选行为。
    /// </summary>
    [InitializeOnLoad]
    public static class UIPickThrough
    {
        const string MenuPath = "Tools/UI Pick-Through 穿透选择";
        const string PrefKey = "EditorTools.UIPickThrough.Enabled";
        const float SamePointThreshold = 4f; // 判定“同一位置再点”的像素阈值

        static bool s_enabled;

        // 循环钻取状态
        static Vector2 s_lastMouse;
        static int s_lastIndex = -1;
        static GameObject s_lastPicked;

        // 点击/拖拽判定
        static bool s_armed;        // 已按下、尚未判定为拖拽
        static Vector2 s_downPos;   // 按下时的屏幕坐标
        const float DragSlop = 4f;  // 超过此像素位移视为拖拽

        // 复用容器，避免每次点击产生 GC
        static readonly List<Hit> s_hits = new List<Hit>(32);
        static readonly Vector3[] s_corners = new Vector3[4];

        struct Hit
        {
            public Graphic graphic;
            public long order; // 绘制顺序排序键，越大越靠上
        }

        static UIPickThrough()
        {
            s_enabled = EditorPrefs.GetBool(PrefKey, false);
            Hook(s_enabled);
        }

        static void Hook(bool on)
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            if (on) SceneView.duringSceneGui += OnSceneGUI;
        }

        [MenuItem(MenuPath)]
        static void Toggle()
        {
            s_enabled = !s_enabled;
            EditorPrefs.SetBool(PrefKey, s_enabled);
            Hook(s_enabled);
            SceneView.RepaintAll();
        }

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, s_enabled);
            return true;
        }

        static void OnSceneGUI(SceneView sv)
        {
            Event e = Event.current;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            switch (e.GetTypeForControl(controlID))
            {
                case EventType.MouseDown:
                    // 按下时只记录、不消费、不抢 hotControl，
                    // 让内置工具（Move/Rect 手柄）有机会接管拖拽。
                    if (e.button == 0 && !e.alt)
                    {
                        s_armed = true;
                        s_downPos = e.mousePosition;
                    }
                    break;

                case EventType.MouseDrag:
                    // 位移超过阈值，或有工具抢走了 hotControl —— 视为拖拽，放弃这次点击
                    if (s_armed &&
                        ((e.mousePosition - s_downPos).sqrMagnitude > DragSlop * DragSlop
                         || GUIUtility.hotControl != 0))
                    {
                        s_armed = false;
                    }
                    break;

                case EventType.MouseUp:
                    // 纯点击（没拖动、也没有工具占用 hotControl）才执行穿透选择
                    if (e.button == 0 && s_armed && GUIUtility.hotControl == 0 &&
                        (e.mousePosition - s_downPos).sqrMagnitude <= DragSlop * DragSlop)
                    {
                        Pick(e);
                        e.Use();
                    }
                    s_armed = false;
                    break;

                case EventType.Repaint:
                    DrawHighlight();
                    break;
            }
        }

        static void Pick(Event e)
        {
            BuildHits(e.mousePosition);

            if (s_hits.Count == 0)
            {
                Selection.activeGameObject = null;
                s_lastPicked = null;
                s_lastIndex = -1;
                s_lastMouse = e.mousePosition;
                return;
            }

            // 绘制顺序降序：index 0 = 最上层
            s_hits.Sort((a, b) => b.order.CompareTo(a.order));

            bool samePoint =
                (e.mousePosition - s_lastMouse).sqrMagnitude <= SamePointThreshold * SamePointThreshold;
            int lastListIndex = samePoint ? IndexOf(s_lastPicked) : -1;

            int pick = (samePoint && lastListIndex >= 0)
                ? (lastListIndex + 1) % s_hits.Count // 往下钻，循环回顶
                : 0;                                  // 最上层

            GameObject go = s_hits[pick].graphic.gameObject;
            Selection.activeGameObject = go;

            s_lastPicked = go;
            s_lastIndex = pick;
            s_lastMouse = e.mousePosition;
        }

        static int IndexOf(GameObject go)
        {
            if (go == null) return -1;
            for (int i = 0; i < s_hits.Count; i++)
                if (s_hits[i].graphic.gameObject == go) return i;
            return -1;
        }

        static void BuildHits(Vector2 guiPoint)
        {
            s_hits.Clear();
            Ray ray = HandleUtility.GUIPointToWorldRay(guiPoint);

            // 只在点击这一帧查询一次
            var graphics = Object.FindObjectsByType<Graphic>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic g = graphics[i];
                if (g == null || !g.isActiveAndEnabled) continue;

                Canvas c = g.canvas;
                if (c == null) continue; // 不在画布下的 Graphic 不参与

                g.rectTransform.GetWorldCorners(s_corners);
                if (!RayHitQuad(ray, s_corners)) continue;

                s_hits.Add(new Hit { graphic = g, order = DrawOrder(g, c) });
            }
        }

        // 绘制顺序键：高位 Canvas sortingOrder，低位 Graphic.depth（canvas 重建后的批次顺序）
        static long DrawOrder(Graphic g, Canvas c)
        {
            Canvas root = c.rootCanvas != null ? c.rootCanvas : c;
            long so = root.sortingOrder;
            long depth = g.depth + 1; // depth 可能为 -1，整体抬高避免负值干扰
            return (so << 32) + depth;
        }

        static bool RayHitQuad(Ray ray, Vector3[] q)
        {
            Plane plane = new Plane(q[0], q[1], q[2]);
            if (plane.normal.sqrMagnitude < 1e-12f) return false; // 退化 rect
            if (!plane.Raycast(ray, out float enter)) return false;

            Vector3 p = ray.GetPoint(enter);
            Vector3 n = plane.normal;
            // q 顺序: BL,TL,TR,BR —— 四条边叉乘同向即在内部
            return SameSide(q[0], q[1], p, n)
                && SameSide(q[1], q[2], p, n)
                && SameSide(q[2], q[3], p, n)
                && SameSide(q[3], q[0], p, n);
        }

        static bool SameSide(Vector3 a, Vector3 b, Vector3 p, Vector3 n)
        {
            return Vector3.Dot(Vector3.Cross(b - a, p - a), n) >= 0f;
        }

        static void DrawHighlight()
        {
            GameObject go = Selection.activeGameObject;
            if (go == null || go != s_lastPicked) return;

            var g = go.GetComponent<Graphic>();
            if (g == null) return;

            g.rectTransform.GetWorldCorners(s_corners);
            Handles.color = new Color(0.2f, 0.9f, 1f, 1f);
            Handles.DrawAAPolyLine(3f,
                s_corners[0], s_corners[1], s_corners[2], s_corners[3], s_corners[0]);

            if (s_lastIndex >= 0 && s_hits.Count > 0)
                Handles.Label(s_corners[1], $"  {s_lastIndex + 1}/{s_hits.Count}");
        }
    }
}
