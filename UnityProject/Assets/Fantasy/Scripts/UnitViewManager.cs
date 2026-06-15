#if FANTASY_UNITY
using System.Collections.Generic;
using UnityEngine;

namespace FantasyClient
{
    /// <summary>
    /// 极简单位视图管理：把服务器推送的单位映射为场景里的 GameObject（基础几何体，纯演示用）。
    /// 自己 = 绿色胶囊，其他单位 = 红色方块。正式项目应替换为对象池 + 真实预制体/美术。
    /// </summary>
    public static class UnitViewManager
    {
        private static readonly Dictionary<long, GameObject> Views = new Dictionary<long, GameObject>();
        private static Transform _root;

        public static int Count => Views.Count;

        private static Transform Root
        {
            get
            {
                if (_root == null)
                {
                    _root = new GameObject("FantasyUnits").transform;
                }
                return _root;
            }
        }

        public static void Spawn(long unitId, string unitName, Vector3 pos, bool isSelf)
        {
            if (Views.ContainsKey(unitId))
            {
                return;
            }

            var go = GameObject.CreatePrimitive(isSelf ? PrimitiveType.Capsule : PrimitiveType.Cube);
            go.name = $"Unit_{unitId}{(isSelf ? "_Self" : "")}";
            go.transform.SetParent(Root);
            go.transform.position = pos;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = isSelf ? Color.green : Color.red;
            }

            Views[unitId] = go;
        }

        public static void Remove(long unitId)
        {
            if (Views.TryGetValue(unitId, out var go))
            {
                Views.Remove(unitId);
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }
        }

        /// <summary>清空所有单位视图（断线/退出时调用）。</summary>
        public static void Clear()
        {
            foreach (var go in Views.Values)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }
            Views.Clear();
        }
    }
}
#endif
