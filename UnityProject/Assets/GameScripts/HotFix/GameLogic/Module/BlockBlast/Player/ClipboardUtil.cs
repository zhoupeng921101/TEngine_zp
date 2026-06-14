using System;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// id 复制剪贴板工具（设计 18 §3.7）。可注入 sink 的薄封装，使逻辑可单测、不碰真实剪贴板。
    /// </summary>
    /// <remarks>
    /// 默认生产 sink 写真实剪贴板（<c>GUIUtility.systemCopyBuffer</c>，运行期可用）；
    /// 测试注入内存 sink 断言写入内容，测后还原 Sink。EditMode 也无桌面剪贴板语义，故走 sink 隔离。
    /// </remarks>
    public static class ClipboardUtil
    {
        /// <summary>可注入接缝：默认写真实系统剪贴板；测试替为捕获 lambda。</summary>
        public static Action<string> Sink = text => UnityEngine.GUIUtility.systemCopyBuffer = text;

        /// <summary>复制文本到剪贴板（经 <see cref="Sink"/>）。空串/空 sink 不写。</summary>
        public static void Copy(string text)
        {
            if (string.IsNullOrEmpty(text)) return;   // 空不写
            Sink?.Invoke(text);
        }
    }
}
