// TODO（设计 17 §3.6 / §七 O2）：列表项 Widget 骨架，本轮未接 prefab、未投放到任何窗口。
// 待后续 UI 轮次：配同名 prefab 到 Assets/AssetRaw/UI/Prefabs/、由窗口经 CreateWidget/AdjustIconNum 实例化。
// 本轮只作「拿到 RewardView 怎么贴到控件」的接法示范，编译通过即可，不要求运行跑通（验收锚在归一层纯逻辑）。
using UnityEngine.UI;
using GameLogic.BlockBlast.Reward;

namespace GameLogic
{
    /// <summary>
    /// 通用奖励列表项 Widget（设计 17 §3.6，UI 接法示范）。展示「拿到 <see cref="RewardView"/>
    /// 怎么贴到控件」，供后续接 UI 时照搬。仿 ui-patterns 的 UIWidget 模板。
    /// </summary>
    /// <remarks>
    /// 本轮不挂 prefab、不投放（无美术 + UI 投放是独立后续，设计 17 §七 O2）。因无 prefab
    /// 它不会被实例化，只作编译通过的接法示范。真实 Sprite 加载用既有 <c>Image.SetSprite</c>
    /// （内置缓存池，无需手动释放，设计 17 §3.5 / §七 O1）。名称文本表本轮未接，临时显文本 id（O4）。
    /// </remarks>
    public class RewardItemWidget : UIWidget
    {
        private Image _imgIcon;
        private Image _imgQualityFrame;
        private Text _textName;
        private Text _textCount;

        protected override void ScriptGenerator()
        {
            _imgIcon = FindChildComponent<Image>("m_img_Icon");
            _imgQualityFrame = FindChildComponent<Image>("m_img_QualityFrame");
            _textName = FindChildComponent<Text>("m_text_Name");
            _textCount = FindChildComponent<Text>("m_text_Count");
        }

        /// <summary>把一项奖励的归一视图贴到控件上。</summary>
        public void SetData(RewardView v)
        {
            if (!string.IsNullOrEmpty(v.IconName))
            {
                _imgIcon.SetSprite(v.IconName); // 内置缓存池，无需手动释放；无美术时落空不影响文字 / 数量 / 颜色
            }
            _imgQualityFrame.color = v.QualityColor;
            _textName.text = v.NameTextId.ToString(); // 文本表接入前临时显 id（设计 17 §七 O4）
            _textCount.text = v.CountText;
        }
    }
}
