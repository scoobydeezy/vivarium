using Vivarium.Domain.Activities;
using Vivarium.Domain.Attention;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;

namespace Vivarium.Application.Commands
{
    /// <summary>
    /// Advances authoritative time (§33).
    /// <para>
    /// The mode is explicit, because offline catch-up is not "Live, but fast" (§21). For
    /// <see cref="SimulationMode.OfflineCatchUp"/> the duration is computed by the Application from the
    /// persisted anchor and <c>IRealWorldClock</c> — the Domain never reads the wall clock (invariant 32).
    /// </para>
    /// </summary>
    public sealed class AdvanceSimulationCommand : ICommand<Result>
    {
        public AdvanceSimulationCommand(SimDuration duration, SimulationMode mode = SimulationMode.Live, int publishEveryInstants = 0)
        {
            Duration = duration;
            Mode = mode;
            PublishEveryInstants = publishEveryInstants;
        }

        public SimDuration Duration { get; }

        public SimulationMode Mode { get; }

        /// <summary>Publish progress every N settled instants during long catch-up. 0 = only at the end.</summary>
        public int PublishEveryInstants { get; }
    }

    /// <summary>Durable follow setting — one of the inputs to the canonical watch signal (§20.1).</summary>
    public sealed class FollowCharacterCommand : ICommand<Result>
    {
        public FollowCharacterCommand(CharacterId characterId, bool follow)
        {
            CharacterId = characterId;
            Follow = follow;
        }

        public CharacterId CharacterId { get; }

        public bool Follow { get; }
    }

    /// <summary>
    /// A character became meaningfully visible for observation (§25).
    /// <para>
    /// Presentation aggregates transitions into this. It must <b>not</b> be emitted per rendered frame.
    /// </para>
    /// </summary>
    public sealed class BeginObservingCharacterCommand : ICommand<Result>
    {
        public BeginObservingCharacterCommand(CharacterId characterId)
        {
            CharacterId = characterId;
        }

        public CharacterId CharacterId { get; }
    }

    /// <summary>A character stopped being visible for observation (§25).</summary>
    public sealed class EndObservingCharacterCommand : ICommand<Result>
    {
        public EndObservingCharacterCommand(CharacterId characterId)
        {
            CharacterId = characterId;
        }

        public CharacterId CharacterId { get; }
    }

    /// <summary>The player opened a character's profile — a stronger observation channel (§24, §25).</summary>
    public sealed class InspectCharacterCommand : ICommand<Result>
    {
        public InspectCharacterCommand(CharacterId characterId, bool open = true)
        {
            CharacterId = characterId;
            Open = open;
        }

        public CharacterId CharacterId { get; }

        public bool Open { get; }
    }

    /// <summary>Requests travel through the world's committed route network.</summary>
    public sealed class TravelCharacterCommand : ICommand<Result>
    {
        public TravelCharacterCommand(CharacterId characterId, LocationId destinationLocationId)
        {
            CharacterId = characterId;
            DestinationLocationId = destinationLocationId;
        }

        public CharacterId CharacterId { get; }

        public LocationId DestinationLocationId { get; }
    }

    /// <summary>Holds a decision so it does not auto-resolve (§20).</summary>
    public sealed class HoldDecisionCommand : ICommand<Result>
    {
        public HoldDecisionCommand(DecisionId decisionId)
        {
            DecisionId = decisionId;
        }

        public DecisionId DecisionId { get; }
    }

    /// <summary>Releases a held decision, letting it resolve on schedule (§20).</summary>
    public sealed class ReleaseDecisionCommand : ICommand<Result>
    {
        public ReleaseDecisionCommand(DecisionId decisionId)
        {
            DecisionId = decisionId;
        }

        public DecisionId DecisionId { get; }
    }

    /// <summary>
    /// Spends an intervention on an open decision (§19).
    /// <para>
    /// Targets a stable <see cref="DecisionInfluenceId"/>, never a list position, so a world change that
    /// reorders influences cannot silently retarget it (§17.2).
    /// </para>
    /// </summary>
    public sealed class ApplyDecisionInterventionCommand : ICommand<Result>
    {
        public ApplyDecisionInterventionCommand(
            DecisionId decisionId,
            AuthoredId interventionDefinitionId,
            DecisionInfluenceId targetInfluenceId)
        {
            DecisionId = decisionId;
            InterventionDefinitionId = interventionDefinitionId;
            TargetInfluenceId = targetInfluenceId;
        }

        public DecisionId DecisionId { get; }

        public AuthoredId InterventionDefinitionId { get; }

        public DecisionInfluenceId TargetInfluenceId { get; }
    }

    /// <summary>Produces and freezes an attended Decision's rolls before outcome commitment.</summary>
    public sealed class BeginDecisionResolutionCommand : ICommand<Result>
    {
        public BeginDecisionResolutionCommand(DecisionId decisionId) => DecisionId = decisionId;
        public DecisionId DecisionId { get; }
    }

    /// <summary>Accepts the currently frozen rolls and commits the ordinary Decision outcome.</summary>
    public sealed class CommitDecisionResolutionCommand : ICommand<Result>
    {
        public CommitDecisionResolutionCommand(DecisionId decisionId) => DecisionId = decisionId;
        public DecisionId DecisionId { get; }
    }

    /// <summary>
    /// Submits a normalized result from interactive play (§29.6).
    /// <para>
    /// The mini-game never mutates Domain state. It hands over a grade and magnitude, which enters the
    /// same consequence pipeline as automatic resolution. Raw score telemetry and in-progress UI state
    /// stay in Presentation (invariant 47).
    /// </para>
    /// </summary>
    public sealed class SubmitActivityPerformanceCommand : ICommand<Result>
    {
        public SubmitActivityPerformanceCommand(ActivityInstanceId activityInstanceId, ActivityPerformanceResult result)
        {
            ActivityInstanceId = activityInstanceId;
            Result = result;
        }

        public ActivityInstanceId ActivityInstanceId { get; }

        public ActivityPerformanceResult Result { get; }
    }

    /// <summary>Adds a location to the containment hierarchy (§27, §33).</summary>
    public sealed class BuildLocationCommand : ICommand<Result<LocationId>>
    {
        public BuildLocationCommand(LocationId parentLocationId, AuthoredId locationKindId, string displayName, bool occupiable = true)
        {
            ParentLocationId = parentLocationId;
            LocationKindId = locationKindId;
            DisplayName = displayName;
            Occupiable = occupiable;
        }

        public LocationId ParentLocationId { get; }

        public AuthoredId LocationKindId { get; }

        public string DisplayName { get; }

        public bool Occupiable { get; }
    }

    /// <summary>Sets the attention policy for a character (§20).</summary>
    public sealed class SetAttentionPolicyCommand : ICommand<Result>
    {
        public SetAttentionPolicyCommand(CharacterId characterId, AttentionPolicy policy)
        {
            CharacterId = characterId;
            Policy = policy;
        }

        public CharacterId CharacterId { get; }

        public AttentionPolicy Policy { get; }
    }
}
