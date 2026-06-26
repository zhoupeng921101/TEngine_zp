using UnityEngine;
using UnityEngine.UI;
using TEngine;

namespace GameLogic
{
    /// <summary>
    /// 棋盘格底块 Widget（MergeOrderWindow 棋盘每个已占格一个实例，由窗口池化复用）。
    /// 承载单格皮肤纹理：彩色态按方块类型贴 default_skin 各自纹理、单色态全盘统一贴当前单色 sprite，
    /// 皮肤选取在窗口侧完成（窗口持 _merge.Skin / BlockSkinCatalog），本 widget 只接收已解析的 sprite 定位名。
    /// 绑定字段 m_eximg_Skin 由 BlockWidget_Gen.g.cs 的 ScriptGenerator() 负责，本文件不重复定义。
    /// </summary>
    public partial class BlockWidget
    {
        /// <summary>
        /// 皮肤 sprite 异步加载到位前的纯色占位（避免闪空）：纯 tint，不动 sprite。
        /// 窗口在新建格时按方块色（BlockLayout.ColorOf）先调一次，随后 <see cref="SetSkin"/> 切白 tint + 贴图覆盖。
        /// </summary>
        public void SetPlaceholder(Color color)
        {
            if (m_eximg_Skin != null) m_eximg_Skin.color = color;
        }

        /// <summary>
        /// 施加格底块皮肤：白 tint 让 sprite 显本色 + 贴当前态纹理（散 PNG 按文件名 location，引用计数自管）。
        /// </summary>
        /// <param name="spriteLocation">已由窗口按单色/彩色态解析好的 sprite 定位名。</param>
        public void SetSkin(string spriteLocation)
        {
            if (m_eximg_Skin == null) return;
            m_eximg_Skin.color = Color.white; // sprite 自带颜色，tint 用白避免叠色
            m_eximg_Skin.SpriteName = spriteLocation;
        }

        /// <summary>对格底块做一次轻微放大反馈（落子命中该格时调用）。ScalePunch 仅销毁组件、不动 GameObject，可重复调用。</summary>
        public void Punch()
        {
            gameObject.AddComponent<ScalePunch>();
        }
    }
}
