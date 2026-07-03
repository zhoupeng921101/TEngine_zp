using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    [Window(UILayer.System, fromResources: true)]
    class UILogPanel : UIPanelMono
    {
        private readonly Stack<string> _errorTextString = new Stack<string>();

        #region 引用（Inspector 拖拽，[SerializeField]）

        [SerializeField] private Text m_textError;
        [SerializeField] private Button m_btnClose;

        protected override void ScriptGenerator()
        {
            // 引用由 [SerializeField] 在 Inspector 拖入就位（原 FindChild 路径对照，便于校核拖线）：
            //   m_textError  m_textError
            //   m_btnClose   m_btnClose
            m_btnClose.onClick.AddListener(OnClickCloseBtn);
        }

        #endregion

        #region 事件

        private void OnClickCloseBtn()
        {
            PopErrorLog().Forget();
        }

        #endregion

        protected override void OnRefresh()
        {
            _errorTextString.Push(UserData.ToString());
            m_textError.text = UserData.ToString();
        }

        private async UniTaskVoid PopErrorLog()
        {
            if (_errorTextString.Count <= 0)
            {
                await UniTask.Yield();
                Close();
                return;
            }

            string error = _errorTextString.Pop();
            m_textError.text = error;
        }
    }
}
