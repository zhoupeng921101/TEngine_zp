using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;

namespace PSDUIImporter
{
    /// <summary>
    /// 中文 → 英文 翻译（用于组件化重命名的基名）。
    /// 策略：先查本地术语表缓存；缺失则调 MyMemory(免 key) 翻译并写回缓存；失败/离线则保留原文。
    /// 缓存 = 可手编 JSON：Assets/PSD2UGUI/Editor/Componentize/TranslationCache.json（翻得不准可手改覆盖）。
    /// </summary>
    internal static class Translator
    {
        private static Dictionary<string, string> _cache;

        private static string CacheAbsPath()
        {
            return Application.dataPath + "/PSD2UGUI/Editor/Componentize/TranslationCache.json";
        }

        public static bool HasChinese(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s) if (c >= 0x4E00 && c <= 0x9FFF) return true;
            return false;
        }

        /// <summary>含中文则翻成英文标识符；否则原样返回。失败保留原文。</summary>
        public static string ToEnglish(string text)
        {
            if (string.IsNullOrEmpty(text) || !HasChinese(text)) return text;
            Load();
            string hit;
            if (_cache.TryGetValue(text, out hit) && !string.IsNullOrEmpty(hit)) return hit;

            string raw = FetchMyMemory(text);
            string ident = Sanitize(raw);
            if (!string.IsNullOrEmpty(ident))
            {
                _cache[text] = ident;
                Save();
                Debug.Log("[翻译] " + text + " → " + ident + "（已缓存）");
                return ident;
            }
            Debug.LogWarning("[翻译] 未翻出「" + text + "」，保留原文（可手动加进 TranslationCache.json）");
            return text;
        }

        private static string FetchMyMemory(string text)
        {
            try
            {
                string url = "https://api.mymemory.translated.net/get?q=" + Uri.EscapeDataString(text) + "&langpair=zh|en";
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 8;
                    UnityWebRequestAsyncOperation op = req.SendWebRequest();
                    while (!op.isDone) System.Threading.Thread.Sleep(10); // 一次性按钮操作，短阻塞可接受
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning("[翻译] 请求失败：" + req.error);
                        return null;
                    }
                    MMResp resp = JsonUtility.FromJson<MMResp>(req.downloadHandler.text);
                    return (resp != null && resp.responseData != null) ? resp.responseData.translatedText : null;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[翻译] 异常：" + e.Message);
                return null;
            }
        }

        // 翻译结果 → 合法标识符片段（PascalCase，仅字母数字）
        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            string[] parts = Regex.Split(s, "[^A-Za-z0-9]+");
            StringBuilder sb = new StringBuilder();
            foreach (string p in parts)
            {
                if (p.Length == 0) continue;
                sb.Append(char.ToUpper(p[0]));
                if (p.Length > 1) sb.Append(p.Substring(1));
            }
            string r = sb.ToString();
            if (r.Length > 0 && char.IsDigit(r[0])) r = "_" + r;
            return r.Length == 0 ? null : r;
        }

        private static void Load()
        {
            if (_cache != null) return;
            _cache = new Dictionary<string, string>();
            try
            {
                string abs = CacheAbsPath();
                if (File.Exists(abs))
                {
                    CacheFile cf = JsonUtility.FromJson<CacheFile>(File.ReadAllText(abs));
                    if (cf != null && cf.items != null)
                        foreach (Entry it in cf.items)
                            if (!string.IsNullOrEmpty(it.zh)) _cache[it.zh] = it.en;
                }
            }
            catch (Exception e) { Debug.LogWarning("[翻译] 读缓存失败：" + e.Message); }
        }

        private static void Save()
        {
            try
            {
                CacheFile cf = new CacheFile();
                foreach (KeyValuePair<string, string> kv in _cache)
                    cf.items.Add(new Entry { zh = kv.Key, en = kv.Value });
                string abs = CacheAbsPath();
                Directory.CreateDirectory(Path.GetDirectoryName(abs));
                File.WriteAllText(abs, JsonUtility.ToJson(cf, true));
                AssetDatabase.Refresh();
            }
            catch (Exception e) { Debug.LogWarning("[翻译] 写缓存失败：" + e.Message); }
        }

        [Serializable] private class Entry { public string zh; public string en; }
        [Serializable] private class CacheFile { public List<Entry> items = new List<Entry>(); }
        [Serializable] private class MMResp { public MMData responseData; }
        [Serializable] private class MMData { public string translatedText; }
    }
}
