using UnityEngine;
using TEngine;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Player;

namespace GameLogic
{
	/// <summary>
	/// 顶部持久 HUD 覆盖层(Top 层):承载头像 + 货币栏(体力 / 虔诚币 / 盲盒 + 体力恢复倒计时),
	/// 登录后跨界面常驻。主界面与合成面板复用本 HUD;弹窗(非主界面 / 合成面板的全屏窗占据 UI 层顶)时隐藏。
	/// 货币为服务端权威的本地投影展示(data-authority),不新增本地权威:
	///   体力  — 展示用 MergeOrderState 实例(从元层缓存 ImportMeta 取 energy + lastEnergyRegenTime),
	///           OnUpdate 每秒 ApplyTimeRegen 本地乐观恢复 + 体力倒计时(不落盘、不上报,纯显示);
	///   虔诚币 — GameContext.PlayerAttr.Piety(登录快照初始化 + delta-push 事件保鲜);
	///   盲盒  — 元层缓存 blindBoxCount(PlayerAttr 不持有;登录快照写入)。
	/// </summary>
	[Window(UILayer.Top, location : "UITopHudPanel")]
	public partial class UITopHudPanel
	{
		/// <summary>体力恢复 + 盲盒的展示投影实例(纯显示,不上报、不落盘)。</summary>
		private MergeOrderState _display;
		/// <summary>体力倒计时 widget(挂 m_rect_EnergyCountdownSlot)。</summary>
		private CountdownWidget _energyCountdown;
		/// <summary>体力时基恢复轮询累加器(秒),每满一秒补算一次 ApplyTimeRegen。</summary>
		private float _energyTickAccum;
		private const float EnergyTickInterval = 1f;

		// HUD 服务的两个主屏窗名白名单(顶层 UI 层窗命中其一才显示 HUD)。
		// UIModule 以 type.FullName 作 WindowName,故此处按全名比对——不能用 nameof(短名),否则永不命中、HUD 恒隐。
		private static readonly string MainMenuWindowName = typeof(UIMainMenuPanel).FullName;
		private static readonly string MergeOrderWindowName = typeof(UIMergeOrderPanel).FullName;

		protected override void OnCreate()
		{
			// 展示实例:从元层缓存载入体力 / 盲盒等(登录快照已把服务端权威值写进该缓存)。缓存缺失(极早期)则留缺省 0。
			_display = new MergeOrderState();
			var dto = MergeMetaPersistence.Load();
			if (dto != null) _display.ImportMeta(dto);

			// 体力倒计时 widget(资源定位名 == 类名 "CountdownWidget",CreateWidgetByType 走 AddressByFileName)。
			if (m_rect_EnergyCountdownSlot != null)
			{
				_energyCountdown = CreateWidgetByType<CountdownWidget>(m_rect_EnergyCountdownSlot);
				if (_energyCountdown == null)
					Log.Error("[UITopHudPanel] CountdownWidget 加载失败(资源定位名 CountdownWidget),体力倒计时未创建。");
				else if (_energyCountdown.rectTransform != null)
					_energyCountdown.rectTransform.localScale = Vector3.one;
			}

			// 虔诚币 / 体力保鲜:订阅元层属性变更(delta-push / 快照),到达即重绘对应项。
			var attr = GameContext.Instance?.PlayerAttr;
			if (attr != null) attr.OnAttrChanged += OnPlayerAttrChanged;

			RefreshCurrencyBar();
			RefreshEnergyCountdown(MergeMetaPersistence.NowUnixSec());
		}

		protected override void OnUpdate()
		{
			// 弹窗遮挡时隐藏:仅当顶层 UI 层窗为 HUD 所服务的两个主屏(主界面 / 合成面板)才显示;
			// 其它全屏窗占据 UI 层顶时隐藏,避免 HUD 显示在弹窗之上。Visible 未变即早返回,逐帧设置无额外开销。
			string topUI = GameModule.UI.GetTopWindow((int)UILayer.UI);
			Visible = topUI == MainMenuWindowName || topUI == MergeOrderWindowName;

			if (_display == null) return;
			_energyTickAccum += Time.deltaTime;
			if (_energyTickAccum < EnergyTickInterval) return;
			_energyTickAccum = 0f;

			long now = MergeMetaPersistence.NowUnixSec();
			int before = _display.Energy;
			_display.ApplyTimeRegen(now);          // 本地乐观恢复(纯显示,不落盘、不上报)
			if (_display.Energy != before) RefreshEnergyText();
			RefreshEnergyCountdown(now);
		}

		protected override void OnDestroyWindow()
		{
			var attr = GameContext.Instance?.PlayerAttr;
			if (attr != null) attr.OnAttrChanged -= OnPlayerAttrChanged;
		}

		/// <summary>元层属性变更回调:虔诚币变(Piety/All)重绘虔诚币;体力变(Energy/All)以服务端权威值校正展示体力后重绘。</summary>
		private void OnPlayerAttrChanged(AttrType type, long newBalance, string reason)
		{
			if (type == AttrType.All)
			{
				SyncEnergyFromAuthoritative();
				RefreshCurrencyBar();
				return;
			}
			if (type == AttrType.Piety) RefreshPietyText();
			if (type == AttrType.Energy) { SyncEnergyFromAuthoritative(); RefreshEnergyText(); }
		}

		/// <summary>以服务端权威体力(PlayerAttr.Energy)校正展示实例,并把恢复基准重置到此刻(避免二次补恢复)。</summary>
		private void SyncEnergyFromAuthoritative()
		{
			var attr = GameContext.Instance?.PlayerAttr;
			if (_display == null || attr == null || !attr.IsReady) return;
			_display.Energy = (int)attr.Energy;
			_display.LastEnergyRegenTime = MergeMetaPersistence.NowUnixSec();
		}

		private void RefreshCurrencyBar()
		{
			RefreshEnergyText();
			RefreshPietyText();
			RefreshBlindBoxText();
		}

		private void RefreshEnergyText()
		{
			if (m_text_Energy != null && _display != null)
				m_text_Energy.text = $"{_display.Energy}/{MergeOrderConfig.EnergyCap}";
		}

		private void RefreshPietyText()
		{
			// 虔诚币槽(CoinNum):服务端权威投影,大数走数值系统缩写。未就绪显 0 缺省占位。
			var attr = GameContext.Instance?.PlayerAttr;
			long piety = attr != null && attr.IsReady ? attr.Piety : 0L;
			if (m_text_CoinNum != null) m_text_CoinNum.text = NumericDisplay.Format(piety);
		}

		private void RefreshBlindBoxText()
		{
			// 盲盒槽(GemNum):元层缓存值(登录快照写入)。
			if (m_text_GemNum != null && _display != null) m_text_GemNum.text = _display.BlindBoxCount.ToString();
		}

		/// <summary>体力倒计时:体力满(≥软上限,不再恢复)隐藏;未满显本周期剩余秒。</summary>
		private void RefreshEnergyCountdown(long now)
		{
			if (_energyCountdown == null || _display == null) return;
			if (_display.Energy >= MergeOrderConfig.EnergyCap)
			{
				_energyCountdown.SetVisible(false);
				return;
			}
			int interval = (int)MergeOrderConfig.RegenIntervalSec;
			int remain = CountdownMath.Remain(now, _display.LastEnergyRegenTime, interval, periodic: true);
			_energyCountdown.SetVisible(true);
			_energyCountdown.SetRemainingSeconds(remain);
		}
	}
}
