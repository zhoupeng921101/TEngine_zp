using GameLogic;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngineInternal;
using Touch = UnityEngine.Touch;
#if ENABLE_WX
using WeChatWASM;
#endif

namespace TEngine
{
    /// <summary>
    /// Unity关的实用函数。
    /// </summary>
    public static class DUnityUtil
    {
        [TypeInferenceRule(TypeInferenceRules.TypeReferencedByFirstArgument)]
        public static Component AddMonoBehaviour(Type type, GameObject go)
        {
            var comp = go.GetComponent(type);
            if (comp == null)
            {
                comp = go.AddComponent(type);
            }
            return comp;
        }
        public static T AddMonoBehaviour<T>(Component comp) where T : Component
        {
            var ret = comp.GetComponent<T>();
            if (ret == null)
            {
                ret = comp.gameObject.AddComponent<T>();
            }
            return ret;
        }
        public static T AddMonoBehaviour<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp == null)
            {
                comp = go.AddComponent<T>();
            }
            return comp;
        }
        [TypeInferenceRule(TypeInferenceRules.TypeReferencedByFirstArgument)]
        public static void RmvMonoBehaviour(Type type, GameObject go)
        {
            var comp = go.GetComponent(type);
            if (comp != null)
            {
                UnityEngine.Object.Destroy(comp);
            }
        }
        public static void RmvMonoBehaviour<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp != null)
            {
                UnityEngine.Object.Destroy(comp);
            }
        }
        public static Transform FindChild(Transform transform, string path)
        {
            var findTrans = transform.Find(path);
            if (findTrans != null)
            {
                return findTrans;
            }
            return null;
        }
        /// <summary>
        /// 根据名字找到子节点，主要用于dummy接口
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Transform FindChildByName(Transform transform, string name)
        {
            if (transform == null)
            {
                return null;
            }
            for (int i = 0; i < transform.childCount; i++)
            {
                var childTrans = transform.GetChild(i);
                if (childTrans.name == name)
                {
                    return childTrans;
                }
                var find = FindChildByName(childTrans, name);
                if (find != null)
                {
                    return find;
                }
            }
            return null;
        }
        [TypeInferenceRule(TypeInferenceRules.TypeReferencedByFirstArgument)]
        public static Component FindChildComponent(Type type, Transform transform, string path)
        {
            var findTrans = transform.Find(path);
            if (findTrans != null)
            {
                return findTrans.gameObject.GetComponent(type);
            }
            return null;
        }
        public static T FindChildComponent<T>(Transform transform, string path) where T : Component
        {
            var findTrans = transform.Find(path);
            if (findTrans != null)
            {
                return findTrans.gameObject.GetComponent<T>();
            }
            return null;
        }
        public static void SetLayer(GameObject go, int layer)
        {
            if (go == null)
            {
                return;
            }
            SetLayer(go.transform, layer);
        }
        public static void SetLayer(Transform trans, int layer)
        {
            if (trans == null)
            {
                return;
            }
            trans.gameObject.layer = layer;
            for (int i = 0, imax = trans.childCount; i < imax; ++i)
            {
                Transform child = trans.GetChild(i);
                SetLayer(child, layer);
            }
        }
        public static int RandomRangeInt(int min, int max)
        {
            return UnityEngine.Random.Range(min, max);
        }
        public static float RandomRangeFloat(float min, float max)
        {
            return UnityEngine.Random.Range(min, max);
        }
        public static Vector2 RandomInsideCircle(float radius)
        {
            return UnityEngine.Random.insideUnitCircle * radius;
        }
        [TypeInferenceRule(TypeInferenceRules.TypeReferencedByFirstArgument)]
        public static Array CreateUnityArray(Type type, int length)
        {
            return Array.CreateInstance(type, length);
        }
        public static T[] CreateUnityArray<T>(int length)
        {
            return new T[length];
        }
        public static GameObject Instantiate(GameObject go)
        {
            if (go != null)
            {
                return UnityEngine.Object.Instantiate(go);
            }
            return null;
        }
        public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance, int layerMask)
        {
            return Physics.Raycast(ray, out hitInfo, maxDistance, layerMask);
        }
        public static List<string> GetRegexMatchGroups(string pattern, string input)
        {
            List<string> list = new List<string>();
            var regexLink = new Regex(pattern);
            var links = regexLink.Match(input);
            for (var i = 0; i < links.Groups.Count; ++i)
            {
                list.Add(links.Groups[i].Value);
            }
            return list;
        }
        public static void SetMaterialVector3(Material mat, int nameId, Vector3 val)
        {
            mat.SetVector(nameId, val);
        }
        public static void GetVectorData(Vector3 val, out float x, out float y, out float z)
        {
            x = val.x;
            y = val.y;
            z = val.z;
        }
        public static void GetVector2Data(Vector2 val, out float x, out float y)
        {
            x = val.x;
            y = val.y;
        }
        public static bool GetTouchByFingerId(int fingerId, out Touch findTouch)
        {
            var find = false;
            var touchCnt = Input.touchCount;
            findTouch = new Touch();
            for (int i = 0; i < touchCnt; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.fingerId == fingerId)
                {
                    findTouch = touch;
                    find = true;
                    break;
                }
            }
            return find;
        }
        public static int GetHashCodeByString(string str)
        {
            return str.GetHashCode();
        }
        public static Vector3 GetClickPosForWorld()
        {
            return Camera.main.ScreenToWorldPoint(Input.mousePosition);
        }
        public static float GetWorldPointBottom(float offset = 0)
        {
            Camera cam = Camera.main;
            // 屏幕底部（Y=0）
            Vector3 bottomPos = cam.ScreenToWorldPoint(
                new Vector3(Screen.width * 0.5f, 0f, cam.nearClipPlane)
            );

            return bottomPos.y + offset;
        }

        public static Vector2 anchorMin, anchorMax;
//         public static void AdaptiveUIScreen(RectTransform Rootrect)
//         {
// #if UNITY_EDITOR
//             var rectInfo = Screen.safeArea;
//             float py = (float)rectInfo.y / (float)rectInfo.height;
//             // Rootrect初始时设置其Anchor，使其与父节点一样大，也就是屏幕的大小
//             anchorMin = rectInfo.position;
//             anchorMax = rectInfo.position + rectInfo.size;
//             anchorMin.x /= Screen.width;
//             anchorMin.y /= Screen.height;
//             anchorMax.x /= Screen.width;
//             anchorMax.y /= Screen.height;
//             Rootrect.anchorMax = anchorMax;
// #else
//             var safeArea = Mid.System.GetSafeArea();
//             anchorMax = new Vector2(safeArea.width + safeArea.left, safeArea.height);
//             anchorMax.x /= safeArea.width;
//             anchorMax.y /= safeArea.bottom;
//             Rootrect.anchorMax = anchorMax;
// #endif
//         }
        
        public static Rect GetRectToWX(RectTransform target)
        {
            var p = UIModule.UIRoot.transform.InverseTransformPoint(target.transform.position);
            var head = (int)((1 - anchorMax.y) * Screen.height);
            var screenWidth = (int)(Screen.width * anchorMax.x);
            var screenHeight = (int)(Screen.height * anchorMax.y);
            var referenceResolution = new Vector2(1080f, 1920f * Screen.width / 1080f);//宽度适配
            var width = (int)(screenWidth / referenceResolution.x * target.rect.width);
            var height = (int)(screenWidth / referenceResolution.x * target.rect.height);
            var xw = (screenWidth - width) / 2 + (int)p.x;
            var yh = (screenHeight - height) / 2 - (int)p.y + head;
            return new Rect(xw, yh, width, height);
        }
    }
}
