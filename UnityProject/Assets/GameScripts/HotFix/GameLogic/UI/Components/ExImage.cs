using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// SpriteAtlas v2 驱动的 Image 子类：按 sprite 名从图集取图渲染。
    /// GetSprite 返回的是图集内部新建的 sprite clone，必须在切名/销毁时 Destroy，否则泄漏 Sprite 对象。
    /// </summary>
    [AddComponentMenu("UI/ExImage", 30)]
    public class ExImage : Image
    {
        /// <summary>sprite 为空时是否仍按 Image 默认网格绘制（false 则清空网格不绘制）。</summary>
        public bool m_DrawOnNull;

        /// <summary>切图后是否把 RectTransform 尺寸调整为 sprite 像素尺寸。</summary>
        public bool m_AdaptiveSize;

        [SerializeField]
        public SpriteAtlas spriteAtlas;

        /// <summary>GetSprite 返回的运行期 clone，生命周期由本组件负责销毁。</summary>
        private Sprite mSpriteClone;

        // 当前 sprite 名，仅供 SpriteName setter 运行时去重（同名早返，避免重复克隆图集 sprite）。
        // 不序列化：无加载期应用钩子，序列化值对渲染无效；且非空序列化值会令 setter 早返、吞掉运行时首次赋值。
        // 静态图走 Source Image 直接拖；运行时动态图走 SpriteName 按名从图集换。
        private string nowSpriteName = string.Empty;

        public SpriteAtlas Atlas
        {
            get => spriteAtlas;
            set => spriteAtlas = value;
        }

        public string SpriteName
        {
            get => nowSpriteName;
            set
            {
                if (Atlas == null || nowSpriteName == value)
                {
                    return;
                }

                nowSpriteName = value;

                if (mSpriteClone != null)
                {
                    Destroy(mSpriteClone);
                    mSpriteClone = null;
                    sprite = null;
                }

                mSpriteClone = nowSpriteName == string.Empty ? null : GetSprite(value);
                sprite = mSpriteClone;
                AdaptiveImageSize(m_AdaptiveSize);
            }
        }

        public int width
        {
            get => (int)rectTransform.sizeDelta.x;
            set => rectTransform.sizeDelta = new Vector2(value, rectTransform.sizeDelta.y);
        }

        public int height
        {
            get => (int)rectTransform.sizeDelta.y;
            set => rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, value);
        }

        public float alpha
        {
            get => color.a;
            set
            {
                if (!value.Equals(color.a))
                {
                    Color c = color;
                    c.a = value;
                    color = c;
                }
            }
        }

        private Sprite GetSprite(string spriteName)
        {
            if (Atlas != null && spriteName != string.Empty)
            {
                return Atlas.GetSprite(spriteName);
            }

            return null;
        }

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            if (sprite == null || overrideSprite == null)
            {
                if (m_DrawOnNull)
                {
                    base.OnPopulateMesh(toFill);
                }
                else
                {
                    toFill.Clear();
                }
            }
            else
            {
                base.OnPopulateMesh(toFill);
            }
        }

        public void AdaptiveImageSize(bool outsize = false)
        {
            m_AdaptiveSize = outsize;
            if (sprite != null && m_AdaptiveSize)
            {
                Rect rect = sprite.rect;
                width = (int)rect.width;
                height = (int)rect.height;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (mSpriteClone != null)
            {
                Destroy(mSpriteClone);
            }
        }
    }
}
