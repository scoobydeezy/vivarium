using System;
using Vivarium.Domain.Evaluation;

namespace Vivarium.Domain.Decisions
{
    /// <summary>Catalog-owned tunable gates over the shared derived-Importance scale.</summary>
    public sealed class DecisionImportancePolicyDefinition
    {
        public DecisionImportancePolicyDefinition(int admissionFloor)
        {
            if (admissionFloor < 0 || admissionFloor > SignalNumeric.Scale)
                throw new ArgumentOutOfRangeException(nameof(admissionFloor));
            AdmissionFloor = admissionFloor;
        }

        public int AdmissionFloor { get; }
    }
}
