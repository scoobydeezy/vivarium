using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vivarium.Application.Queries;

namespace Vivarium.Unity.Presentation
{
    public sealed class CharacterProfilePanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI summaryText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button travelButton;

        private System.Action _close;
        private System.Action _travel;

        public void Configure(System.Action close, System.Action travel)
        {
            _close = close;
            _travel = travel;
            closeButton.onClick.RemoveListener(InvokeClose);
            travelButton.onClick.RemoveListener(InvokeTravel);
            closeButton.onClick.AddListener(InvokeClose);
            travelButton.onClick.AddListener(InvokeTravel);
            ShowPrompt("Click a character to inspect");
        }

        public void Apply(CharacterProfileView profile)
        {
            string needs = "Needs: not yet observed";
            if (profile.KnownNeeds.Count > 0)
            {
                needs = "Needs:";
                for (int i = 0; i < profile.KnownNeeds.Count; i++)
                {
                    KnownFactView need = profile.KnownNeeds[i];
                    string stale = need.MayBeStale ? " (possibly stale)" : string.Empty;
                    needs += $"\n  {need.Label}: {need.ValueLabel} observed {need.ObservedAtLabel}{stale}";
                }
            }

            summaryText.text =
                $"{profile.DisplayName}\n" +
                $"Activity: {profile.CurrentActivityLabel}\n" +
                $"Location: {profile.LocationLabel}\n" +
                needs;
            closeButton.gameObject.SetActive(true);
            travelButton.gameObject.SetActive(!profile.IsTraveling);
        }

        public void ShowPrompt(string message)
        {
            summaryText.text = message;
            closeButton.gameObject.SetActive(false);
            travelButton.gameObject.SetActive(false);
        }

        private void InvokeClose() => _close?.Invoke();

        private void InvokeTravel() => _travel?.Invoke();
    }
}
