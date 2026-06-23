using System;
using System.Text;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using GameLogic;
using TEngine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GameUtils
{
    /// <summary>
    /// 根据T值，计算贝塞尔曲线上面相对应的点
    /// </summary>
    /// <param name="t"></param>T值
    /// <param name="p0"></param>起始点
    /// <param name="p1"></param>控制点
    /// <param name="p2"></param>目标点
    /// <returns></returns>根据T值计算出来的贝赛尔曲线点
    private static  Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        
        Vector3 p = uu * p0;
        p += 2 * u * t * p1;
        p += tt * p2;
         
        return p;
    }
    
    /// <summary>
    /// 获取存储贝塞尔曲线点的数组
    /// </summary>
    /// <param name="startPoint"></param>起始点
    /// <param name="controlPoint"></param>控制点
    /// <param name="endPoint"></param>目标点
    /// <param name="segmentNum"></param>采样点的数量
    /// <returns></returns>存储贝塞尔曲线点的数组
    public static Vector3 [] GetBezierList(Vector3 startPoint, Vector3 controlPoint, Vector3 endPoint,int segmentNum = 6)
    {
        Vector3 [] path = new Vector3[segmentNum];
        for (int i = 1; i <= segmentNum; i++)
        {
            float t = i / (float)segmentNum;
            Vector3 pixel = CalculateCubicBezierPoint(t, startPoint,
                controlPoint, endPoint);
            path[i - 1] = pixel;
        }
        return path;
    }
    
    /// <summary>
    /// 世界坐标转换为屏幕坐标
    /// </summary>
    /// <param name="camera">主摄像机</param>
    /// <param name="worldPoint">屏幕坐标</param>
    /// <returns></returns>
    public static Vector2 WorldPointToScreenPoint(Camera camera,Vector3 worldPoint)
    {
        // Camera.main 世界摄像机
        Vector2 screenPoint = camera.WorldToScreenPoint(worldPoint);
        return screenPoint;
    }
 
    /// <summary>
    /// 屏幕坐标转换为世界坐标
    /// </summary>
    /// <param name="camera">主摄像机</param>
    /// <param name="screenPoint">屏幕坐标</param>
    /// <param name="planeZ">距离摄像机 Z 平面的距离</param>
    /// <returns></returns>
    public static Vector3 ScreenPointToWorldPoint(Camera camera,Vector2 screenPoint, float planeZ)
    {
        // Camera.main 世界摄像机
        Vector3 position = new Vector3(screenPoint.x, screenPoint.y, planeZ);
        Vector3 worldPoint = camera.ScreenToWorldPoint(position);
        return worldPoint;
    }
    
    // RectTransformUtility.WorldToScreenPoint
    // RectTransformUtility.ScreenPointToWorldPointInRectangle
    // RectTransformUtility.ScreenPointToLocalPointInRectangle
    // 上面三个坐标转换的方法使用 Camera 的地方
    // 当 Canvas renderMode 为 RenderMode.ScreenSpaceCamera、RenderMode.WorldSpace 时 传递参数 canvas.worldCamera
    // 当 Canvas renderMode 为 RenderMode.ScreenSpaceOverlay 时 传递参数 null
    // UI 坐标转换为屏幕坐标
    public static Vector2 UIPointToScreenPoint(Camera uiCamera,Vector3 worldPoint)
    {
        // RectTransform：target
        // worldPoint = target.position;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, worldPoint);
        return screenPoint;
    }
 
    // 屏幕坐标转换为 UGUI 坐标
    public static Vector3 ScreenPointToUIPoint(Camera uiCamera,RectTransform rt, Vector2 screenPoint)
    {
        Vector3 globalMousePos;
        //UI屏幕坐标转换为世界坐标
        // 当 Canvas renderMode 为 RenderMode.ScreenSpaceCamera、RenderMode.WorldSpace 时 uiCamera 不能为空
        // 当 Canvas renderMode 为 RenderMode.ScreenSpaceOverlay 时 uiCamera 可以为空
        RectTransformUtility.ScreenPointToWorldPointInRectangle(rt, screenPoint, uiCamera, out globalMousePos);
        // 转换后的 globalMousePos 使用下面方法赋值
        // target 为需要使用的 UI RectTransform
        // rt 可以是 target.GetComponent<RectTransform>(), 也可以是 target.parent.GetComponent<RectTransform>()
        // target.transform.position = globalMousePos;
        return globalMousePos;
    }
 
    // 屏幕坐标转换为 UGUI RectTransform 的 anchoredPosition
    public static Vector2 ScreenPointToUILocalPoint(Camera uiCamera,RectTransform parentRT, Vector2 screenPoint)
    {
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRT, screenPoint, uiCamera, out localPos);
        // 转换后的 localPos 使用下面方法赋值
        // target 为需要使用的 UI RectTransform
        // parentRT 是 target.parent.GetComponent<RectTransform>()
        // 最后赋值 target.anchoredPosition = localPos;
        return localPos;
    }
    
    //返回今天结束剩下的秒数
    public static int GetSecondsUntilEndOfDay()
    {
        DateTime now = DateTime.Now;
        DateTime endOfDay = DateTime.Today.AddDays(1);
        
        return (int)(endOfDay - now).TotalSeconds;
    }
    //返回今天结束时的时间戳
    public static long GetTicksUntilEndOfDay()
    {
        DateTime endOfDay = DateTime.Today.AddDays(1);
        return endOfDay.Ticks;
    }
    
    //返回是否点击在ui上
    public static bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
            return false;

#if UNITY_EDITOR || UNITY_STANDALONE
        return EventSystem.current.IsPointerOverGameObject();
#else
        if (Input.touchCount > 0)
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return false;
#endif
    }
    
    // 解析每一帧的buffer并返回当前片段的分贝
    public static double GetVoiceDb(byte[] buffer)
    {
        if (buffer == null || buffer.Length < 2)
        {
            return 0;
        }
            
        var sampleCount = buffer.Length / 2;
        var audioData = new short[sampleCount];
        var copyBytes = sampleCount * 2;
        Buffer.BlockCopy(buffer, 0, audioData, 0, copyBytes);
        double sum = 0;
        for (var i = 0; i < audioData.Length; i++)
        {
            double s = audioData[i];
            sum += s * s;
        }
        var rms = Math.Sqrt(sum / audioData.Length);
        // rms 为 0 时 Log10 无意义，可按需处理
        var db = rms > 0 ? 20 * Math.Log10(rms) : double.NegativeInfinity;
        return db;
    }

    /// <summary>
    /// <paramref name="url"/> 长度大于 2 时视为网络地址并下载头像；否则使用 AB 内精灵，资源名为 <c>icon_head_</c> + url。
    /// </summary>
    public static async UniTask SetHeadUrl(Image image, string url)
    {
        var key = url ?? string.Empty;
        Log.Debug("图片地址:" + url);
        if (key.Length > 2)
        {
            var tex = await UIExtensionExtra.GetTextureFromWeb(key);
            if (image == null)
                return;
            if (tex == null)
                return;

            // 下载句柄释放后底层纹理可能失效，拷一份供 UI 长期持有
            Texture2D owned = tex;
            try
            {
                var dup = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                dup.SetPixels32(tex.GetPixels32());
                dup.Apply();
                UnityEngine.Object.Destroy(tex);
                owned = dup;
            }
            catch
            {
                // 不可读等异常时仍尝试使用原纹理
            }

            var sprite = Sprite.Create(owned, new Rect(0, 0, owned.width, owned.height), new Vector2(0.5f, 0.5f), 100f);
            image.sprite = sprite;
            return;
        }

        var address = "icon_head_" + key;
        var localSprite = await GameModule.Resource.LoadAssetAsync<Sprite>(address);
        if (image == null)
            return;
        if (localSprite != null)
            image.sprite = localSprite;
    }

    /// <summary>
    /// 将秒数格式化为单一单位：≥1 天用 d，≥1 小时用 h，≥1 分用 m，否则用 s。
    /// </summary>
    public static string GetFormatTime(int sec)
    {
        if (sec < 0)
            sec = 0;

        const int secPerMinute = 60;
        const int secPerHour = 3600;
        const int secPerDay = 86400;

        if (sec >= secPerDay)
            return $"{sec / secPerDay}d";
        if (sec >= secPerHour)
            return $"{sec / secPerHour}h";
        if (sec >= secPerMinute)
            return $"{sec / secPerMinute}m";
        return $"{sec}s";
    }
    
    public static string NameSanitize(string name, int maxLen = 5)
    {
        if (string.IsNullOrEmpty(name)) return "玩家";
        if (name.StartsWith("u_") || name.StartsWith("test_")) return name;

        // 移除代理对（emoji）
        var sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            if (char.IsHighSurrogate(name[i]) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]))
            {
                i++; // 跳过整个代理对
                continue;
            }
            sb.Append(name[i]);
        }
    
        string result = sb.ToString();
    
        // 移除零宽字符和特殊控制字符
        result = Regex.Replace(result, @"[\p{Cf}]", "");
    
        // 移除其他常见 emoji 所在的 Unicode 区块
        result = Regex.Replace(result, @"[\u2600-\u27BF]", "");      // 杂项符号
        result = Regex.Replace(result, @"[\u1F300-\u1F6FF]", "");    // 补充符号和象形文字
        result = Regex.Replace(result, @"[\u1F900-\u1F9FF]", "");    // 补充符号和象形文字扩展
    
        result = result.Trim();
    
        // 判断是否需要截断并添加 "..."
        if (result.Length > maxLen)
        {
            result = result.Substring(0, maxLen) + "...";
        }
    
        return string.IsNullOrEmpty(result) ? name : result;
    }
}