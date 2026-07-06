using TEngine;

namespace GameLogic
{
	/// <summary>
	/// 主菜单窗。视觉为设计稿成品切图(资源数值/等级为切图内示例值,接数据时换无字底图并启用隐藏文本节点);
	/// 按钮经 UIMainMenuPanel_Gen.g.cs 的 [SerializeField] 绑定,回调体待接业务。
	/// </summary>
	[Window(UILayer.UI, location : "UIMainMenuPanel")]
	public partial class UIMainMenuPanel
	{
		#region 事件
		

		private partial void OnClick_FindImageBtn()
		{
            GameModule.UI.ShowUIAsync<UIMergeOrderPanel>();
			
		}

		private partial void OnClick_CollapseMenuButtonBtn()
		{
			
		}

		private partial void OnClick_PersonalInformationButtonBtn()
		{
			
		}

		private partial void OnClick_CheckInButtonBtn()
		{
			
		}

		private partial void OnClick_DailyDivinationButtonBtn()
		{
			
		}

		private partial void OnClick_InviteCourtesyButtonBtn()
		{
			
		}

		private partial void OnClick_TarotButtonBtn()
		{
			
		}

		private partial void OnClick_WooCommerceButtonBtn()
		{
			
		}

		private partial void OnClick_LimitedTimeEventButtonBtn()
		{
			
		}

		private partial void OnClick_StarterCelebrationButtonBtn()
		{
			
		}

		private partial void OnClick_SpecialPoolButtonBtn()
		{
			
		}

		private partial void OnClick_BackpackButtonBtn()
		{
			
		}

		private partial void OnClick_TaskButtonBtn()
		{
			
		}
		#endregion

		
		
	}
}
