using Cysharp.Threading.Tasks;
using System.Collections;
using System.Threading;
using TEngine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;
namespace GameLogic
{
    public static class UIExtensionExtra
    {
    #region Unity UI Extension
        public static void SetAnchoredPositionX(this RectTransform rectTransform, float anchoredPositionX)
        {
            var value = rectTransform.anchoredPosition;
            value.x = anchoredPositionX;
            rectTransform.anchoredPosition = value;
        }
        public static void SetAnchoredPositionY(this RectTransform rectTransform, float anchoredPositionY)
        {
            var value = rectTransform.anchoredPosition;
            value.y = anchoredPositionY;
            rectTransform.anchoredPosition = value;
        }
        public static void SetAnchoredPosition3DZ(this RectTransform rectTransform, float anchoredPositionZ)
        {
            var value = rectTransform.anchoredPosition3D;
            value.z = anchoredPositionZ;
            rectTransform.anchoredPosition3D = value;
        }
        public static void SetColorAlpha(this UnityEngine.UI.Graphic graphic, float alpha)
        {
            var value = graphic.color;
            value.a = alpha;
            graphic.color = value;
        }
        public static void SetFlexibleSize(this LayoutElement layoutElement, Vector2 flexibleSize)
        {
            layoutElement.flexibleWidth = flexibleSize.x;
            layoutElement.flexibleHeight = flexibleSize.y;
        }
        public static Vector2 GetFlexibleSize(this LayoutElement layoutElement)
        {
            return new Vector2(layoutElement.flexibleWidth, layoutElement.flexibleHeight);
        }
        public static void SetMinSize(this LayoutElement layoutElement, Vector2 size)
        {
            layoutElement.minWidth = size.x;
            layoutElement.minHeight = size.y;
        }
        public static Vector2 GetMinSize(this LayoutElement layoutElement)
        {
            return new Vector2(layoutElement.minWidth, layoutElement.minHeight);
        }
        public static void SetPreferredSize(this LayoutElement layoutElement, Vector2 size)
        {
            layoutElement.preferredWidth = size.x;
            layoutElement.preferredHeight = size.y;
        }
        public static Vector2 GetPreferredSize(this LayoutElement layoutElement)
        {
            return new Vector2(layoutElement.preferredWidth, layoutElement.preferredHeight);
        }
    #endregion
        
        public static void AddListener(this Button button, UnityAction action)
        {
            button?.onClick.RemoveAllListeners();
            button?.onClick.AddListener(() =>
            {
                action();
                // GameModule.Audio.PlayUISound(1);
            });
        }
        public static void ShowCommonTip(params object[] param)
        {
            // if (GameModule.UI.HasWindow<CommonTip>())
            // {
            //     GameModule.UI.CloseUI<CommonTip>();
            // }
            // GameModule.UI.ShowUIAsync<CommonTip>(param);
        }
        public static async UniTask<Texture2D> GetTextureFromWeb(string url, CancellationToken cancellationToken = default, int timeout = 5)
        {
            using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(url))
            {
                using (DownloadHandlerTexture downloadTexture = new DownloadHandlerTexture(true))
                {
                    uwr.timeout = timeout;
                    uwr.downloadHandler = downloadTexture;
                    var (isCanceled, request) = await uwr.SendWebRequest().WithCancellation(cancellationToken).SuppressCancellationThrow();
                    if (isCanceled || request == null)
                    {
                        return null;
                    }
                    Texture2D texture2D = null;
                    if (!(uwr.result == UnityWebRequest.Result.ConnectionError || uwr.result == UnityWebRequest.Result.ProtocolError))
                    {
                        texture2D = downloadTexture.texture;
                    }
                    else
                    {
                        Log.Error("下载失败，请检查网络，或者下载地址是否正确");
                    }
                    return texture2D;
                }
            }
        }
    }
}
