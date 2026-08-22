using System.Collections.Generic;
using UnityEngine;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Time;

namespace Vivarium.Unity.Authoring
{
    /// <summary>
    /// Collects authoring assets and converts them into an immutable
    /// <see cref="DefinitionCatalog"/> (§41).
    /// <para>
    /// This is the conversion step in the content pipeline: authoring assets → validation and conversion
    /// → catalog → simulation. Everything downstream of <see cref="Build"/> is Unity-free.
    /// </para>
    /// <para>
    /// Rebuilding the catalog is how hot reload works (§42): a new catalog changes future runtime
    /// entities, while ones already in flight keep the values they snapshotted (§42.1).
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Vivarium/Content Pack", fileName = "ContentPack")]
    public sealed class ContentPackAsset : ScriptableObject
    {
        [Tooltip("Bumped whenever content changes. Recorded in saves and traces (§38, §53).")]
        [SerializeField] private int contentVersion = 1;

        [SerializeField] private TraitDefinitionAsset[] traits = new TraitDefinitionAsset[0];

        [Header("Placeholder inline content")]
        [Tooltip("Needs authored inline until they get their own asset type.")]
        [SerializeField] private NeedEntry[] needs = new NeedEntry[0];

        [SerializeField] private ActivityEntry[] activities = new ActivityEntry[0];

        [SerializeField] private string[] locationKindIds = new string[0];

        [Tooltip("Adds the authored decision/intervention content used by the Unity smoke-test scene.")]
        [SerializeField] private bool includeDemoDecisionContent;

        public int ContentVersion => contentVersion;

        /// <summary>
        /// Builds the catalog. Throws if validation fails, so a broken content pack cannot reach
        /// gameplay (§42).
        /// </summary>
        public DefinitionCatalog Build()
        {
            var builder = new DefinitionCatalog.Builder { ContentVersion = contentVersion };

            for (int i = 0; i < traits.Length; i++)
            {
                if (traits[i] != null)
                {
                    builder.Add(traits[i].ToDefinition());
                }
            }

            for (int i = 0; i < needs.Length; i++)
            {
                builder.Add(needs[i].ToDefinition());
            }

            for (int i = 0; i < activities.Length; i++)
            {
                builder.Add(activities[i].ToDefinition());
            }

            for (int i = 0; i < locationKindIds.Length; i++)
            {
                builder.Add(new LocationKindDefinition(new AuthoredId(locationKindIds[i]), locationKindIds[i]));
            }

            if (includeDemoDecisionContent)
            {
                builder.Add(new DecisionDefinition(
                    new AuthoredId("decision.job_offer"),
                    new[]
                    {
                        new DecisionOption(new AuthoredId("option.accept"), "Take the opportunity", 0),
                        new DecisionOption(new AuthoredId("option.stay"), "Stay where you are", 1),
                    },
                    SimDuration.FromHours(8),
                    new AuthoredId("conflict_scope.employment"),
                    importance: 10));
                builder.Add(new InterventionDefinition(
                    new AuthoredId("intervention.encourage"),
                    InterventionKind.StepDieUp,
                    cost: 1));
            }

            // The system-provided activities the architecture assumes exist (§29.2, invariant 39).
            if (!ContainsActivity(WellKnownActivities.Waiting))
            {
                builder.Add(new ActivityDefinition(WellKnownActivities.Waiting, "Waiting", SimDuration.FromHours(1), false));
            }

            if (!ContainsActivity(WellKnownActivities.Traveling))
            {
                builder.Add(new ActivityDefinition(WellKnownActivities.Traveling, "Traveling", SimDuration.FromMinutes(10), false, false, true));
            }

            DefinitionCatalog catalog = builder.Build();

            IReadOnlyList<string> errors = ContentValidator.Validate(catalog);
            if (errors.Count > 0)
            {
                throw new System.InvalidOperationException(
                    "Content validation failed: " + string.Join("; ", errors));
            }

            return catalog;
        }

        /// <summary>Collects every authoring-time problem without throwing. Used by the editor menu.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var seenIds = new HashSet<string>();

            for (int i = 0; i < traits.Length; i++)
            {
                if (traits[i] == null)
                {
                    problems.Add($"trait slot {i} is empty");
                    continue;
                }

                foreach (string problem in traits[i].Validate())
                {
                    problems.Add(problem);
                }

                if (!seenIds.Add(traits[i].AuthoredId))
                {
                    problems.Add($"duplicate trait id '{traits[i].AuthoredId}'");
                }
            }

            return problems;
        }

        private bool ContainsActivity(AuthoredId id)
        {
            for (int i = 0; i < activities.Length; i++)
            {
                if (activities[i].authoredId == id.Value)
                {
                    return true;
                }
            }

            return false;
        }

        [System.Serializable]
        public struct NeedEntry
        {
            public string authoredId;
            public string displayName;
            public long minValue;
            public long maxValue;
            public long ratePerMinuteNumerator;
            public long ratePerMinuteDenominator;
            public long[] behaviouralThresholds;

            public Domain.Characters.NeedDefinition ToDefinition() => new Domain.Characters.NeedDefinition(
                new AuthoredId(authoredId),
                displayName,
                minValue,
                maxValue,
                ratePerMinuteNumerator,
                ratePerMinuteDenominator <= 0 ? 1 : ratePerMinuteDenominator,
                behaviouralThresholds ?? new long[0]);
        }

        [System.Serializable]
        public struct ActivityEntry
        {
            public string authoredId;
            public string displayName;
            public int defaultDurationMinutes;
            public bool producesOutcome;
            public bool supportsInteractiveResolution;
            public bool isTravel;

            public ActivityDefinition ToDefinition() => new ActivityDefinition(
                new AuthoredId(authoredId),
                displayName,
                SimDuration.FromMinutes(defaultDurationMinutes),
                producesOutcome,
                supportsInteractiveResolution,
                isTravel);
        }
    }
}
