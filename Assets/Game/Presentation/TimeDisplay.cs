using UnityEngine;
using TMPro;
using Vivarium.Application.Queries;

namespace Vivarium.Unity.Presentation
{
    public sealed class TimeDisplay : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI timeText;

        public string DisplayedText => timeText == null ? string.Empty : timeText.text;

        public void Apply(SimulationStatusView view)
        {
            if (timeText == null || view == null)
            {
                return;
            }

            string offline = view.IsOfflineReturn && !string.IsNullOrEmpty(view.OfflineElapsedLabel)
                ? $" · {view.OfflineElapsedLabel}"
                : string.Empty;
            timeText.text = $"{view.TimeLabel} · {view.StatusLabel} · {view.SpeedLabel}{offline}";
        }
    }
}
