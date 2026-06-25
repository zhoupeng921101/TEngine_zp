using UnityEditor;
using UnityEngine;

namespace UIAtlasPackerTool
{
    /// <summary>
    /// 将工程 Sprite Packer 模式切到 Sprite Atlas V2（Enabled）。
    /// 工程图集为 .spriteatlasv2，需 V2 packer 才会被打包；V1 packer 下 V2 图集 spriteCount=0、GetSprite 全返回 null。
    /// 切换会触发全部 .spriteatlasv2 图集重打，并改动 ProjectSettings/EditorSettings.asset。
    /// </summary>
    public static class SpritePackerModeSetup
    {
        [MenuItem("Tools/Sprite Atlas/Set Packer Mode = V2 (Enabled)")]
        public static void SetSpritePackerModeV2()
        {
            EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            AssetDatabase.SaveAssets();
            Debug.Log($"[SpritePackerModeSetup] EditorSettings.spritePackerMode = {EditorSettings.spritePackerMode}");
        }
    }
}
