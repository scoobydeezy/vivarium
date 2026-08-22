using UnityEngine;
using TMPro;
using Vivarium.Domain.Time;

namespace Vivarium.Unity.Presentation
{
    public sealed class TimeDisplay : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI timeText;

        public void SetTime(SimTime time)
        {
            if (timeText == null)
            {
                return;
            }

            long day = time.TotalMinutes / 1440;
            long hour = (time.TotalMinutes % 1440) / 60;
            long minute = time.TotalMinutes % 60;
            timeText.text = $"Day {day} {hour:00}:{minute:00}";
        }
    }
}
