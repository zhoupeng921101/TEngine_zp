using System.Globalization;
using System.Xml;
using TEngine;
using TMPro;
using UnityEngine;
namespace GameLogic
{
    [ExecuteAlways]
    public class CustomItalicAngle : MonoBehaviour
    {
        private TMP_Text textMeshPro;
        public float italicAngle = 15f; // 自定义斜体角度
        void Start()
        {
            if (textMeshPro == null)
            {
                textMeshPro = GetComponent<TMP_Text>();
            }
            AdjustItalicAngle(textMeshPro.textInfo);
        }
        void OnValidate()
        {
            if (textMeshPro != null)
            {
                AdjustItalicAngle(textMeshPro.textInfo);
            }
        }
        public void RefreshTextMeshProLtalic()
        {
            AdjustItalicAngle(textMeshPro.textInfo);
        }
        void AdjustItalicAngle(TMP_TextInfo textInfo)
        {
            // 强制更新文本网格
            textMeshPro.ForceMeshUpdate();
            if (textInfo != null)
            {
                float angle = italicAngle * Mathf.Deg2Rad;
                for (int i = 0; i < textInfo.characterCount; i++)
                {
                    TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                    if (!charInfo.isVisible) continue;
                    int vertexIndex = charInfo.vertexIndex;
                    int materialIndex = charInfo.materialReferenceIndex;
                    Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
                    // 计算每个顶点的偏移量，使字符倾斜
                    float offsetX0 = Mathf.Tan(angle) * (vertices[vertexIndex + 0].y - charInfo.baseLine);
                    float offsetX1 = Mathf.Tan(angle) * (vertices[vertexIndex + 1].y - charInfo.baseLine);
                    float offsetX2 = Mathf.Tan(angle) * (vertices[vertexIndex + 2].y - charInfo.baseLine);
                    float offsetX3 = Mathf.Tan(angle) * (vertices[vertexIndex + 3].y - charInfo.baseLine);
                    vertices[vertexIndex + 0] += new Vector3(offsetX0, 0, 0);
                    vertices[vertexIndex + 1] += new Vector3(offsetX1, 0, 0);
                    vertices[vertexIndex + 2] += new Vector3(offsetX2, 0, 0);
                    vertices[vertexIndex + 3] += new Vector3(offsetX3, 0, 0);
                }
                // 更新网格顶点数据
                textMeshPro.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            }
        }
    }
}