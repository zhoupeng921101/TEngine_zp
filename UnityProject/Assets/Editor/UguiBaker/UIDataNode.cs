using System.Collections.Generic;

namespace EditorTools.Ugui
{
    /// <summary>
    /// 描述 JSON 的节点模型，与 JSON 字段名一一对应，Newtonsoft 直接映射。
    /// 每个节点只产出视觉占位：有可见文字(text 非空)建文本(Text)，否则建图片(Image)。
    /// 真按钮/滑条等控件在 Unity 里由人手动转，不在烘焙阶段推断。
    /// </summary>
    [System.Serializable]
    public class UIDataNode
    {
        /// <summary>中文描述：说明该节点是什么(替代原控件类型)。同时作为生成节点名。</summary>
        public string name;

        public int x;
        public int y;
        public int width;
        public int height;

        /// <summary>
        /// 可选：显式指定锚点预设，非空时跳过几何推断。为空(默认)则由烘焙器据子框相对父框的几何自动推断。
        /// 预设值：top-left/top/top-right/left/center/right/bottom-left/bottom/bottom-right/
        /// stretch/stretch-x/stretch-y/top-stretch/bottom-stretch/left-stretch/right-stretch。
        /// 非法值回退到自动推断。
        /// </summary>
        public string anchor = "";

        /// <summary>图片占位色(十六进制，可带 alpha)。文本节点可不填。</summary>
        public string color = "#FFFFFF00";

        /// <summary>有可见文字则建文本(Text)；为空则建图片(Image)。</summary>
        public string text = "";

        // 以下仅文本节点使用
        public string fontColor = "#FFFFFF";
        public int fontSize = 24;
        public string textAlign = "center";

        public List<UIDataNode> children;
    }
}
