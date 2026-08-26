using System.Collections.Generic;
using UnityEngine;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Time;

namespace Vivarium.Unity.Authoring
{
    /// <summary>Designer-facing authoring asset for one Decision intervention definition.</summary>
    [CreateAssetMenu(menuName = "Vivarium/Intervention Definition", fileName = "intervention_")]
    public sealed class InterventionDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string authoredId = "intervention.";
        [SerializeField] private InterventionKind kind;
        [SerializeField] private int cost;
        [SerializeField] private InterventionResourceKind resourceKind;
        [SerializeField] private int replacementDieSides;
        [SerializeField] private int fixedResult;
        [SerializeField] private int initialBalance;
        [SerializeField] private int availabilityCap;
        [SerializeField] private int refreshAmount;
        [SerializeField] private long refreshPeriodMinutes;

        public string AuthoredId => authoredId;

        public InterventionDefinition ToDefinition() => new InterventionDefinition(
            new AuthoredId(authoredId),
            kind,
            cost,
            replacementDie: new Die(replacementDieSides, fixedResult),
            resourceKind: resourceKind,
            resourcePolicy: availabilityCap > 0
                ? new Vivarium.Domain.PlayerAgency.InterventionResourcePolicy(
                    initialBalance,
                    availabilityCap,
                    refreshAmount,
                    new SimDuration(refreshPeriodMinutes))
                : default);

        public List<string> Validate()
        {
            var problems = new List<string>();
            if (string.IsNullOrEmpty(authoredId) || authoredId.EndsWith("."))
                problems.Add($"{name}: authored id '{authoredId}' is incomplete.");
            if (!authoredId.StartsWith("intervention."))
                problems.Add($"{name}: Intervention ids should be namespaced 'intervention.<something>'.");
            try { ToDefinition(); }
            catch (System.Exception error) { problems.Add($"{name}: {error.Message}"); }
            return problems;
        }
    }
}
