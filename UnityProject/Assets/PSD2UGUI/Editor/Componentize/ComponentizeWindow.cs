using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TEngine.Editor.UI;   // 复用项目 UI 代码生成器的命名规则（ScriptGeneratorSetting）

namespace PSDUIImporter
{
    /// <summary>
    /// PSD2UGUI 组件化工具：选中节点 → 点组件按钮 → 给节点挂上对应 UGUI 组件。
    /// 控件(Button/Toggle/Slider…)会自动补一个 Image 作 targetGraphic。
    /// 不做角色指派——spriteState / fill·handle / content 等具体引用，在 Inspector 里直接连。
    /// 支持多选：对当前选中的所有节点批量挂。全程 Undo。
    /// </summary>
    public class ComponentizeWindow : EditorWindow
    {
        [MenuItem("QuickTool/PSD2UGUI 组件化")]
        public static void Open()
        {
            GetWindow<ComponentizeWindow>("PSD2UGUI 组件化").minSize = new Vector2(280, 300);
        }

        private Vector2 scroll;
        private bool _translate = true;

        private void OnSelectionChange() { Repaint(); }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.ObjectField("选中节点", Selection.activeGameObject, typeof(GameObject), true);

            GameObject[] sel = Selection.gameObjects;
            if (sel == null || sel.Length == 0)
            {
                EditorGUILayout.HelpBox("在 Hierarchy 选中一个或多个节点。", MessageType.Info);
                return;
            }
            if (sel.Length > 1) EditorGUILayout.LabelField("将作用于选中的 " + sel.Length + " 个节点");
            _translate = EditorGUILayout.ToggleLeft("中文基名翻译成英文（MyMemory，结果缓存可手改）", _translate);
            EditorGUILayout.Space();

            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("控件（自动补 Image 作 targetGraphic）", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (Btn("Button")) AddSelectable<Button>();
            if (Btn("Toggle")) AddSelectable<Toggle>();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (Btn("Slider")) AddSelectable<Slider>();
            if (Btn("Scrollbar")) AddSelectable<Scrollbar>();
            if (Btn("InputField")) AddSelectable<InputField>();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("布局 / 滚动", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (Btn("Grid")) AddPlain<GridLayoutGroup>();
            if (Btn("水平布局")) AddPlain<HorizontalLayoutGroup>();
            if (Btn("垂直布局")) AddPlain<VerticalLayoutGroup>();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (Btn("ScrollRect")) AddPlain<ScrollRect>();
            if (Btn("RectMask2D")) AddPlain<RectMask2D>();
            if (Btn("LayoutElement")) AddPlain<LayoutElement>();
            if (Btn("SizeFitter")) AddPlain<ContentSizeFitter>();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("图形", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (Btn("Image")) AddPlain<Image>();
            if (Btn("RawImage")) AddPlain<RawImage>();
            if (Btn("Text")) AddPlain<Text>();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("挂好组件后，在 Inspector 里连具体引用：Button 的 spriteState、Slider 的 fill/handle、ScrollRect 的 content 等。", MessageType.None);
        }

        private static bool Btn(string label) { return GUILayout.Button(label, GUILayout.Height(26)); }

        // Unity 组件类型 → 生成器的 UIComponentName 枚举
        private static UIComponentName? CompEnum(System.Type t)
        {
            switch (t.Name)
            {
                case "Button": return UIComponentName.Button;
                case "Toggle": return UIComponentName.Toggle;
                case "Slider": return UIComponentName.Slider;
                case "Scrollbar": return UIComponentName.Scrollbar;
                case "InputField": return UIComponentName.InputField;
                case "GridLayoutGroup": return UIComponentName.GridLayoutGroup;
                case "HorizontalLayoutGroup": return UIComponentName.HorizontalLayoutGroup;
                case "VerticalLayoutGroup": return UIComponentName.VerticalLayoutGroup;
                case "ScrollRect": return UIComponentName.ScrollRect;
                case "Image": return UIComponentName.Image;
                case "RawImage": return UIComponentName.RawImage;
                case "Text": return UIComponentName.Text;
                default: return null; // 不在生成器规则里（RectMask2D/LayoutElement/ContentSizeFitter 等），不改名
            }
        }

        // 从 ScriptGeneratorSetting 取该组件的命名前缀（uiElementRegex，如 m_btn）
        private static string PrefixFor(System.Type t)
        {
            UIComponentName? ce = CompEnum(t);
            var rules = ScriptGeneratorSetting.GetScriptGenerateRule();
            if (ce == null || rules == null) return null;
            ScriptGenerateRuler rule = rules.Find(r => r.componentName == ce.Value);
            return rule != null ? rule.uiElementRegex : null;
        }

        // 去掉 @* 标签 + 已有的(最长)规则前缀，得到干净基名
        private static string CleanBase(string name)
        {
            int at = name.IndexOf('@');
            if (at >= 0) name = name.Substring(0, at);   // 移除 @*
            name = name.Trim();
            var rules = ScriptGeneratorSetting.GetScriptGenerateRule();
            if (rules != null)
            {
                string best = null;
                foreach (ScriptGenerateRuler r in rules)
                    if (!string.IsNullOrEmpty(r.uiElementRegex) && name.StartsWith(r.uiElementRegex) &&
                        (best == null || r.uiElementRegex.Length > best.Length))
                        best = r.uiElementRegex;
                if (best != null) name = name.Substring(best.Length);
            }
            name = name.Trim('_');   // 去掉前缀与基名之间残留的分隔下划线，避免叠加
            return name.Length == 0 ? "Node" : name;
        }

        // 按生成器规则给节点改名：前缀(m_xxx) + "_" + 基名(可选中→英)，去 @*。组件不在规则里则不改名。
        private static void Rename(GameObject go, System.Type compType, bool translate)
        {
            string prefix = PrefixFor(compType);
            if (string.IsNullOrEmpty(prefix)) return;
            string baseName = CleanBase(go.name);
            if (translate) baseName = Translator.ToEnglish(baseName);
            string nn = prefix + "_" + baseName;
            if (nn != go.name) { Undo.RecordObject(go, "Rename"); go.name = nn; }
        }

        // 普通组件：直接挂（已有则跳过挂载，仍按规则重命名）
        private void AddPlain<T>() where T : Component
        {
            foreach (GameObject go in Selection.gameObjects)
            {
                Undo.IncrementCurrentGroup();
                int grp = Undo.GetCurrentGroup();
                bool had = go.GetComponent<T>() != null;
                if (!had) Undo.AddComponent<T>(go);
                Rename(go, typeof(T), _translate);
                Undo.CollapseUndoOperations(grp);
                Debug.Log("[组件化] " + (had ? "(已有)" : "+") + typeof(T).Name + " → " + go.name);
            }
        }

        // 控件：缺 Graphic 时补一个 Image 作 targetGraphic，再挂控件
        private void AddSelectable<T>() where T : Selectable
        {
            foreach (GameObject go in Selection.gameObjects)
            {
                Undo.IncrementCurrentGroup();
                int grp = Undo.GetCurrentGroup();

                Graphic g = go.GetComponent<Graphic>();
                if (g == null) g = Undo.AddComponent<Image>(go);

                T sel = go.GetComponent<T>();
                if (sel == null) sel = Undo.AddComponent<T>(go);
                Undo.RecordObject(sel, "targetGraphic");
                if (sel.targetGraphic == null) sel.targetGraphic = g;

                Rename(go, typeof(T), _translate);
                Undo.CollapseUndoOperations(grp);
                Debug.Log("[组件化] +" + typeof(T).Name + "（targetGraphic=" + g.GetType().Name + "）→ " + go.name);
            }
        }
    }
}
