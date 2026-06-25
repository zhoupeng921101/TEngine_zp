using System;
using System.IO;
using YooAsset.Editor;

// 通用「按扩展名收集」过滤规则。
// 在 AssetBundleCollector 窗口的 FilterRule 下拉里选中本规则，并在 User Data 框填写扩展名清单，
// 即可让该收集器只收集匹配扩展名的资源，免去为每种类型单独写一个过滤类。
//
// User Data 写法约定：
// - 多个扩展名用 '|' / ';' / ',' 任一分隔，如 "*.mp3|*.wav|*.ogg"。
// - 每个 token 容忍 "*.mp3" / ".mp3" / "mp3" 三种写法。
// - 大小写不敏感。
// - User Data 为空时退化为收集全部（防误伤）。
[DisplayName("按扩展名收集(UserData填 *.mp3|*.wav)")]
public class CollectByExtensions : IFilterRule
{
    public string FindAssetType => EAssetSearchType.All.ToString();

    public bool IsCollectAsset(FilterRuleData data)
    {
        if (string.IsNullOrEmpty(data.UserData))
            return true;

        string ext = Path.GetExtension(data.AssetPath); // ".wav"（含点）
        if (string.IsNullOrEmpty(ext))
            return false;

        foreach (var raw in data.UserData.Split(new[] { '|', ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = raw.Trim().TrimStart('*');       // "*.mp3" → ".mp3"
            if (!t.StartsWith("."))
                t = "." + t;                          // "mp3"   → ".mp3"
            if (string.Equals(ext, t, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
