using System.Collections.Generic;

namespace Vivarium.Application.Persistence
{
    /// <summary>
    /// Explicitly versioned save DTOs (§38).
    /// <para>
    /// Runtime Domain objects are never serialized directly. These are flat, format-agnostic data
    /// classes: no behaviour, no Unity references, and definition references held as stable authored id
    /// strings rather than object links (§39).
    /// </para>
    /// <para>
    /// Everything reconstructible is deliberately absent — occupancy indexes, membership indexes,
    /// decision dependency indexes, ancestor caches. Those are rebuilt and validated on load (§40).
    /// The scheduler and active Activities/Commitments are <b>not</b> in that category: they are
    /// authoritative state and are persisted (invariant 59).
    /// </para>
    /// </summary>
    public sealed class SaveGameData
    {
        /// <summary>
        /// The current persisted shape. Bump on any structural change and add a migration (§39).
        /// </summary>
        public const int CurrentSchemaVersion = 10;

        /// <summary>Determines whether the persisted shape can be understood or migrated (§39.1).</summary>
        public int SchemaVersion = CurrentSchemaVersion;

        /// <summary>
        /// Compatibility and diagnostics metadata. A mismatch is <b>not</b> automatically a load
        /// blocker — support policy decides loadability (§39.1, invariant 62).
        /// </summary>
        public int ContentVersion;

        public int SimulationRulesVersion;

        public int RandomAlgorithmVersion;

        public long WorldSeed;

        /// <summary>Authoritative simulation clock, in whole simulation minutes (§9).</summary>
        public long ClockMinutes;

        /// <summary>
        /// Real-world instant the save was taken, in UTC ticks. Application/Infrastructure subtracts
        /// this from <c>IRealWorldClock</c> to derive offline elapsed duration — the Domain never reads
        /// the wall clock (§21, §38).
        /// </summary>
        public long SavedAtRealTimeUtcTicks;

        /// <summary>Last external command sequence issued, so ingress numbering continues (§2.2.1).</summary>
        public long LastCommandSequence;

        public RuntimeIdCountersData RuntimeIdCounters = new RuntimeIdCountersData();

        public SchedulerData Scheduler = new SchedulerData();

        /// <summary>
        /// Aspect-scoped revision counters (§11.2.1).
        /// <para>
        /// These must be persisted, not rebuilt: pending events record the revisions they expected, so
        /// restoring the counters to zero would make every saved event look stale and silently discard
        /// the world's entire future.
        /// </para>
        /// </summary>
        public List<RevisionData> Revisions = new List<RevisionData>();

        public List<CharacterData> Characters = new List<CharacterData>();

        public List<ActivityData> Activities = new List<ActivityData>();

        public List<CommitmentData> Commitments = new List<CommitmentData>();

        public List<LocationData> Locations = new List<LocationData>();

        public List<TravelConnectionData> TravelConnections = new List<TravelConnectionData>();

        public List<GroupData> Groups = new List<GroupData>();

        public List<GroupMembershipData> GroupMemberships = new List<GroupMembershipData>();

        public List<EmploymentData> Employments = new List<EmploymentData>();

        public List<RelationshipData> Relationships = new List<RelationshipData>();

        public List<DecisionData> Decisions = new List<DecisionData>();

        public List<KnowledgeEntryData> Knowledge = new List<KnowledgeEntryData>();

        public List<SocialBeliefData> SocialBeliefs = new List<SocialBeliefData>();

        public AttentionData Attention = new AttentionData();

        /// <summary>Only Significant and Legacy tiers are persisted (§37).</summary>
        public List<HistoryEntryData> SignificantHistory = new List<HistoryEntryData>();
    }

    /// <summary>Allocator counters. Ids are never reused, so these only ever move forward (§7.1).</summary>
    public sealed class RuntimeIdCountersData
    {
        public int Characters;
        public int Activities;
        public int Commitments;
        public int CommitmentOutcomes;
        public int Relationships;
        public int Decisions;
        public int Locations;
        public int Groups;
        public int Employments;
        public int ScheduledEvents;
        public int HistoryEntries;

        /// <summary>Scheduler tie-break counter — distinct from the command sequence (§11, §34).</summary>
        public long EventSequence;
    }

    /// <summary>Flattened <c>AnalyticalProgression</c> (§10.1).</summary>
    public sealed class ProgressionData
    {
        public long ValueAtAnchor;
        public long AnchoredAtMinutes;
        public long RateNumerator;
        public long RateDenominator = 1;
        public long MinValue;
        public long MaxValue;
    }

    public sealed class CharacterData
    {
        public int Id;
        public string DisplayName;
        public long CreatedAtMinutes;
        public bool IsActive;
        public long RetiredAtMinutes = -1;
        public int CurrentActivityId;
        public List<string> Traits = new List<string>();
        public List<NeedData> Needs = new List<NeedData>();
        public List<AuthoredLongData> Personality = new List<AuthoredLongData>();
        public int PersonalityRevision;
        public List<AuthoredLongData> Values = new List<AuthoredLongData>();
        public int ValuesRevision;
        public List<AuthoredLongData> Interests = new List<AuthoredLongData>();
        public int InterestsRevision;
        public List<AffectData> Affect = new List<AffectData>();
        public List<AppraisalFieldData> AppraisalFields = new List<AppraisalFieldData>();
    }

    public sealed class NeedData
    {
        public string NeedId;
        public ProgressionData Progression = new ProgressionData();
        public long BehaviouralThreshold;

        /// <summary>The pending threshold-crossing event, revalidated on load (§10.2).</summary>
        public int PendingThresholdEventId;
    }

    /// <summary>
    /// An active or completed Activity. Active Traveling route/timing parameters must round-trip
    /// exactly (§38, §40).
    /// </summary>
    public sealed class ActivityData
    {
        public int Id;
        public int CharacterId;
        public string DefinitionId;
        public long StartedAtMinutes;
        public int Status;
        public int SourceCommitmentId;
        public int PendingCompletionEventId;

        /// <summary>0 = Located, 1 = Traveling (§29.1).</summary>
        public int SpatialKind;

        public int LocationId;
        public int TransitOriginLocationId;
        public int TransitDestinationLocationId;
        public long TransitDepartedAtMinutes;
        public long TransitArrivesAtMinutes;
        public string TransitTravelModeId;
        public int TransitTravelPlanId;

        public ProgressionData Progress = new ProgressionData();
        public ProgressionData Performance = new ProgressionData();

        public bool HasAcceptedResult;
        public int ResultGrade;
        public long ResultMagnitude;
        public int ResultSource;
        public string ResultOutcomeId;

        /// <summary>Definition-derived values snapshotted at construction (§42.1).</summary>
        public List<AuthoredLongData> CommittedParameters = new List<AuthoredLongData>();

        public List<ActivityModifierData> ActiveModifiers = new List<ActivityModifierData>();
    }

    public sealed class ActivityModifierData
    {
        public string ModifierId;
        public long AppliedAtMinutes;
        public long RateNumerator;
        public long RateDenominator = 1;
        public int CauseEntityKind;
        public int CauseRuntimeId;
    }

    public sealed class CommitmentData
    {
        public int Id;
        public int CharacterId;
        public string Kind;
        public long EarliestStartMinutes;
        public long LatestStartMinutes;
        public long ExpectedDurationMinutes;
        public int LocationId;
        public int Priority;
        public string ActivityDefinitionId;
        public int SourceEntityKind;
        public int SourceRuntimeId;
        public string SourceTemplateId;
        public int Status;
        public int FulfillingActivityId;
        public List<int> AdditionalParticipants = new List<int>();
        public List<CommitmentStakeholderData> Stakeholders = new List<CommitmentStakeholderData>();
        public bool HasStakeholderSnapshot;
        public CommitmentAccountabilityPolicyData AccountabilityPolicy;
    }

    public sealed class EmploymentData
    {
        public int Id;
        public int EmployeeId;
        public int EmployerGroupId;
        public string DefinitionId;
        public string RoleId;
        public int WorkLocationId;
        public int SupervisorId;
        public List<EmploymentObligationPatternData> ObligationPatterns = new List<EmploymentObligationPatternData>();
    }

    public sealed class EmploymentObligationPatternData
    {
        public string Id;
        public string CommitmentKind;
        public int CycleLengthDays;
        public int ActiveDaysMask;
        public int StartMinuteOfDay;
        public long DurationMinutes;
        public int Priority;
        public string ActivityDefinitionId;
        public long StartWindowMinutes;
        public CommitmentAccountabilityPolicyData AccountabilityPolicy;
    }

    public sealed class CommitmentStakeholderData
    {
        public int EntityKind;
        public int RuntimeId;
        public int Role;
    }

    public sealed class CommitmentAccountabilityPolicyData
    {
        public string Id;
        public CommitmentConsequenceSetData Default;
        public List<CommitmentOutcomeConsequenceData> ByOutcome = new List<CommitmentOutcomeConsequenceData>();
        public List<CommitmentRoleConsequenceData> ByRole = new List<CommitmentRoleConsequenceData>();
        public List<CommitmentAccountabilityOverrideData> SpecificOverrides = new List<CommitmentAccountabilityOverrideData>();
    }

    public sealed class CommitmentConsequenceSetData
    {
        public string MemoryKind;
        public string MemoryExplanationId;
        public int MemoryRetentionTier;
        public string EvidenceActionId;
        public List<AuthoredLongData> ChannelDeltas = new List<AuthoredLongData>();
    }

    public sealed class CommitmentOutcomeConsequenceData
    {
        public int Outcome;
        public CommitmentConsequenceSetData Consequences;
    }

    public sealed class CommitmentRoleConsequenceData
    {
        public int Role;
        public CommitmentConsequenceSetData Consequences;
    }

    public sealed class CommitmentAccountabilityOverrideData
    {
        public int Outcome;
        public int Role;
        public bool HasPerceivedCause;
        public int PerceivedCause;
        public CommitmentConsequenceSetData Consequences;
    }

    public sealed class LocationData
    {
        public int Id;
        public int ParentLocationId;
        public string LocationKindId;
        public string DisplayName;
        public bool IsOccupiable;
        public int Capacity;
        public List<string> ActivityAffordances = new List<string>();
    }

    public sealed class TravelConnectionData
    {
        public int FromLocationId;
        public int ToLocationId;
        public long CostMinutes;
        public string TravelModeId;
    }

    public sealed class GroupData
    {
        public int Id;
        public string Kind;
        public string DisplayName;
        public int PrimaryLocationId;
    }

    public sealed class GroupMembershipData
    {
        public int GroupId;
        public int CharacterId;
    }

    public sealed class RelationshipData
    {
        public int Id;
        public int LowCharacterId;
        public int HighCharacterId;
        public string Kind;
        // Schema-v1 migration inputs only. Schema-v2 writers leave these at their defaults.
        public ProgressionData Affinity = new ProgressionData();
        public int Familiarity;
        public long EstablishedAtMinutes;
        public long LastInteractionAtMinutes = -1;
        public bool IsActive;
        public DirectionalRelationshipData LowToHigh = new DirectionalRelationshipData();
        public DirectionalRelationshipData HighToLow = new DirectionalRelationshipData();
    }

    /// <summary>
    /// An active or recently resolved Decision. Influences carry their stable within-decision ids so
    /// interventions stay bound across a reload (§17.2, invariant 37).
    /// </summary>
    public sealed class DecisionData
    {
        public int Id;
        public int CharacterId;
        public string DefinitionId;
        public long CreatedAtMinutes;
        public long ResolveAtMinutes;
        public int Status;
        public int Importance;
        public int InfluenceRevision;
        public int PendingResolveEventId;
        public int ResolutionHistoryEntryId;
        public string ConflictScopeKind;
        public int ConflictScopeEntityKind;
        public int ConflictScopeRuntimeId;

        public List<DecisionOptionData> Options = new List<DecisionOptionData>();
        public List<DecisionInfluenceData> Influences = new List<DecisionInfluenceData>();
        public List<AppliedInterventionData> Interventions = new List<AppliedInterventionData>();
        public List<DependencyKeyData> DependencyKeys = new List<DependencyKeyData>();
        public List<AuthoredLongData> SnapshottedParameters = new List<AuthoredLongData>();
        public List<DecisionParameterData> ContextParameters = new List<DecisionParameterData>();
        public DecisionReasoningProgramData ReasoningProgram;
        public bool HasCommitmentConflict;
        public int ConflictInstanceRevision;
        public long LatestResolutionAtMinutes;
        public List<int> ConflictCommitmentIds = new List<int>();

        public bool HasResolution;
        public string ResolvedOptionId;
        public int ResolvedDegree;
        public long ResolvedAtMinutes;
        public int ResolutionSource;
        public List<OptionTotalData> OptionTotals = new List<OptionTotalData>();
        public List<InfluenceRollData> Rolls = new List<InfluenceRollData>();
    }

    public sealed class DecisionOptionData
    {
        public string Id;
        public string LabelId;
        public int OrderIndex;
        public List<DecisionParameterData> Context = new List<DecisionParameterData>();
        public CommitmentResolutionPlanData CommitmentResolutionPlan;
    }

    public sealed class CommitmentResolutionPlanData
    {
        public string PlanId;
        public List<int> Preserve = new List<int>();
        public List<int> Defer = new List<int>();
        public List<int> Relinquish = new List<int>();
    }

    public sealed class DecisionParameterData
    {
        public string Key;
        public int Kind;
        public long Integer;
        public string AuthoredId;
        public int EntityKind;
        public int RuntimeId;
    }

    public sealed class DecisionReasoningProgramData
    {
        public List<CompiledConsiderationBindingData> Bindings = new List<CompiledConsiderationBindingData>();
    }

    public sealed class CompiledConsiderationBindingData
    {
        public string BindingId;
        public string ConsiderationId;
        public int DefinitionVersion;
        public List<ConsiderationParameterData> ParameterSchema = new List<ConsiderationParameterData>();
        public List<CompiledParameterBindingData> ParameterBindings = new List<CompiledParameterBindingData>();
        public List<DecisionSignalRequestData> Signals = new List<DecisionSignalRequestData>();
        public SignalFieldDefinitionData Field = new SignalFieldDefinitionData();
        public string ReasonChannelId;
        public int ConsolidationPolicy;
        public string ScaleId;
        public List<ReasonDieThresholdData> ScaleThresholds = new List<ReasonDieThresholdData>();
        public string CategoryId;
        public string PositiveLabelId;
        public string NegativeLabelId;
        public int Visibility;
    }

    public sealed class ConsiderationParameterData
    {
        public string Id;
        public int Kind;
        public bool Required;
    }

    public sealed class CompiledParameterBindingData
    {
        public string ParameterId;
        public int Source;
        public string SourceParameterId;
        public DecisionParameterData Literal = new DecisionParameterData();
    }

    public sealed class DecisionSignalRequestData
    {
        public string SignalId;
        public string ProviderId;
    }

    public sealed class SignalFieldDefinitionData
    {
        public string Id;
        public long Bias;
        public int Revision;
        public List<SignalLinearTermData> LinearTerms = new List<SignalLinearTermData>();
        public List<SignalPairwiseTermData> PairwiseTerms = new List<SignalPairwiseTermData>();
        public List<AuthoredLongData> IdealPoint = new List<AuthoredLongData>();
        public List<SignalIdealFactorData> IdealFactors = new List<SignalIdealFactorData>();
    }

    public sealed class SignalLinearTermData
    {
        public string Signal;
        public long Coefficient;
        public string Provenance;
    }

    public sealed class SignalPairwiseTermData
    {
        public string First;
        public string Second;
        public long Coefficient;
        public string Provenance;
    }

    public sealed class SignalIdealFactorData
    {
        public string Id;
        public string Provenance;
        public List<SignalLinearTermData> Coefficients = new List<SignalLinearTermData>();
    }

    public sealed class ReasonDieThresholdData
    {
        public long MinimumMagnitude;
        public int DieSides;
    }

    public sealed class DecisionInfluenceData
    {
        public int Id;
        public string OptionId;
        public string Category;
        public string LabelId;
        public int BaseDieSides;
        public int CurrentDieSides;
        public int Visibility;
        public int RollIndex;
        public bool IsRetracted;
        public string DependencyContextKind;
        public int DependencyEntityKind;
        public int DependencyRuntimeId;
        public int SubjectEntityKind;
        public int SubjectRuntimeId;
        public int Polarity;
        public string ReasonChannelId;
        public string ReasonBindingId;
        public DecisionReasonEvaluationData Evaluation = new DecisionReasonEvaluationData();
    }

    public sealed class AppliedInterventionData
    {
        public string InterventionDefinitionId;
        public int TargetInfluenceId;
        public long CommandSequence;
        public int Kind = -1;
        public int ReplacementDieSides;
    }

    public sealed class DependencyKeyData
    {
        public string ContextKind;
        public int SubjectEntityKind;
        public int SubjectRuntimeId;
    }

    public sealed class OptionTotalData
    {
        public string OptionId;
        public int Total;
        public int OrderIndex;
    }

    public sealed class InfluenceRollData
    {
        public int InfluenceId;
        public string OptionId;
        public int DieSides;
        public int Rolled;
        public int RollIndex;
        public int Polarity;
        public FrozenDecisionReasonData Reason;
    }

    public sealed class DecisionReasonEvaluationData
    {
        public long ExpectedScore;
        public long OutputVariance;
        public List<DecisionSignalEvidenceData> Signals = new List<DecisionSignalEvidenceData>();
        public List<DecisionContributionEvidenceData> Contributions = new List<DecisionContributionEvidenceData>();
    }

    public sealed class DecisionSignalEvidenceData
    {
        public string SignalId;
        public long Mean;
        public long Variance;
        public int Applicability;
        public int SourceRevision;
    }

    public sealed class DecisionContributionEvidenceData
    {
        public int Kind;
        public string SourceId;
        public long Amount;
    }

    public sealed class FrozenDecisionReasonData
    {
        public string CategoryId;
        public string LabelId;
        public string ReasonChannelId;
        public string BindingId;
        public int SubjectEntityKind;
        public int SubjectRuntimeId;
        public int Visibility;
        public DecisionReasonEvaluationData Evaluation = new DecisionReasonEvaluationData();
    }

    /// <summary>
    /// Player knowledge — not derivable from truth, so always persisted (§22).
    /// <see cref="SourceHistoryEntryId"/> is a weak reference and may dangle after pruning (§23.1).
    /// </summary>
    public sealed class KnowledgeEntryData
    {
        public int ObserverKind;
        public int ObserverCharacterId;
        public string FactKind;
        public int SubjectEntityKind;
        public int SubjectRuntimeId;
        public string Qualifier;
        public string ObservedBand;
        public long ObservedMagnitude;
        public bool HasObservedMagnitude;
        public long ObservedAtMinutes;
        public int Confidence;
        public string SourceChannelId;
        public int InformantEntityKind;
        public int InformantRuntimeId;
        public int SourceHistoryEntryId;
        public int SourceOutcomeId;
    }

    /// <summary>
    /// Durable attention state only. Ephemeral watch flags — visibility, selection — are presentation
    /// state and are deliberately not saved (§8, §20.1).
    /// </summary>
    public sealed class AttentionData
    {
        public List<int> FollowedCharacters = new List<int>();
        public List<CharacterPolicyData> CharacterPolicies = new List<CharacterPolicyData>();
        public List<DecisionPolicyData> DecisionPolicies = new List<DecisionPolicyData>();
        public List<int> HeldDecisions = new List<int>();
        public List<ObservationOrdinalData> ObservationOrdinals = new List<ObservationOrdinalData>();
    }

    public sealed class CharacterPolicyData
    {
        public int CharacterId;
        public int Policy;
    }

    public sealed class DecisionPolicyData
    {
        public int DecisionId;
        public int Policy;
    }

    public sealed class ObservationOrdinalData
    {
        public int CharacterId;
        public int Ordinal;
    }

    public sealed class HistoryEntryData
    {
        public int Id;
        public string Kind;
        public long OccurredAtMinutes;
        public int Tier;
        public string Summary;
        public List<EntityRefData> Subjects = new List<EntityRefData>();
        public int SourceOutcomeId;
    }

    public sealed class EntityRefData
    {
        public int EntityKind;
        public int RuntimeId;
    }

    public sealed class AuthoredLongData
    {
        public string Key;
        public long Value;
    }

    /// <summary>
    /// Pending scheduled work. Authoritative state, persisted rather than rebuilt (§40, invariant 59).
    /// </summary>
    public sealed class SchedulerData
    {
        public long NextEventSequence;

        public List<ScheduledEventData> PendingEvents = new List<ScheduledEventData>();
    }

    public sealed class ScheduledEventData
    {
        public int Id;
        public long DueAtMinutes;
        public int Phase;
        public long EventSequence;
        public string EventType;

        /// <summary>Payload encoded by the codec registered for <see cref="EventType"/>.</summary>
        public ScheduledEventPayloadData Payload = new ScheduledEventPayloadData();

        public List<EventDependencyData> Dependencies = new List<EventDependencyData>();
    }

    /// <summary>One aspect-scoped revision counter (§11.2.1).</summary>
    public sealed class RevisionData
    {
        public int SubjectEntityKind;
        public int SubjectRuntimeId;
        public string Aspect;
        public int Revision;
    }

    public sealed class EventDependencyData
    {
        public int SubjectEntityKind;
        public int SubjectRuntimeId;
        public string Aspect;
        public int ExpectedRevision;
    }

    /// <summary>
    /// Format-agnostic payload encoding: a handful of strings and numbers whose meaning is defined by
    /// the codec for that event type (§11.3 — payloads are pure data).
    /// </summary>
    public sealed class ScheduledEventPayloadData
    {
        public List<string> Strings = new List<string>();

        public List<long> Numbers = new List<long>();

        // Optional definition-derived snapshots used by commitment-introduction events. Older saves
        // omit these fields and therefore migrate to a no-op accountability policy.
        public List<CommitmentStakeholderData> CommitmentStakeholders = new List<CommitmentStakeholderData>();

        public bool HasCommitmentStakeholderSnapshot;

        public CommitmentAccountabilityPolicyData CommitmentAccountabilityPolicy;
    }
}
