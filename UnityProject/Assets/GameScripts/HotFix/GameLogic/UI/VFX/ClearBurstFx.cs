using UnityEngine;
using TEngine;

namespace GameLogic
{
    /// <summary>
    /// 消除爆破粒子特效播放器（设计配方 clear_burst：白光闪核 + 糖果碎块放射，约 0.6s 消散）。
    /// 视觉壳是预制 <c>Assets/AssetRaw/Effects/ClearBurstFx.prefab</c>（根 UIParticle + 两层 ParticleSystem：
    /// 子物体 "Flash" = 闪光核保持白；"Debris" = 碎块按被消方块色染色）。
    /// UIParticle 每帧把子 ParticleSystem 的模拟结果烘进 Canvas mesh 渲染，故子 PS 只需 ps.Play() 自驱，
    /// 渲染尺寸由根 UIParticle 的 scale 决定（预制内已设为可见量级，子 PS 数值按该 scale 反算）。
    /// 本类只触碰内置 ParticleSystem（染色 + 播放 + 计时自毁），不引用 UIParticle 包类型，
    /// 故 GameLogic 程序集无需引用 Coffee.UIParticle——预制以序列化数据承载该组件，实例化即生效。
    /// 播完按生命周期上限计时销毁；实例由 AssetsReference 自管引用计数（随 GameObject 销毁释放资源）。
    /// </summary>
    public sealed class ClearBurstFx : MonoBehaviour
    {
        // 预制 location（YooAsset Effects 组 AddressByFileName，地址=文件名）。
        public const string PrefabLocation = "ClearBurstFx";

        // 自毁时长：覆盖两层最长生命周期（Debris lifetime 0.66 + 余量）。
        private const float SelfDestroyDelay = 0.9f;

        private float _t;

        /// <summary>
        /// 在指定父层、按格中心设计坐标实例化一发消除爆破，碎块层染成被消方块色。
        /// </summary>
        /// <param name="parent">父层 RectTransform（与棋盘格同坐标系，如 BoardLayer）。</param>
        /// <param name="designCx">格中心设计 X（左上原点、Y 下正）。</param>
        /// <param name="designCy">格中心设计 Y。</param>
        /// <param name="debrisColor">碎块层主色（被消方块色）。</param>
        public static void Spawn(Transform parent, float designCx, float designCy, Color debrisColor)
        {
            // 设计坐标重载：父层须为设计全屏 overlay（如 m_rect_Content）。自适应棋盘走 SpawnAtLocal 直接传本地坐标。
            SpawnAtLocal(parent, BlockLayout.DesignToAnchored(designCx, designCy), debrisColor);
        }

        /// <summary>
        /// 在指定父层、按父层本地 anchoredPosition 实例化一发消除爆破（碎块层染被消方块色）。
        /// 自适应棋盘用：父层 = m_rect_BoardLayer，本地坐标由窗口的 BoardCellLocalPos 现算（不经设计坐标换算）。
        /// </summary>
        /// <param name="parent">父层 RectTransform。</param>
        /// <param name="localAnchored">在 parent 本地的 anchoredPosition（parent anchor/pivot 居中时即中心相对偏移）。</param>
        /// <param name="debrisColor">碎块层主色（被消方块色）。</param>
        public static void SpawnAtLocal(Transform parent, Vector2 localAnchored, Color debrisColor)
        {
            var go = GameModule.Resource.LoadGameObject(PrefabLocation, parent);
            if (go == null)
            {
                // 寻址失败不应静默：预制未进运行时清单 / Effects 收集器未覆盖时在此暴露。
                Log.Warning($"[ClearBurstFx] LoadGameObject('{PrefabLocation}') 返回 null，特效未播放。检查 AssetBundleCollector 是否覆盖 Effects 目录。");
                return;
            }

            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.localScale = Vector3.one;
                rt.anchoredPosition = localAnchored;
            }

            // 给碎块层染色：Debris 的 colorOverLifetime 保持白→白，故主色相完全由 main.startColor 注入。
            // 子层按名定位："Debris" 染被消色；"Flash" 保持白不动。
            var debris = go.transform.Find("Debris");
            if (debris != null)
            {
                var ps = debris.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var main = ps.main;
                    main.startColor = debrisColor;
                }
            }

            // 重播两层：清空残留后 Play，由 UIParticle 每帧自动烘焙渲染。
            // burst 颗数/数值全部来自预制 ParticleSystem 配置，代码不发射、不硬编码数量。
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Clear(true);
                ps.Play(true);
            }

            var fx = go.AddComponent<ClearBurstFx>();
            fx._t = 0f;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            if (_t >= SelfDestroyDelay)
            {
                Object.Destroy(gameObject);
            }
        }
    }
}
