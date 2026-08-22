using System.Collections.Generic;

namespace Vivarium.Application.Queries
{
    public sealed class CharacterRosterEntryView
    {
        public CharacterRosterEntryView(int characterId, string displayName, bool isFollowed)
        {
            CharacterId = characterId;
            DisplayName = displayName;
            IsFollowed = isFollowed;
        }

        public int CharacterId { get; }

        public string DisplayName { get; }

        public bool IsFollowed { get; }
    }

    /// <summary>
    /// Read models the UI binds to (§35).
    /// <para>
    /// UI never binds to mutable Domain entities. It binds to these — immutable snapshots produced at
    /// quiescent boundaries. That matters most because projections <b>incorporate player knowledge</b>:
    /// two players with different knowledge must be able to receive different views of the same
    /// decision (§56).
    /// </para>
    /// </summary>
    public sealed class CharacterProfileView
    {
        public CharacterProfileView(
            int characterId,
            string displayName,
            string currentActivityLabel,
            string locationLabel,
            bool isTraveling,
            string travelOriginLabel,
            int travelProgressBasisPoints,
            bool isFollowed,
            IReadOnlyList<KnownFactView> knownTraits,
            IReadOnlyList<KnownFactView> knownNeeds)
        {
            CharacterId = characterId;
            DisplayName = displayName;
            CurrentActivityLabel = currentActivityLabel;
            LocationLabel = locationLabel;
            IsTraveling = isTraveling;
            TravelOriginLabel = travelOriginLabel;
            TravelProgressBasisPoints = travelProgressBasisPoints;
            IsFollowed = isFollowed;
            KnownTraits = knownTraits;
            KnownNeeds = knownNeeds;
        }

        public int CharacterId { get; }

        public string DisplayName { get; }

        public string CurrentActivityLabel { get; }

        /// <summary>Where they are, or where they are heading while travelling.</summary>
        public string LocationLabel { get; }

        public bool IsTraveling { get; }

        public string TravelOriginLabel { get; }

        public int TravelProgressBasisPoints { get; }

        public bool IsFollowed { get; }

        /// <summary>
        /// Only what the player has learned (§22). Absence here means "not known", which is not the
        /// same as "not true".
        /// </summary>
        public IReadOnlyList<KnownFactView> KnownTraits { get; }

        public IReadOnlyList<KnownFactView> KnownNeeds { get; }
    }

    /// <summary>
    /// One thing the player knows, with the observation's age attached.
    /// <para>
    /// <see cref="ObservedAtLabel"/> exists because knowledge goes stale by design: the value shown may
    /// no longer be true, and the UI should be able to say when it was seen (§22).
    /// </para>
    /// </summary>
    public sealed class KnownFactView
    {
        public KnownFactView(string label, string valueLabel, string observedAtLabel, string confidenceLabel, bool mayBeStale)
        {
            Label = label;
            ValueLabel = valueLabel;
            ObservedAtLabel = observedAtLabel;
            ConfidenceLabel = confidenceLabel;
            MayBeStale = mayBeStale;
        }

        public string Label { get; }

        public string ValueLabel { get; }

        public string ObservedAtLabel { get; }

        public string ConfidenceLabel { get; }

        /// <summary>Whether enough simulated time has passed that this observation may have drifted.</summary>
        public bool MayBeStale { get; }
    }

    /// <summary>
    /// The player-facing view of one influence (§26).
    /// <para>
    /// Every facet is independently nullable, because existence, category, label, and magnitude are
    /// independently controllable. That is what allows the same truth to render as
    /// <c>Fear of disappointing Glen d8</c>, <c>Personal concern d8</c>, <c>??? d8</c>, <c>???</c>, or
    /// nothing at all (invariants 9–10).
    /// </para>
    /// </summary>
    public sealed class InfluenceView
    {
        public InfluenceView(int influenceId, string label, string category, int? dieSides, string explanation, bool canBeIntervenedOn)
        {
            InfluenceId = influenceId;
            Label = label;
            Category = category;
            DieSides = dieSides;
            Explanation = explanation;
            CanBeIntervenedOn = canBeIntervenedOn;
        }

        /// <summary>Stable within-decision id. Interventions target this (§17.2).</summary>
        public int InfluenceId { get; }

        /// <summary>Specific label, or <c>null</c> when the player has not identified it.</summary>
        public string Label { get; }

        /// <summary>Broad category, or <c>null</c> when even that is unknown.</summary>
        public string Category { get; }

        /// <summary>Die size, or <c>null</c> when the magnitude is hidden.</summary>
        public int? DieSides { get; }

        public string Explanation { get; }

        /// <summary>
        /// Whether an intervention control should appear enabled. Computed from the <b>same</b>
        /// authoritative rules the command handler applies (§19, invariant 57).
        /// </summary>
        public bool CanBeIntervenedOn { get; }
    }

    /// <summary>One option and the influences the player can see arguing for it (§17, §26).</summary>
    public sealed class DecisionOptionView
    {
        public DecisionOptionView(string optionId, string label, IReadOnlyList<InfluenceView> influences)
        {
            OptionId = optionId;
            Label = label;
            Influences = influences;
        }

        public string OptionId { get; }

        public string Label { get; }

        /// <summary>
        /// Visible influences only. The number of hidden influences is deliberately not exposed —
        /// not even as a count (§26).
        /// </summary>
        public IReadOnlyList<InfluenceView> Influences { get; }
    }

    /// <summary>The player-facing decision encounter (§17, §26, §35).</summary>
    public sealed class DecisionView
    {
        public DecisionView(
            int decisionId,
            int characterId,
            string characterName,
            string definitionId,
            string statusLabel,
            string resolveAtLabel,
            int influenceRevision,
            bool isHeld,
            bool canBeHeld,
            IReadOnlyList<DecisionOptionView> options,
            DecisionResolutionView resolution)
        {
            DecisionId = decisionId;
            CharacterId = characterId;
            CharacterName = characterName;
            DefinitionId = definitionId;
            StatusLabel = statusLabel;
            ResolveAtLabel = resolveAtLabel;
            InfluenceRevision = influenceRevision;
            IsHeld = isHeld;
            CanBeHeld = canBeHeld;
            Options = options;
            Resolution = resolution;
        }

        public int DecisionId { get; }

        public int CharacterId { get; }

        public string CharacterName { get; }

        public string DefinitionId { get; }

        public string StatusLabel { get; }

        public string ResolveAtLabel { get; }

        /// <summary>
        /// Changes whenever the true influence set changes. UI can use it to detect that an open
        /// decision has evolved since it last rendered (§17.2).
        /// </summary>
        public int InfluenceRevision { get; }

        public bool IsHeld { get; }

        public bool CanBeHeld { get; }

        public IReadOnlyList<DecisionOptionView> Options { get; }

        /// <summary><c>null</c> while unresolved.</summary>
        public DecisionResolutionView Resolution { get; }
    }

    /// <summary>The outcome, once there is one (§18).</summary>
    public sealed class DecisionResolutionView
    {
        public DecisionResolutionView(string chosenOptionId, string degreeLabel, string resolvedAtLabel, string outcomeSourceLabel)
        {
            ChosenOptionId = chosenOptionId;
            DegreeLabel = degreeLabel;
            ResolvedAtLabel = resolvedAtLabel;
            OutcomeSourceLabel = outcomeSourceLabel;
        }

        public string ChosenOptionId { get; }

        public string DegreeLabel { get; }

        public string ResolvedAtLabel { get; }

        public string OutcomeSourceLabel { get; }
    }

    /// <summary>A location and who the player can see in it (§30, §35).</summary>
    public sealed class LocationView
    {
        public LocationView(
            int locationId,
            string displayName,
            string locationKindId,
            int directOccupantCount,
            int occupantsWithinCount,
            IReadOnlyList<int> childLocationIds)
        {
            LocationId = locationId;
            DisplayName = displayName;
            LocationKindId = locationKindId;
            DirectOccupantCount = directOccupantCount;
            OccupantsWithinCount = occupantsWithinCount;
            ChildLocationIds = childLocationIds;
        }

        public int LocationId { get; }

        public string DisplayName { get; }

        public string LocationKindId { get; }

        /// <summary>Standing here. Excludes travellers (§30).</summary>
        public int DirectOccupantCount { get; }

        /// <summary>Anywhere beneath here.</summary>
        public int OccupantsWithinCount { get; }

        public IReadOnlyList<int> ChildLocationIds { get; }
    }

    /// <summary>A character's upcoming commitments (§29.3, §29.4).</summary>
    public sealed class ScheduleView
    {
        public ScheduleView(int characterId, IReadOnlyList<ScheduleEntryView> entries)
        {
            CharacterId = characterId;
            Entries = entries;
        }

        public int CharacterId { get; }

        /// <summary>
        /// Only the materialized planning horizon. Asking for a longer view is what causes the planner
        /// to materialize further ahead — the calendar is never eagerly infinite (§29.4).
        /// </summary>
        public IReadOnlyList<ScheduleEntryView> Entries { get; }
    }

    /// <summary>One planned commitment.</summary>
    public sealed class ScheduleEntryView
    {
        public ScheduleEntryView(int commitmentId, string kind, string startLabel, string durationLabel, string locationLabel, string statusLabel, bool conflicts)
        {
            CommitmentId = commitmentId;
            Kind = kind;
            StartLabel = startLabel;
            DurationLabel = durationLabel;
            LocationLabel = locationLabel;
            StatusLabel = statusLabel;
            Conflicts = conflicts;
        }

        public int CommitmentId { get; }

        public string Kind { get; }

        public string StartLabel { get; }

        public string DurationLabel { get; }

        public string LocationLabel { get; }

        public string StatusLabel { get; }

        /// <summary>Whether this overlaps another planned commitment (§29.3).</summary>
        public bool Conflicts { get; }
    }

    /// <summary>The feed of decisions worth surfacing, ordered by attention policy (§20, §36).</summary>
    public sealed class DecisionFeedView
    {
        public DecisionFeedView(IReadOnlyList<DecisionFeedEntryView> entries, int heldCount, int heldCapacity)
        {
            Entries = entries;
            HeldCount = heldCount;
            HeldCapacity = heldCapacity;
        }

        public IReadOnlyList<DecisionFeedEntryView> Entries { get; }

        public int HeldCount { get; }

        public int HeldCapacity { get; }
    }

    /// <summary>One entry in the decision feed.</summary>
    public sealed class DecisionFeedEntryView
    {
        public DecisionFeedEntryView(int decisionId, int characterId, string characterName, string definitionId, string resolveAtLabel, bool isHeld, int importance)
        {
            DecisionId = decisionId;
            CharacterId = characterId;
            CharacterName = characterName;
            DefinitionId = definitionId;
            ResolveAtLabel = resolveAtLabel;
            IsHeld = isHeld;
            Importance = importance;
        }

        public int DecisionId { get; }

        public int CharacterId { get; }

        public string CharacterName { get; }

        public string DefinitionId { get; }

        public string ResolveAtLabel { get; }

        public bool IsHeld { get; }

        public int Importance { get; }
    }
}
