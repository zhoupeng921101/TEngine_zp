using UnityEditor;
using UnityEditor.UI;

namespace GameLogic.EditorTools
{
    /// <summary>
    /// ExImage 自定义 Inspector：在标准 Image Inspector 之上补画 ExImage 专属序列化字段。
    /// UnityEditor.UI.ImageEditor 以 [CustomEditor(typeof(Image), true)] 注册，对子类生效但只画 Image 自带属性，
    /// 导致 ExImage 的 spriteAtlas / m_DrawOnNull / m_AdaptiveSize 在面板里被吞掉。
    /// nowSpriteName 不在此补画：它是运行时去重状态、不序列化，Inspector 编辑它无渲染效果。
    /// </summary>
    [CustomEditor(typeof(GameLogic.ExImage), true)]
    [CanEditMultipleObjects]
    public class ExImageEditor : ImageEditor
    {
        private SerializedProperty _spriteAtlas;
        private SerializedProperty _drawOnNull;
        private SerializedProperty _adaptiveSize;

        protected override void OnEnable()
        {
            base.OnEnable();
            _spriteAtlas = serializedObject.FindProperty("spriteAtlas");
            _drawOnNull = serializedObject.FindProperty("m_DrawOnNull");
            _adaptiveSize = serializedObject.FindProperty("m_AdaptiveSize");
        }

        public override void OnInspectorGUI()
        {
            // 标准 Image 部分（Sprite/Color/Material/Raycast Target 等）。
            base.OnInspectorGUI();

            EditorGUILayout.Space();

            serializedObject.Update();
            if (_spriteAtlas != null)
            {
                EditorGUILayout.PropertyField(_spriteAtlas);
            }
            if (_drawOnNull != null)
            {
                EditorGUILayout.PropertyField(_drawOnNull);
            }
            if (_adaptiveSize != null)
            {
                EditorGUILayout.PropertyField(_adaptiveSize);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
