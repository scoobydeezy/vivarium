using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vivarium.Application.Queries;
using Vivarium.Domain.Common;

namespace Vivarium.Unity.Presentation
{
    public sealed class CharacterRosterEntry : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI label;

        private CharacterId _characterId;
        private System.Action<CharacterId> _toggle;

        public void Bind(CharacterRosterEntryView view, System.Action<CharacterId> toggle)
        {
            _characterId = new CharacterId(view.CharacterId);
            _toggle = toggle;
            label.text = $"{(view.IsFollowed ? "ON" : "OFF")}  {view.DisplayName}";
            background.color = view.IsFollowed
                ? new Color(0.08f, 0.42f, 0.32f, 0.95f)
                : new Color(0.22f, 0.22f, 0.22f, 0.95f);
            button.onClick.RemoveListener(InvokeToggle);
            button.onClick.AddListener(InvokeToggle);
        }

        private void InvokeToggle() => _toggle?.Invoke(_characterId);
    }
}
