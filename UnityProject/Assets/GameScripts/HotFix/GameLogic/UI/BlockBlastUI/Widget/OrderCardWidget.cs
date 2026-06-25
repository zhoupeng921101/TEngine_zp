using UnityEngine;
using UnityEngine.UI;
using TEngine;

namespace GameLogic
{
    /// <summary>
    /// 订单卡 Widget（MergeOrderWindow 固定 2 张复用）。
    /// 数据填充由 MergeOrderWindow 调用 <see cref="SetData"/> 完成；
    /// 交付按钮点击通过 <see cref="OnDeliver"/> 回调暴露给窗口，窗口注入槽位索引闭包。
    /// 绑定字段由 OrderCardWidget_Gen.g.cs 的 ScriptGenerator() 负责，本文件不重复定义。
    /// </summary>
    public partial class OrderCardWidget : UIWidgetMono
    {
        /// <summary>交付按钮点击回调，由 MergeOrderWindow 在 CreateWidget 后注入。</summary>
        public System.Action OnDeliver;

        /// <summary>Gen 生成的按钮事件入口，转发到外部回调。</summary>
        private partial void OnClick_DeliverBtn()
        {
            OnDeliver?.Invoke();
        }

        /// <summary>交付按钮文字组件缓存（首次 SetData 时从按钮子节点取，供可交付态染色）。</summary>
        private Text _deliverLabel;

        /// <summary>
        /// 刷新订单卡显示数据。
        /// <para>sprite 名称传空则保持当前图标；canDeliver 控制按钮可交互态与背景/文字染色。</para>
        /// 色值沿用旧 RefreshOrders：可交付背景 0x33aa55 + 白字；不可交付背景 0x44444c + 灰字 0x888888。
        /// </summary>
        /// <param name="glyphSpriteName">元素图标 sprite 名称（SetSprite 加载，内置缓存池无需手动释放）。</param>
        /// <param name="reqLabel">需求文字，如 "Lv2\n×3"。</param>
        /// <param name="canDeliver">是否满足交付条件。</param>
        public void SetData(string glyphSpriteName, string reqLabel, bool canDeliver)
        {
            if (!string.IsNullOrEmpty(glyphSpriteName)) m_img_Glyph.SetSprite(glyphSpriteName, setNativeSize:true);
            
            m_text_Req.text = reqLabel;

            m_btn_Deliver.interactable = canDeliver;
            m_btn_Deliver.image.color = canDeliver
                ? new Color32(0x33, 0xaa, 0x55, 0xFF)
                : new Color32(0x44, 0x44, 0x4c, 0xFF);

            if (_deliverLabel == null) _deliverLabel = m_btn_Deliver.GetComponentInChildren<Text>();
            if (_deliverLabel != null)
                _deliverLabel.color = canDeliver ? Color.white : new Color32(0x88, 0x88, 0x88, 0xFF);
        }

        /// <summary>
        /// 元素图标落点 RectTransform（订单卡内的需求图标），供交付飞行动画取终点世界坐标。
        /// 只读暴露既有绑定节点，不改 prefab 结构。
        /// </summary>
        public RectTransform GlyphRect => m_img_Glyph != null ? m_img_Glyph.rectTransform : null;

        /// <summary>交付庆祝：对本卡 GameObject 施加 scale-punch（订单卡为常驻实例，原 RefreshOrders 对重建卡 punch 的等价替换）。</summary>
        public void Punch()
        {
            gameObject.AddComponent<ScalePunch>();
        }
    }
}
