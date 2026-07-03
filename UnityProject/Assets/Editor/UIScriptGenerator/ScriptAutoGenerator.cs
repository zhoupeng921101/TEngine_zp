using System;
using System.IO;
using System.Linq;
using System.Text;
using GameLogic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TEngine.Editor.UI
{
    public partial class ScriptGenerator
    {
        private static TextEditor m_textEditor = new TextEditor();
        private static string[] VARIABLE_NAME_REGEX;

        private static void CheckVariableNames()
        {
            var cnt = (int)UIFieldCodeStyle.Max;
            VARIABLE_NAME_REGEX = new string[cnt];

            for (int i = 0; i < cnt; i++)
            {
                VARIABLE_NAME_REGEX[i] = GetPrefixNameByCodeStyle((UIFieldCodeStyle)i);
            }
        }

        [MenuItem("GameObject/ScriptGenerator/UIPropertyBindComponent", priority = 84)]
        public static void UIPropertyBindComponent()
        {
            GenerateCSharpScript(false);
        }

        [MenuItem("GameObject/ScriptGenerator/UIPropertyBindComponent", true)]
        public static bool ValidateUIPropertyBindComponent()
        {
            return ScriptGeneratorSetting.Instance.UseBindComponent;
        }

        [MenuItem("GameObject/ScriptGenerator/UIPropertyBindComponent - UniTask", priority = 85)]
        public static void UIPropertyBindComponentUniTask()
        {
            GenerateCSharpScript(false, true);
        }

        [MenuItem("GameObject/ScriptGenerator/UIPropertyBindComponent - UniTask", true)]
        public static bool ValidateUIPropertyBindComponentUniTask()
        {
            return ScriptGeneratorSetting.Instance.UseBindComponent;
        }

        [MenuItem("GameObject/ScriptGenerator/UIPropertyAndListenerBindComponent", priority = 86)]
        public static void UIPropertyAndListenerBindComponent()
        {
            GenerateCSharpScript(true);
        }

        [MenuItem("GameObject/ScriptGenerator/UIPropertyAndListenerBindComponent", true)]
        public static bool ValidateUIPropertyAndListenerBindComponent()
        {
            return ScriptGeneratorSetting.Instance.UseBindComponent;
        }

        [MenuItem("GameObject/ScriptGenerator/UIPropertyAndListenerBindComponentUniTask - UniTask", priority = 87)]
        public static void UIPropertyAndListenerBindComponentUniTask()
        {
            GenerateCSharpScript(true, true);
        }

        [MenuItem("GameObject/ScriptGenerator/UIPropertyAndListenerBindComponentUniTask - UniTask", true)]
        public static bool ValidateUIPropertyAndListenerBindComponentUniTask()
        {
            return ScriptGeneratorSetting.Instance.UseBindComponent;
        }

        private static string GetUITypeName(string uiGenTypeName, string fileName)
        {
            var uiGenType = ScriptGeneratorSetting.GetUIGenType(uiGenTypeName);

            if (uiGenType == null)
            {
                return "UIPanelMono";
            }
            return !uiGenType.isGeneric ? uiGenType.uiTypeName : $"{uiGenType.uiTypeName}<{fileName}>";
        }

        public static bool GenerateCSharpScript(bool includeListener, bool isUniTask = false,
            bool isAutoGenerate = false, string savePath = null, string className = null,
            string uiGenTypeName = null, bool isGenImp = false,
            string impSavePath = null)
        {
            return GenerateCSharpScript(Selection.activeTransform, includeListener, isUniTask,
                isAutoGenerate, savePath, className, uiGenTypeName, isGenImp, impSavePath);
        }

        /// <summary>
        /// 显式传入遍历根的生成入口，不依赖全局 <see cref="Selection"/>。
        /// 自定义 Inspector 按钮按被检视组件的 transform 调用，菜单项仍走 Selection。
        /// </summary>
        public static bool GenerateCSharpScript(Transform root, bool includeListener, bool isUniTask = false,
            bool isAutoGenerate = false, string savePath = null, string className = null,
            string uiGenTypeName = null, bool isGenImp = false,
            string impSavePath = null)
        {
            if (root == null)
            {
                return false;
            }
            CheckVariableNames();
            StringBuilder strVar = new StringBuilder();
            StringBuilder strBind = new StringBuilder();
            StringBuilder strOnCreate = new StringBuilder();
            StringBuilder strCallback = new StringBuilder();

            var widgetPrefix = GetUIWidgetName();
            string fileName = $"{root.name}.cs";
            if (!string.IsNullOrEmpty(className))
            {
                fileName = $"{className}.cs";
            }
            string uiTypeName = GetUITypeName(uiGenTypeName, className);
            if (!isAutoGenerate)
            {
                uiTypeName = "UIPanelMono";
                if (root.name.StartsWith(widgetPrefix))
                {
                    uiTypeName = "UIWidgetMono";
                    fileName = $"{root.name.Replace(GetUIWidgetName(), string.Empty)}.cs";
                }
            }

            // 窗口体系统一为 MonoBehaviour（UIPanelMono / UIWidgetMono）：引用走 [SerializeField] 具名拖拽，
            // 字段生成 [SerializeField] private T m_x; ScriptGenerator() 只挂事件，不取 index。
            AutoErgodic(root, root, ref strVar, ref strBind, ref strOnCreate, ref strCallback, isUniTask, isMono: true);
            StringBuilder strFile = new StringBuilder();

            if (includeListener)
            {
                strFile.AppendLine("//----------------------------------------------------------");
                strFile.AppendLine("// <auto-generated>");
                strFile.AppendLine("// -This code was generated.");
                strFile.AppendLine("// -Changes to this file may cause incorrect behavior.");
                strFile.AppendLine("// -will be lost if the code is regenerated.");
                strFile.AppendLine("// <auto-generated/>");
                strFile.AppendLine("//----------------------------------------------------------");
#if TextMeshPro
                strFile.AppendLine("using TMPro;");
#endif
                if (isUniTask)
                {
                    strFile.AppendLine("using Cysharp.Threading.Tasks;");
                }

                strFile.AppendLine("using UnityEngine;");
                strFile.AppendLine("using UnityEngine.UI;");
                strFile.AppendLine("using TEngine;");
                strFile.AppendLine();
                strFile.AppendLine($"namespace {ScriptGeneratorSetting.GetUINameSpace()}");
                strFile.AppendLine("{");
                {
                    if (!isAutoGenerate)
                    {
                        if (root.name.StartsWith(widgetPrefix))
                        {
                            strFile.AppendLine($"\tpublic partial class {fileName.Replace(".cs", "")} : {uiTypeName}");
                        }
                        else
                        {
                            strFile.AppendLine($"\t[Window(UILayer.UI, location : \"{fileName.Replace(".cs", "")}\")]");
                            strFile.AppendLine($"\tpublic partial class {fileName.Replace(".cs", "")} : {uiTypeName}");
                        }
                    }
                    else
                    {
                        //if (string.Equals(uiTypeName, "UIWindow", StringComparison.Ordinal))
                        //{
                        //    strFile.AppendLine($"\t[Window(UILayer.UI, location : \"{fileName.Replace(".cs", "")}\")]");
                        //}

                        strFile.AppendLine($"\tpublic partial class {fileName.Replace(".cs", "")} : {uiTypeName}");
                    }

                    strFile.AppendLine("\t{");
                }
            }

            // 脚本工具生成的代码
            strFile.AppendLine($"\t\t#region 脚本工具生成的代码");
            strFile.AppendLine();
            strFile.AppendLine(strVar.ToString());
            strFile.AppendLine("\t\tprotected override void ScriptGenerator()");
            strFile.AppendLine("\t\t{");
            {
                strFile.Append(strBind.ToString());
                strFile.Append(strOnCreate.ToString());
            }
            strFile.AppendLine("\t\t}");
            strFile.AppendLine();
            strFile.Append($"\t\t#endregion");
            strFile.AppendLine();

            if (includeListener)
            {
                strFile.AppendLine();
                strFile.AppendLine("\t\t#region 事件");
                strFile.AppendLine();
                strFile.Append(strCallback.ToString());
                strFile.AppendLine($"\t\t#endregion");
                strFile.AppendLine("\t}");
                strFile.AppendLine("}");
            }

            m_textEditor.Delete();
            m_textEditor.text = strFile.ToString();
            m_textEditor.SelectAll();
            m_textEditor.Copy();

            if (isAutoGenerate)
            {
                string path = savePath?.Replace("\\", "/");
                if (string.IsNullOrEmpty(path))
                {
                    return false;
                }
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                var saveFileName = fileName.Replace(".cs", "_Gen.g.cs");
                var filePath = Path.Combine(path, saveFileName).Replace("\\", "/");

                if (File.Exists(filePath))
                {
                    FileAttributes attributes = File.GetAttributes(filePath);
                    if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
                    }
                    File.Delete(filePath);
                    AssetDatabase.Refresh();
                }

                File.WriteAllText(filePath, strFile.ToString(), Encoding.UTF8);
                File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.ReadOnly);
                if (isGenImp)
                {
                    GenerateImpCSharpScript(isUniTask, fileName, impSavePath, uiTypeName);
                }
                AssetDatabase.Refresh();
            }
            else
            {
                Debug.Log($"<color=#1E90FF>脚本已生成到剪贴板，请自行Ctl+V粘贴</color>");
            }

            return true;
        }

        public static void AutoErgodic(Transform root, Transform transform, ref StringBuilder strVar,
            ref StringBuilder strBind, ref StringBuilder strOnCreate, ref StringBuilder strCallback, bool isUniTask, bool isMono = true)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                WriteAutoScript(root, child, ref strVar, ref strBind, ref strOnCreate, ref strCallback, isUniTask, isMono);
                // 跳过 "m_item"
                if (child.name.StartsWith(GetUIWidgetGameObjectName()))
                {
                    continue;
                }

                AutoErgodic(root, child, ref strVar, ref strBind, ref strOnCreate, ref strCallback, isUniTask, isMono);
            }
        }

        private static void WriteAutoScript(Transform root, Transform child, ref StringBuilder strVar,
            ref StringBuilder strBind, ref StringBuilder strOnCreate, ref StringBuilder strCallback, bool isUniTask, bool isMono = true)
        {
            string varName = child.name;
            // 查找相关的规则定义
            var rule = ScriptGeneratorSetting.GetScriptGenerateRule()
                .Find(r => varName.StartsWith(r.uiElementRegex));

            if (rule == null)
            {
                return;
            }

            var componentName = rule.componentName.ToString();

            if (string.IsNullOrEmpty(componentName))
            {
                return;
            }

            varName = GetVariableName(varName);

            if (string.IsNullOrEmpty(varName))
            {
                return;
            }

            // 字段 [SerializeField] 暴露到 Inspector，引用靠具名拖拽就位。
            strVar.AppendLine($"\t\t[SerializeField] private {componentName} {varName}{(ScriptGeneratorSetting.Instance.NullableEnable ? " = null!;" : ";")}");

            switch (rule.componentName)
            {
                case UIComponentName.Button:
                    var btnFuncName = GetBtnFuncName(varName);

                    if (isUniTask)
                    {
                        strOnCreate.AppendLine($"\t\t\t{varName}.onClick.RemoveAllListeners();");
                        strOnCreate.AppendLine($"\t\t\t{varName}.onClick.AddListener(UniTask.UnityAction({btnFuncName}));");
                        strCallback.AppendLine($"\t\tprivate partial UniTaskVoid {btnFuncName}();");
                    }
                    else
                    {
                        strOnCreate.AppendLine($"\t\t\t{varName}.onClick.RemoveAllListeners();");
                        strOnCreate.AppendLine($"\t\t\t{varName}.onClick.AddListener({btnFuncName});");
                        strCallback.AppendLine($"\t\tprivate partial void {btnFuncName}();");
                    }

                    strCallback.AppendLine();
                    break;

                case UIComponentName.Toggle:
                    var toggleFuncName = GetToggleFuncName(varName);
                    strOnCreate.AppendLine($"\t\t\t{varName}.onValueChanged.RemoveAllListeners();");
                    strOnCreate.AppendLine($"\t\t\t{varName}.onValueChanged.AddListener({toggleFuncName});");
                    strCallback.AppendLine($"\t\tprivate partial void {toggleFuncName}(bool isOn);");
                    strCallback.AppendLine();
                    break;

                case UIComponentName.TMP_Dropdown:
                    var tmpDropdownFuncName = GetTMPDropdownFuncName(varName);
                    strOnCreate.Append($"\t\t\t{varName}.onValueChanged.RemoveAllListeners();\n");
                    strOnCreate.Append($"\t\t\t{varName}.onValueChanged.AddListener({tmpDropdownFuncName});\n");
                    strCallback.Append($"\t\tprivate partial void {tmpDropdownFuncName}(int selectedIndex);\n");
                    strCallback.AppendLine();
                    break;

                case UIComponentName.Slider:
                    var sliderFuncName = GetSliderFuncName(varName);
                    strOnCreate.AppendLine($"\t\t\t{varName}.onValueChanged.RemoveAllListeners();");
                    strOnCreate.AppendLine($"\t\t\t{varName}.onValueChanged.AddListener({sliderFuncName});");
                    strCallback.AppendLine($"\t\tprivate partial void {sliderFuncName}(float value);");
                    strCallback.AppendLine();
                    break;
            }
        }

        #region GenerateImpCSharp

        private static bool GenerateImpCSharpScript(bool isUniTask = false, string fileName = null, string impSavePath = null, string uiTypeName = null)
        {
            var root = Selection.activeTransform;
            if (root == null || string.IsNullOrEmpty(fileName))
            {
                return false;
            }
            CheckVariableNames();
            StringBuilder strCallback = new StringBuilder();

            AutoImpErgodic(root, root, ref strCallback, isUniTask);
            StringBuilder strFile = new StringBuilder();

#if TextMeshPro
            strFile.AppendLine("using TMPro;");
#endif
            if (isUniTask)
            {
                strFile.AppendLine("using Cysharp.Threading.Tasks;");
            }

            strFile.AppendLine("using UnityEngine;");
            strFile.AppendLine("using UnityEngine.UI;");
            strFile.AppendLine("using TEngine;");
            strFile.AppendLine();
            strFile.AppendLine($"namespace {ScriptGeneratorSetting.GetUINameSpace()}");
            strFile.AppendLine("{");
            {
                if (string.Equals(uiTypeName, "UIWindow", StringComparison.Ordinal)
                    || string.Equals(uiTypeName, "UIPanelMono", StringComparison.Ordinal))
                {
                    strFile.AppendLine($"\t[Window(UILayer.UI, location : \"{fileName.Replace(".cs", "")}\")]");
                }
                strFile.AppendLine($"\tpublic partial class {fileName.Replace(".cs", "")}");
                strFile.AppendLine("\t{");
                {
                    strFile.AppendLine("\t\t#region 事件");
                    strFile.AppendLine();
                    strFile.Append(strCallback.ToString());
                    strFile.AppendLine($"\t\t#endregion");
                }
                strFile.AppendLine("\t}");
            }
            strFile.AppendLine("}");

            m_textEditor.Delete();
            m_textEditor.text = strFile.ToString();
            m_textEditor.SelectAll();
            m_textEditor.Copy();

            string path = impSavePath?.Replace("\\", "/");
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            var filePath = Path.Combine(path, fileName).Replace("\\", "/");

            if (File.Exists(filePath))
            {
                Debug.LogWarning("相关实现类脚本已生成，再次生成跳过");
                return false;
            }

            File.WriteAllText(filePath, strFile.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            return true;
        }

        public static void AutoImpErgodic(Transform root, Transform transform, ref StringBuilder strCallback, bool isUniTask)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                WriteAutoImpScript(root, child, ref strCallback, isUniTask);
                // 跳过 "m_item"
                if (child.name.StartsWith(GetUIWidgetGameObjectName()))
                {
                    continue;
                }

                AutoImpErgodic(root, child, ref strCallback, isUniTask);
            }
        }

        private static void WriteAutoImpScript(Transform root, Transform child, ref StringBuilder strCallback, bool isUniTask)
        {
            string varName = child.name;
            // 查找相关的规则定义
            var rule = ScriptGeneratorSetting.GetScriptGenerateRule()
                .Find(r => varName.StartsWith(r.uiElementRegex));

            if (rule == null)
            {
                return;
            }

            var componentName = rule.componentName.ToString();

            if (string.IsNullOrEmpty(componentName))
            {
                return;
            }

            varName = GetVariableName(varName);

            if (string.IsNullOrEmpty(varName))
            {
                return;
            }

            switch (rule.componentName)
            {
                case UIComponentName.Button:
                    var btnFuncName = GetBtnFuncName(varName);

                    if (isUniTask)
                    {
                        strCallback.AppendLine($"\t\tprivate async partial UniTaskVoid {btnFuncName}()");
                        strCallback.AppendLine("\t\t{");
                        strCallback.AppendLine("\t\t\tawait UniTask.Yield();");
                        strCallback.AppendLine("\t\t}");
                    }
                    else
                    {
                        strCallback.AppendLine($"\t\tprivate partial void {btnFuncName}()");
                        strCallback.AppendLine("\t\t{");
                        strCallback.AppendLine("\t\t}");
                    }

                    strCallback.AppendLine();
                    break;

                case UIComponentName.Toggle:
                    var toggleFuncName = GetToggleFuncName(varName);
                    strCallback.AppendLine($"\t\tprivate partial void {toggleFuncName}(bool isOn)");
                    strCallback.AppendLine("\t\t{");
                    strCallback.AppendLine("\t\t}");
                    strCallback.AppendLine();
                    break;

                case UIComponentName.TMP_Dropdown:
                    var tmpDropdownFuncName = GetTMPDropdownFuncName(varName);
                    strCallback.AppendLine($"\t\tprivate partial void {tmpDropdownFuncName}(int selectedIndex)");
                    strCallback.AppendLine("\t\t{");
                    strCallback.AppendLine("\t\t}");
                    strCallback.AppendLine();
                    break;

                case UIComponentName.Slider:
                    var sliderFuncName = GetSliderFuncName(varName);
                    strCallback.AppendLine($"\t\tprivate partial void {sliderFuncName}(float value)");
                    strCallback.AppendLine("\t\t{");
                    strCallback.AppendLine("\t\t}");
                    strCallback.AppendLine();
                    break;
            }
        }

        #endregion


        private static string GetPrefixNameByCodeStyle(UIFieldCodeStyle style)
        {
            return ScriptGeneratorSetting.GetPrefixNameByCodeStyle(style);
        }

        /// <summary>
        /// 在 <paramref name="root"/> 子树内按节点名精确匹配查找后代 Transform，遇 widget 边界
        /// （名字以 <see cref="GetUIWidgetGameObjectName"/> 起头的子节点）即停止下钻，与 <see cref="AutoErgodic"/>
        /// 的遍历边界同规则。找不到返回 null。
        /// </summary>
        public static Transform FindNodeByNameWithWidgetBoundary(Transform root, string nodeName)
        {
            if (root == null || string.IsNullOrEmpty(nodeName))
            {
                return null;
            }

            var widgetBoundary = GetUIWidgetGameObjectName();
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (string.Equals(child.name, nodeName, StringComparison.Ordinal))
                {
                    return child;
                }

                // 命中 widget 边界则不再下钻该子树（与 AutoErgodic 一致）。
                if (child.name.StartsWith(widgetBoundary))
                {
                    continue;
                }

                var found = FindNodeByNameWithWidgetBoundary(child, nodeName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static string GetUIWidgetGameObjectName()
        {
            foreach (var rule in ScriptGeneratorSetting.Instance.ScriptGenerateRule.Where(rule => rule.isUIWidget))
            {
                return rule.uiElementRegex;
            }
            // 生成规则里没有有勾选是否Widget时，保底
            return GetUIWidgetName();
        }

        private static string GetUIWidgetName()
        {
            return GetComponentName(ScriptGeneratorSetting.GetWidgetName());
        }

        private static string GetComponentName(string componentName)
        {
            return GetPrefixName() + componentName;
        }

        private static string GetPrefixName()
        {
            return ScriptGeneratorSetting.GetPrefixNameByCodeStyle(ScriptGeneratorSetting.Instance.CodeStyle);
        }

        private static string GetVariableName(string varName)
        {
            if (string.IsNullOrEmpty(varName))
            {
                return varName;
            }

            foreach (var prefix in VARIABLE_NAME_REGEX)
            {
                if (varName.StartsWith(prefix))
                {
                    varName = varName[prefix.Length..];
                    varName = GetComponentName(varName);
                    break;
                }
            }
            return varName;
        }
    }
}