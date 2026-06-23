using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace GameLogic
{
    public class MToggle : MonoBehaviour
    {
        public Toggle toggle;
        public TextMeshProUGUI title;
        public Color defaultColor;
        public Color SelectColor;
        private void Start()
        {
            toggle.onValueChanged.AddListener((v) =>
            {
                title.color = v ? SelectColor : defaultColor;
            });
        }
    }
}
