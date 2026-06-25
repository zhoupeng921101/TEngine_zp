using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TEngine.Editor.UI
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "TEngine/ScriptGeneratorSetting", fileName = "ScriptGeneratorSetting")]
    public class ScriptGeneratorSetting : ScriptableObject
    {
        private static ScriptGeneratorSetting _instance;

        public static ScriptGeneratorSetting Instance
        {
            get
            {
                if (_instance == null)
                {
                    string[] guids = AssetDatabase.FindAssets("t:ScriptGeneratorSetting");
                    if (guids.Length >= 1)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        _instance = AssetDatabase.LoadAssetAtPath<ScriptGeneratorSetting>(path);
                    }
                }
                return _instance;
            }
        }

        [SerializeField] private bool useBindComponent;

        // [FolderPath]
        // [LabelText("默认组件代码保存路径")]
        [SerializeField] private string genCodePath = "Assets/GameScripts/HotFix/GameLogic/UI/Gen";
        [SerializeField] private string impCodePath = "Assets/GameScripts/HotFix/GameLogic/UI";

        // [LabelText("绑定代码命名空间")]
        [SerializeField]
        private string _namespace = "GameLogic";

        // [LabelText("子组件名称(不会往下继续遍历)")]
        [SerializeField]
        private string _widgetName = "item";

        // 烘焙器(UguiBaker)建文本节点时使用的默认字体。开箱未设时回退到工程内 GBK.ttf。
        [SerializeField]
        private Font defaultUIFont;

        public bool UseBindComponent => useBindComponent;

        public string GenCodePath => genCodePath;
        public string ImpCodePath => impCodePath;

        public string Namespace => _namespace;

        public string WidgetName => _widgetName;

        public UIFieldCodeStyle CodeStyle = UIFieldCodeStyle.UnderscorePrefix;

        [SerializeField]
        private List<UIGenType> uiGenTypes = new List<UIGenType>()
        {
            new UIGenType("UIWindowMono", false),
            new UIGenType("UIWidgetMono", false),
        };
        public List<UIGenType> UIGenTypes => uiGenTypes;

        public bool NullableEnable;

        [SerializeField]
        private List<ScriptGenerateRuler> scriptGenerateRule = new List<ScriptGenerateRuler>()
        {
            new ScriptGenerateRuler("m_go", UIComponentName.GameObject),
            new ScriptGenerateRuler("m_item", UIComponentName.GameObject),
            new ScriptGenerateRuler("m_tf", UIComponentName.Transform),
            new ScriptGenerateRuler("m_rect", UIComponentName.RectTransform),
            new ScriptGenerateRuler("m_text", UIComponentName.Text),
            new ScriptGenerateRuler("m_richText", UIComponentName.RichTextItem),
            new ScriptGenerateRuler("m_btn", UIComponentName.Button),
            new ScriptGenerateRuler("m_img", UIComponentName.Image),
            new ScriptGenerateRuler("m_rimg", UIComponentName.RawImage),
            new ScriptGenerateRuler("m_scrollBar", UIComponentName.Scrollbar),
            new ScriptGenerateRuler("m_scroll", UIComponentName.ScrollRect),
            new ScriptGenerateRuler("m_input", UIComponentName.InputField),
            new ScriptGenerateRuler("m_grid", UIComponentName.GridLayoutGroup),
            new ScriptGenerateRuler("m_hlay", UIComponentName.HorizontalLayoutGroup),
            new ScriptGenerateRuler("m_vlay", UIComponentName.VerticalLayoutGroup),
            new ScriptGenerateRuler("m_slider", UIComponentName.Slider),
            new ScriptGenerateRuler("m_group", UIComponentName.ToggleGroup),
            new ScriptGenerateRuler("m_curve", UIComponentName.AnimationCurve),
            new ScriptGenerateRuler("m_canvasGroup", UIComponentName.CanvasGroup),
            new ScriptGenerateRuler("m_toggle",UIComponentName.Toggle),
#if TextMeshPro
            new ScriptGenerateRuler("m_tmpInput", UIComponentName.TMP_InputField),
            new ScriptGenerateRuler("m_tmpDropdown", UIComponentName.TMP_Dropdown),
            new ScriptGenerateRuler("m_tmp",UIComponentName.TextMeshProUGUI),
#endif
        };

        public List<ScriptGenerateRuler> ScriptGenerateRule => scriptGenerateRule;


        [MenuItem("TEngine/Create ScriptGeneratorSetting")]
        private static void CreateAutoBindGlobalSetting()
        {
            string[] paths = AssetDatabase.FindAssets("t:ScriptGeneratorSetting");
            if (paths.Length >= 1)
            {
                string path = AssetDatabase.GUIDToAssetPath(paths[0]);
                EditorUtility.DisplayDialog("警告", $"已存在ScriptGeneratorSetting，路径:{path}", "确认");
                return;
            }

            ScriptGeneratorSetting setting = CreateInstance<ScriptGeneratorSetting>();
            AssetDatabase.CreateAsset(setting, "Assets/Editor/ScriptGeneratorSetting.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static List<ScriptGenerateRuler> GetScriptGenerateRule()
        {
            if (Instance == null)
            {
                return null;
            }
            return Instance.ScriptGenerateRule;
        }

        public static string GetUINameSpace()
        {
            if (Instance == null)
            {
                return string.Empty;
            }

            return Instance.Namespace;
        }

        public static UIFieldCodeStyle GetCodeStyle()
        {
            if (Instance == null)
            {
                return UIFieldCodeStyle.UnderscorePrefix;
            }

            return Instance.CodeStyle;
        }

        public static string GetGenCodePath() => Instance?.GenCodePath;
        public static string GetImpCodePath() => Instance?.ImpCodePath;

        // 工程内方正中文字体；运行时 UI 也用它配合 OS 回退渲染中文。
        private const string GBKFontPath = "Assets/AssetRaw/Fonts/GBK.ttf";

        /// <summary>
        /// 烘焙器建文本节点用的默认字体。回退链：配置字体 → GBK.ttf → 内置 LegacyRuntime.ttf。
        /// 默认指向 GBK.ttf，不依赖 OS 字体回退。
        /// </summary>
        public static Font GetDefaultUIFont()
        {
            var configured = Instance != null ? Instance.defaultUIFont : null;
            if (configured != null) return configured;

            var gbk = AssetDatabase.LoadAssetAtPath<Font>(GBKFontPath);
            if (gbk != null) return gbk;

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static string GetWidgetName()
        {
            if (Instance == null)
            {
                return string.Empty;
            }

            return Instance.WidgetName;
        }

        public static string GetPrefixNameByCodeStyle(UIFieldCodeStyle style)
        {
            return style switch
            {
                UIFieldCodeStyle.UnderscorePrefix => "_",
                UIFieldCodeStyle.MPrefix => "m_",
                _ => "m_"
            };
        }

        public static string GetUIComponentWithoutPrefixName(UIComponentName uiComponentName)
        {
            if (Instance.ScriptGenerateRule == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < Instance.ScriptGenerateRule.Count; i++)
            {
                var rule = Instance.ScriptGenerateRule[i];

                if (rule.componentName == uiComponentName)
                {
                    return rule.uiElementRegex.Substring(rule.uiElementRegex.IndexOf("_", StringComparison.Ordinal) + 1);
                }
            }
            return string.Empty;
        }

        public static UIGenType GetUIGenType(string uiGenTypeName)
        {
            if (string.IsNullOrEmpty(uiGenTypeName))
            {
                return null;
            }
            var tempList = Instance.UIGenTypes;
            for (int i = 0; i < tempList.Count; i++)
            {
                var uiGenType = tempList[i];

                if (string.Equals(uiGenTypeName, uiGenType.uiTypeName, StringComparison.Ordinal))
                {
                    return uiGenType;
                }
            }
            return null;
        }
    }
}