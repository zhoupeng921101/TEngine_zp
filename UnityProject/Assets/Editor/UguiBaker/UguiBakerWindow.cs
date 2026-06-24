using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace EditorTools.Ugui
{
    /// <summary>
    /// 描述 JSON → UGUI 视觉占位节点树的烘焙窗口。
    /// 菜单：Tools/UI Architecture/UGUI Baker (JSON)。每节点产出文本或图片占位，控件人工转。
    /// </summary>
    public class UguiBakerWindow : EditorWindow
    {
        enum SourceMode { PasteJson, JsonFile }

        [SerializeField] Canvas _canvas;
        [SerializeField] SourceMode _mode = SourceMode.PasteJson;
        [SerializeField] string _json = "";
        [SerializeField] string _filePath = "";
        Vector2 _scroll;

        [MenuItem("UITools/UGUI Baker (JSON)")]
        static void Open()
        {
            var w = GetWindow<UguiBakerWindow>("UGUI Baker");
            w.minSize = new Vector2(380, 420);
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "把描述 JSON 粘进来，选目标 Canvas，点生成。\n" +
                "有文字的节点建文本(Text)，其余建图片(Image)占位；真按钮/滑条由你手动转。",
                MessageType.Info);

            _canvas = (Canvas)EditorGUILayout.ObjectField("目标 Canvas", _canvas, typeof(Canvas), true);
            if (_canvas == null)
                EditorGUILayout.HelpBox("未指定 Canvas：生成时会自动在场景里找一个，没有则新建。", MessageType.None);

            _mode = (SourceMode)EditorGUILayout.EnumPopup("来源", _mode);

            if (_mode == SourceMode.PasteJson)
            {
                EditorGUILayout.LabelField("JSON 文本");
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(180));
                _json = EditorGUILayout.TextArea(_json, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                _filePath = EditorGUILayout.TextField("JSON 文件", _filePath);
                if (GUILayout.Button("…", GUILayout.Width(28)))
                {
                    var p = EditorUtility.OpenFilePanel("选择 JSON", Application.dataPath, "json");
                    if (!string.IsNullOrEmpty(p)) _filePath = p;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(EditorApplication.isCompiling))
            {
                if (GUILayout.Button("执行烘焙生成", GUILayout.Height(36)))
                    DoBake();
            }
        }

        void DoBake()
        {
            string json = _json;
            if (_mode == SourceMode.JsonFile)
            {
                if (!System.IO.File.Exists(_filePath))
                {
                    EditorUtility.DisplayDialog("UGUI Baker", "JSON 文件不存在：\n" + _filePath, "OK");
                    return;
                }
                json = System.IO.File.ReadAllText(_filePath);
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                EditorUtility.DisplayDialog("UGUI Baker", "JSON 为空。", "OK");
                return;
            }

            Canvas canvas = _canvas != null ? _canvas : ResolveCanvas();
            try
            {
                var go = UguiBaker.Bake(json, canvas.transform as RectTransform);
                Selection.activeGameObject = go;
                EditorGUIUtility.PingObject(go);
                Debug.Log($"[UguiBaker] 生成完成：{go.name}", go);
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("UGUI Baker 失败", ex.Message, "OK");
                Debug.LogException(ex);
            }
        }

        static Canvas ResolveCanvas()
        {
            var c = Object.FindFirstObjectByType<Canvas>();
            if (c != null) return c;
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            }
            return canvas;
        }
    }
}
