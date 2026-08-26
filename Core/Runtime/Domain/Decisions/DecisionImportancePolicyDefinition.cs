using System;
using Vivarium.Domain.Evaluation;

namespace Vivarium.Domain.Decisions
{
    /// <summary>Catalog-owned tunable gates over the shared derived-Importance scale.</summary>
    public sealed class DecisionImportancePolicyDefinition
    {
        public DecisionImportancePolicyDefinition(
            int admissionFloor,
            int prioritizedFeedFloor,
            int normalFeedFloor,
            int autoHoldFloor)
        {
            Validate(admissionFloor, nameof(admissionFloor));
            Validate(prioritizedFeedFloor, nameof(prioritizedFeedFloor));
            Validate(normalFeedFloor, nameof(normalFeedFloor));
            Validate(autoHoldFloor, nameof(autoHoldFloor));
            if (admissionFloor > prioritizedFeedFloor ||
                prioritizedFeedFloor > normalFeedFloor ||
                normalFeedFloor > autoHoldFloor)
                throw new ArgumentException(
                    "Importance floors must satisfy Admission <= PrioritizedFeed <= NormalFeed <= AutoHold.");
            AdmissionFloor = admissionFloor;
            PrioritizedFeedFloor = prioritizedFeedFloor;
            NormalFeedFloor = normalFeedFloor;
            AutoHoldFloor = autoHoldFloor;
        }

        public int AdmissionFloor { get; }
        public int PrioritizedFeedFloor { get; }
        public int NormalFeedFloor { get; }
        public int AutoHoldFloor { get; }

        private static void Validate(int value, string parameterName)
        {
            if (value < 0 || value > SignalNumeric.Scale)
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
