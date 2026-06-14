using GameLogic.BlockBlast;
using GameLogic.Settings;

namespace GameLogic
{
    /// <summary>
    /// 运行期通用服务上下文（单例，设计 23 §五 方案 B）。
    /// 持有「数据层已建、需运行期持有者」的无主系统服务，提供统一初始化 + 统一接存档接缝。
    /// 玩法态（棋盘 / 得分 / 悔棋）仍归 <see cref="BlockGameState"/>，二者分层：
    /// 玩法态随开局 Reset，通用服务随会话长存。
    /// </summary>
    /// <remarks>
    /// 本轮只持有 <see cref="SettingsService"/>；player-info / item / mail / rank 后续逐个挂入，
    /// 接口预留薄而通用，不投机性预建成员。
    /// 启动接线（AudioSink + 首次 Load）在热更入口 <c>GameApp.StartGameLogic()</c> 完成
    /// （非热更区 ProcedureLaunch 引用不到本类，热更边界所致，设计 23 §五接线落点）。
    /// </remarks>
    public sealed class GameContext : SimpleSingleton<GameContext>
    {
        /// <summary>设置服务（音频开关 + 持久化 + 信息 getter，设计 19 数据层）。</summary>
        public SettingsService Settings { get; private set; }

        protected override void OnInit()
        {
            // 生产用框架键存储（PlayerPrefs），启动即从已保存的开关态加载。
            Settings = new SettingsService(new PlayerPrefsSettingsStore());
            Settings.Load();
        }

        /// <summary>
        /// 测试 / 注入入口：用指定存储重建 <see cref="Settings"/> 并加载。
        /// 单测经 <see cref="InMemorySettingsStore"/> 断言往返，不污染真实 PlayerPrefs（设计 23 §9.1 H/W 组）。
        /// </summary>
        public void InitSettingsWithStore(ISettingsStore store)
        {
            Settings = new SettingsService(store);
            Settings.Load();
        }
    }
}
