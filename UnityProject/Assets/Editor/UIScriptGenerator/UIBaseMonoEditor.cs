#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Reflection;
using GameLogic;
using TEngine;
using TEngine.Editor.UI;
using UnityEditor;
using UnityEngine;

namespace GameLogic.Editor
{
    /// <summary>
    /// UIBaseMono 派生组件（窗口 / widget）的自定义 Inspector。
    /// 在默认 Inspector 之上提供两项编辑期便利：
    /// 一键重新生成绑定代码 <c>_Gen.g.cs</c>，以及按节点名把 prefab 子节点引用自动绑回 <c>[SerializeField]</c> 字段。
    /// 字段名与节点名在 MPrefix 风格下恒等，绑定按此约定逐字段精确匹配。
    /// </summary>
    [CustomEditor(typeof(UIBaseMono), editorForChildClasses: true)]
    public class UIBaseMonoEditor : UnityEditor.Editor
    {
        private bool _useUniTask;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("HelpBox");
            {
                _useUniTask = EditorGUILayout.Toggle("生成 UniTask 版本", _useUniTask);

                // 多选时无法把引用绑定 / 代码生成对应到单一组件，按钮置灰。
                using (new EditorGUI.DisabledScope(targets.Length != 1))
                {
                    if (GUILayout.Button("重新生成 _Gen.g.cs", GUILayout.Height(28)))
                    {
                        RegenerateGenCode();
                    }

                    if (GUILayout.Button("按节点名自动绑定 [SerializeField]", GUILayout.Height(28)))
                    {
                        BindSerializedFieldsByNodeName();
                    }
                }
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 以被检视组件的 transform 为遍历根重新生成 <c>_Gen.g.cs</c>。
        /// 仅重写绑定代码，不生成实现类（isGenImp=false），不覆盖用户逻辑。
        /// </summary>
        private void RegenerateGenCode()
        {
            var component = (UIBaseMono)target;
            string className = component.GetType().Name;
            string uiGenTypeName = component is UIPanelMono ? "UIPanelMono" : "UIWidgetMono";
            string savePath = ScriptGeneratorSetting.GetGenCodePath();

            bool ok = ScriptGenerator.GenerateCSharpScript(
                root: component.transform,
                includeListener: true,
                isUniTask: _useUniTask,
                isAutoGenerate: true,
                savePath: savePath,
                className: className,
                uiGenTypeName: uiGenTypeName,
                isGenImp: false,
                impSavePath: null);

            if (ok)
            {
                Log.Info($"[UIBaseMonoEditor] 已重新生成 {className}_Gen.g.cs（{savePath}）。");
            }
            else
            {
                Log.Warning($"[UIBaseMonoEditor] 生成 {className}_Gen.g.cs 失败，请检查保存路径配置。");
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 反射被检视类型上所有命中绑定前缀的 <c>[SerializeField]</c> 私有字段（含基类继承），
        /// 在 prefab 子树内按字段名精确匹配节点（遇 widget 边界停止下钻），将引用绑回序列化字段。
        /// </summary>
        private void BindSerializedFieldsByNodeName()
        {
            var component = (UIBaseMono)target;
            Transform root = component.transform;

            var prefixes = CollectBindPrefixes();
            if (prefixes.Count == 0)
            {
                Log.Warning("[UIBaseMonoEditor] 未取到绑定前缀规则（ScriptGeneratorSetting 缺失？），绑定中止。");
                return;
            }

            var so = new SerializedObject(target);
            int boundCount = 0;

            foreach (var field in CollectSerializeFields(component.GetType(), prefixes))
            {
                Transform node = ScriptGenerator.FindNodeByNameWithWidgetBoundary(root, field.Name);
                if (node == null)
                {
                    Log.Warning($"[UIBaseMonoEditor] 字段 [{field.Name}] 找不到同名节点，已跳过（命名是否一致？）。");
                    continue;
                }

                UnityEngine.Object value = ResolveReference(node, field.FieldType);
                if (value == null)
                {
                    Log.Warning($"[UIBaseMonoEditor] 字段 [{field.Name}] 找到节点 [{node.name}]，但其上缺少组件 {field.FieldType.Name}，已跳过。");
                    continue;
                }

                var prop = so.FindProperty(field.Name);
                if (prop == null)
                {
                    Log.Warning($"[UIBaseMonoEditor] 字段 [{field.Name}] 无对应序列化属性，已跳过。");
                    continue;
                }

                prop.objectReferenceValue = value;
                boundCount++;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);

            Log.Info($"[UIBaseMonoEditor] 绑定完成：成功绑定 {boundCount} 个字段。");
        }

        /// <summary>
        /// 收集绑定前缀集（各规则的 <c>uiElementRegex</c>，如 m_btn / m_img / m_eximg）。
        /// </summary>
        private static List<string> CollectBindPrefixes()
        {
            var result = new List<string>();
            var rules = ScriptGeneratorSetting.GetScriptGenerateRule();
            if (rules == null)
            {
                return result;
            }

            foreach (var rule in rules)
            {
                if (!string.IsNullOrEmpty(rule.uiElementRegex))
                {
                    result.Add(rule.uiElementRegex);
                }
            }
            return result;
        }

        /// <summary>
        /// 反射类型上所有带 <c>[SerializeField]</c> 且名字命中绑定前缀的私有实例字段，含基类继承的私有字段
        /// （逐层上溯，BindingFlags 不传递到基类私有成员，故手动遍历类型链）。
        /// </summary>
        private static IEnumerable<FieldInfo> CollectSerializeFields(Type type, List<string> prefixes)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;

            for (Type t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                foreach (var field in t.GetFields(flags))
                {
                    if (!seen.Add(field.Name))
                    {
                        continue;
                    }

                    if (field.GetCustomAttribute<SerializeField>() == null)
                    {
                        continue;
                    }

                    if (!MatchesAnyPrefix(field.Name, prefixes))
                    {
                        continue;
                    }

                    yield return field;
                }
            }
        }

        private static bool MatchesAnyPrefix(string fieldName, List<string> prefixes)
        {
            foreach (var prefix in prefixes)
            {
                if (fieldName.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 按字段声明类型从节点解析引用对象：
        /// GameObject → 节点物体；Transform / RectTransform → 节点 transform；其余 → GetComponent。
        /// </summary>
        private static UnityEngine.Object ResolveReference(Transform node, Type fieldType)
        {
            if (fieldType == typeof(GameObject))
            {
                return node.gameObject;
            }

            if (fieldType == typeof(Transform) || fieldType == typeof(RectTransform))
            {
                return node.transform;
            }

            return node.GetComponent(fieldType);
        }
    }
}

#endif
