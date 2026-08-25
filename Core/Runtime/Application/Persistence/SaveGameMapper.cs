using System;
using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Attention;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Evaluation;
using Vivarium.Domain.Groups;
using Vivarium.Domain.Employment;
using Vivarium.Domain.History;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Randomness;
using Vivarium.Domain.PlayerAgency;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
using Vivarium.Domain.Social;
using Vivarium.Domain.Time;

namespace Vivarium.Application.Persistence
{
    /// <summary>
    /// Maps <see cref="WorldState"/> to and from versioned save DTOs (§38, §40).
    /// <para>
    /// Two rules shape everything here. Authoritative state is written explicitly — scheduler, active
    /// Activities and Commitments, revisions, knowledge, allocator counters. Reconstructible state is
    /// <i>not</i> written; it is rebuilt and validated on load, so a stale cache can never carry
    /// authority into a resumed world.
    /// </para>
    /// <para>
    /// Saves are taken against a quiescent snapshot, never halfway through a settlement cascade (§2.2.1).
    /// </para>
    /// </summary>
    public sealed class SaveGameMapper
    {
        private readonly ScheduledEventPayloadCodecRegistry _payloadCodecs;
        private readonly DecisionSignalProviderRegistry _decisionSignals;

        public SaveGameMapper(
            ScheduledEventPayloadCodecRegistry payloadCodecs,
            DecisionSignalProviderRegistry decisionSignals = null)
        {
            _payloadCodecs = payloadCodecs ?? throw new ArgumentNullException(nameof(payloadCodecs));
            _decisionSignals = decisionSignals;
        }

        /// <summary>Captures a quiescent world into save data.</summary>
        public SaveGameData ToSave(
            WorldState world,
            int contentVersion,
            int simulationRulesVersion,
            long savedAtRealTimeUtcTicks,
            long lastCommandSequence)
        {
            var data = new SaveGameData
            {
                SchemaVersion = SaveGameData.CurrentSchemaVersion,
                ContentVersion = contentVersion,
                SimulationRulesVersion = simulationRulesVersion,
                RandomAlgorithmVersion = RandomAlgorithmVersion.Current,
                WorldSeed = world.WorldSeed,
                ClockMinutes = world.Clock.Now.TotalMinutes,
                SavedAtRealTimeUtcTicks = savedAtRealTimeUtcTicks,
                LastCommandSequence = lastCommandSequence,
                NudgeBalance = world.Nudges.Balance,
                NudgeRevision = world.Nudges.Revision,
            };

            foreach (KeyValuePair<InterventionResourceKind, ResourceState> pair in world.InterventionResources.All)
            {
                ResourceState state = pair.Value;
                data.InterventionResources.Add(new InterventionResourceData
                {
                    Kind = (int)pair.Key, Balance = state.Balance, Cap = state.Cap, Revision = state.Revision,
                    RefreshAmount = state.RefreshAmount, RefreshPeriodMinutes = state.RefreshPeriod.TotalMinutes,
                    NextRefreshAtMinutes = state.NextRefreshAt.TotalMinutes,
                });
            }

            RuntimeIdCounters counters = world.RuntimeIds.Snapshot();
            data.RuntimeIdCounters = new RuntimeIdCountersData
            {
                Characters = counters.Characters,
                Activities = counters.Activities,
                Commitments = counters.Commitments,
                CommitmentOutcomes = counters.CommitmentOutcomes,
                Relationships = counters.Relationships,
                Decisions = counters.Decisions,
                Locations = counters.Locations,
                Groups = counters.Groups,
                Employments = counters.Employments,
                ScheduledEvents = counters.ScheduledEvents,
                HistoryEntries = counters.HistoryEntries,
                EventSequence = counters.EventSequence,
            };

            WriteRevisions(world, data);
            WriteScheduler(world, data);
            WriteSpatial(world, data);
            WriteCharacters(world, data);
            WriteActivities(world, data);
            WriteCommitments(world, data);
            WriteGroups(world, data);
            WriteEmployments(world, data);
            WriteRelationships(world, data);
            WriteDecisions(world, data);
            WriteKnowledge(world, data);
            WriteAttention(world, data);
            WriteHistory(world, data);

            return data;
        }

        /// <summary>
        /// Rebuilds a world from save data, then rebuilds derived indexes and clears ephemeral state
        /// (§40). The returned world is quiescent and ready to resume.
        /// </summary>
        public WorldState Restore(SaveGameData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.SchemaVersion > SaveGameData.CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Save schema version {data.SchemaVersion} is newer than this build understands ({SaveGameData.CurrentSchemaVersion}). SchemaVersion decides loadability; content/rules/random versions do not (§39.1).");
            }

            var world = new WorldState(
                data.WorldSeed,
                new SimTime(data.ClockMinutes),
                new RuntimeIdCounters(
                    data.RuntimeIdCounters.Characters,
                    data.RuntimeIdCounters.Activities,
                    data.RuntimeIdCounters.Commitments,
                    data.RuntimeIdCounters.CommitmentOutcomes,
                    data.RuntimeIdCounters.Relationships,
                    data.RuntimeIdCounters.Decisions,
                    data.RuntimeIdCounters.Locations,
                    data.RuntimeIdCounters.Groups,
                    data.RuntimeIdCounters.Employments,
                    data.RuntimeIdCounters.ScheduledEvents,
                    data.RuntimeIdCounters.HistoryEntries,
                    data.RuntimeIdCounters.EventSequence));

            ReadSpatial(world, data);
            ReadCharacters(world, data);
            ReadActivities(world, data);
            ReadCommitments(world, data);
            ReadGroups(world, data);
            ReadEmployments(world, data);
            ReadRelationships(world, data);
            ReadDecisions(world, data);
            ReadKnowledge(world, data);
            ReadAttention(world, data);
            ReadHistory(world, data);
            ReadRevisions(world, data);
            ReadScheduler(world, data);
            world.RestoreNudges(data.NudgeBalance, data.NudgeRevision);
            for (int i = 0; i < data.InterventionResources.Count; i++)
            {
                InterventionResourceData resource = data.InterventionResources[i];
                world.InterventionResources.Restore((InterventionResourceKind)resource.Kind,
                    new ResourceState(resource.Balance, resource.Cap, resource.Revision, resource.RefreshAmount,
                        new SimDuration(resource.RefreshPeriodMinutes), new SimTime(resource.NextRefreshAtMinutes)));
            }
            NudgeRegenerationSchedule.EnsureScheduled(world);

            // Canonical state is in; derived structures are rebuilt from it and never trusted from disk.
            world.RebuildDerivedIndexes();
            if (_decisionSignals != null)
            {
                var reasoning = new CompiledDecisionReasoningService();
                foreach (Decision decision in world.Decisions.All)
                {
                    if (decision.IsActive && !decision.IsAwaitingCommit && decision.ReasoningProgram != null)
                    {
                        reasoning.RebuildRoutes(world, decision, _decisionSignals);
                    }
                }
            }

            return world;
        }

        // ---------- revisions ----------

        private static void WriteRevisions(WorldState world, SaveGameData data)
        {
            foreach (KeyValuePair<RevisionKey, int> entry in world.Revisions.Snapshot())
            {
                data.Revisions.Add(new RevisionData
                {
                    SubjectEntityKind = (int)entry.Key.Subject.Kind,
                    SubjectRuntimeId = entry.Key.Subject.RuntimeId,
                    Aspect = entry.Key.Aspect.Value,
                    Revision = entry.Value,
                });
            }
        }

        private static void ReadRevisions(WorldState world, SaveGameData data)
        {
            for (int i = 0; i < data.Revisions.Count; i++)
            {
                RevisionData revision = data.Revisions[i];
                var key = new RevisionKey(
                    new EntityRef((EntityKind)revision.SubjectEntityKind, revision.SubjectRuntimeId),
                    new AuthoredId(revision.Aspect));

                // Counters must land on their saved values: pending events recorded the revisions they
                // expected, so restoring to zero would discard the world's whole queued future (§11.2).
                for (int bump = world.Revisions.Get(key); bump < revision.Revision; bump++)
                {
                    world.Revisions.Bump(key);
                }
            }
        }

        // ---------- scheduler ----------

        private void WriteScheduler(WorldState world, SaveGameData data)
        {
            data.Scheduler.NextEventSequence = world.RuntimeIds.EventSequence.Issued;

            foreach (ScheduledEvent scheduled in world.Scheduler.PendingEvents)
            {
                var dto = new ScheduledEventData
                {
                    Id = scheduled.Id.Value,
                    DueAtMinutes = scheduled.DueAt.TotalMinutes,
                    Phase = (int)scheduled.Phase,
                    EventSequence = scheduled.EventSequence,
                    EventType = scheduled.EventType.Value,
                    Payload = _payloadCodecs.Encode(scheduled.EventType, scheduled.Payload),
                };

                for (int i = 0; i < scheduled.Dependencies.Count; i++)
                {
                    EventDependency dependency = scheduled.Dependencies[i];
                    dto.Dependencies.Add(new EventDependencyData
                    {
                        SubjectEntityKind = (int)dependency.Key.Subject.Kind,
                        SubjectRuntimeId = dependency.Key.Subject.RuntimeId,
                        Aspect = dependency.Key.Aspect.Value,
                        ExpectedRevision = dependency.ExpectedRevision,
                    });
                }

                data.Scheduler.PendingEvents.Add(dto);
            }
        }

        private void ReadScheduler(WorldState world, SaveGameData data)
        {
            for (int i = 0; i < data.Scheduler.PendingEvents.Count; i++)
            {
                ScheduledEventData dto = data.Scheduler.PendingEvents[i];
                var eventType = new AuthoredId(dto.EventType);

                var dependencies = new EventDependency[dto.Dependencies.Count];
                for (int d = 0; d < dto.Dependencies.Count; d++)
                {
                    EventDependencyData dependency = dto.Dependencies[d];
                    dependencies[d] = new EventDependency(
                        new RevisionKey(
                            new EntityRef((EntityKind)dependency.SubjectEntityKind, dependency.SubjectRuntimeId),
                            new AuthoredId(dependency.Aspect)),
                        dependency.ExpectedRevision);
                }

                world.Scheduler.Restore(new ScheduledEvent(
                    new ScheduledEventId(dto.Id),
                    new SimTime(dto.DueAtMinutes),
                    (SchedulePhase)dto.Phase,
                    dto.EventSequence,
                    eventType,
                    _payloadCodecs.Decode(eventType, dto.Payload),
                    dependencies));
            }
        }

        // ---------- spatial ----------

        private static void WriteSpatial(WorldState world, SaveGameData data)
        {
            foreach (LocationNode node in world.Locations.Nodes.All)
            {
                data.Locations.Add(new LocationData
                {
                    Id = node.Id.Value,
                    ParentLocationId = node.ParentLocationId.Value,
                    LocationKindId = node.LocationKindId.Value,
                    DisplayName = node.DisplayName,
                    IsOccupiable = node.IsOccupiable,
                    Capacity = node.Capacity,
                    SupportsPlayerManagedAvailability = node.SupportsPlayerManagedAvailability,
                    IsOpen = node.IsOpen,
                });
                LocationData location = data.Locations[data.Locations.Count - 1];
                for (int a = 0; a < node.ActivityAffordances.Count; a++)
                    location.ActivityAffordances.Add(node.ActivityAffordances[a].Value);
            }

            foreach (LocationNode node in world.Locations.Nodes.All)
            {
                IReadOnlyList<TravelConnection> connections = world.TravelNetwork.ConnectionsFrom(node.Id);
                for (int i = 0; i < connections.Count; i++)
                {
                    TravelConnection connection = connections[i];
                    data.TravelConnections.Add(new TravelConnectionData
                    {
                        FromLocationId = connection.From.Value,
                        ToLocationId = connection.To.Value,
                        CostMinutes = connection.Cost.TotalMinutes,
                        TravelModeId = connection.TravelModeId.Value,
                    });
                }
            }
        }

        private static void ReadSpatial(WorldState world, SaveGameData data)
        {
            // Parents must exist before children; ascending id order guarantees it, since a parent is
            // always allocated before anything nested inside it.
            data.Locations.Sort((a, b) => a.Id.CompareTo(b.Id));

            for (int i = 0; i < data.Locations.Count; i++)
            {
                LocationData location = data.Locations[i];
                var affordances = new AuthoredId[location.ActivityAffordances.Count];
                for (int a = 0; a < affordances.Length; a++)
                    affordances[a] = new AuthoredId(location.ActivityAffordances[a]);
                world.Locations.Add(new LocationNode(
                    new LocationId(location.Id),
                    new LocationId(location.ParentLocationId),
                    new AuthoredId(location.LocationKindId),
                    location.DisplayName,
                    location.IsOccupiable,
                    location.Capacity,
                    affordances,
                    location.SupportsPlayerManagedAvailability,
                    location.IsOpen));
            }

            for (int i = 0; i < data.TravelConnections.Count; i++)
            {
                TravelConnectionData connection = data.TravelConnections[i];
                world.TravelNetwork.Connect(
                    new LocationId(connection.FromLocationId),
                    new LocationId(connection.ToLocationId),
                    new SimDuration(connection.CostMinutes),
                    new AuthoredId(connection.TravelModeId));
            }
        }

        // ---------- characters ----------

        private static void WriteCharacters(WorldState world, SaveGameData data)
        {
            foreach (Character character in world.Characters.All)
            {
                var dto = new CharacterData
                {
                    Id = character.Id.Value,
                    DisplayName = character.DisplayName,
                    CreatedAtMinutes = character.CreatedAt.TotalMinutes,
                    IsActive = character.IsActive,
                    RetiredAtMinutes = character.RetiredAt?.TotalMinutes ?? -1,
                    CurrentActivityId = character.CurrentActivityId.Value,
                };

                foreach (AuthoredId trait in character.Traits)
                {
                    dto.Traits.Add(trait.Value);
                }

                WriteVector(character.Personality, dto.Personality);
                dto.PersonalityRevision = character.PersonalityRevision;
                WriteTags(character.Values, dto.Values);
                dto.ValuesRevision = character.Values.Revision;
                WriteTags(character.Interests, dto.Interests);
                dto.InterestsRevision = character.Interests.Revision;
                foreach (KeyValuePair<AuthoredId, AnalyticalProgression> affect in character.Affect.All)
                {
                    dto.Affect.Add(new AffectData
                    {
                        Kind = affect.Key.Value,
                        Progression = ToDto(affect.Value),
                        Revision = character.Affect.Revision(affect.Key),
                    });
                }
                foreach (KeyValuePair<AuthoredId, AppraisalField> field in character.AppraisalFields)
                {
                    dto.AppraisalFields.Add(ToDto(field.Value));
                }

                foreach (KeyValuePair<AuthoredId, NeedState> need in character.Needs)
                {
                    dto.Needs.Add(new NeedData
                    {
                        NeedId = need.Key.Value,
                        Progression = ToDto(need.Value.Progression),
                        BehaviouralThreshold = need.Value.BehaviouralThreshold,
                        PendingThresholdEventId = need.Value.PendingThresholdEventId.Value,
                    });
                }

                data.Characters.Add(dto);
            }
        }

        private static void ReadCharacters(WorldState world, SaveGameData data)
        {
            for (int i = 0; i < data.Characters.Count; i++)
            {
                CharacterData dto = data.Characters[i];
                var character = new Character(new CharacterId(dto.Id), dto.DisplayName, new SimTime(dto.CreatedAtMinutes));

                for (int t = 0; t < dto.Traits.Count; t++)
                {
                    character.AddTrait(new AuthoredId(dto.Traits[t]));
                }

                character.RestorePersonality(ReadVector(dto.Personality), dto.PersonalityRevision);
                ReadTags(character.Values, dto.Values, dto.ValuesRevision);
                ReadTags(character.Interests, dto.Interests, dto.InterestsRevision);
                for (int a = 0; a < dto.Affect.Count; a++)
                {
                    AffectData affect = dto.Affect[a];
                    character.Affect.Restore(new AuthoredId(affect.Kind), FromDto(affect.Progression), affect.Revision);
                }
                for (int f = 0; f < dto.AppraisalFields.Count; f++)
                {
                    character.SetAppraisalField(FromDto(dto.AppraisalFields[f], character.Id));
                }

                for (int n = 0; n < dto.Needs.Count; n++)
                {
                    NeedData need = dto.Needs[n];
                    character.SetNeed(new NeedState(
                        new AuthoredId(need.NeedId),
                        FromDto(need.Progression),
                        need.BehaviouralThreshold,
                        new ScheduledEventId(need.PendingThresholdEventId)));
                }

                character.RestoreLifecycle(
                    dto.IsActive,
                    dto.RetiredAtMinutes >= 0 ? new SimTime(dto.RetiredAtMinutes) : (SimTime?)null,
                    new ActivityInstanceId(dto.CurrentActivityId));

                world.Characters.Add(character.Id, character);
            }
        }

        // ---------- activities ----------

        private static void WriteActivities(WorldState world, SaveGameData data)
        {
            foreach (ActivityInstance activity in world.Activities.All)
            {
                var dto = new ActivityData
                {
                    Id = activity.Id.Value,
                    CharacterId = activity.CharacterId.Value,
                    DefinitionId = activity.DefinitionId.Value,
                    StartedAtMinutes = activity.StartedAt.TotalMinutes,
                    Status = (int)activity.Status,
                    SourceCommitmentId = activity.SourceCommitmentId.Value,
                    PendingCompletionEventId = activity.PendingCompletionEventId.Value,
                    SpatialKind = (int)activity.SpatialContext.Kind,
                    Progress = ToDto(activity.Progress),
                    Performance = ToDto(activity.Performance),
                };

                if (activity.SpatialContext.IsLocated)
                {
                    dto.LocationId = activity.SpatialContext.LocationId.Value;
                }
                else
                {
                    TransitDetails transit = activity.SpatialContext.Transit;
                    dto.TransitOriginLocationId = transit.OriginLocationId.Value;
                    dto.TransitDestinationLocationId = transit.DestinationLocationId.Value;
                    dto.TransitDepartedAtMinutes = transit.DepartedAt.TotalMinutes;
                    dto.TransitArrivesAtMinutes = transit.ArrivesAt.TotalMinutes;
                    dto.TransitTravelModeId = transit.TravelModeId.Value;
                    dto.TransitTravelPlanId = transit.TravelPlanId;
                }

                if (activity.AcceptedResult.HasValue)
                {
                    ActivityPerformanceResult result = activity.AcceptedResult.Value;
                    dto.HasAcceptedResult = true;
                    dto.ResultGrade = (int)result.Grade;
                    dto.ResultMagnitude = result.Magnitude;
                    dto.ResultSource = (int)result.Source;
                    dto.ResultOutcomeId = result.OutcomeId.Value;
                }

                foreach (KeyValuePair<AuthoredId, long> parameter in activity.CommittedParameters)
                {
                    dto.CommittedParameters.Add(new AuthoredLongData { Key = parameter.Key.Value, Value = parameter.Value });
                }

                for (int i = 0; i < activity.ActiveModifiers.Count; i++)
                {
                    ActivityContextModifier modifier = activity.ActiveModifiers[i];
                    dto.ActiveModifiers.Add(new ActivityModifierData
                    {
                        ModifierId = modifier.ModifierId.Value,
                        AppliedAtMinutes = modifier.AppliedAt.TotalMinutes,
                        RateNumerator = modifier.PerformanceRateNumerator,
                        RateDenominator = modifier.PerformanceRateDenominator,
                        CauseEntityKind = (int)modifier.Cause.Kind,
                        CauseRuntimeId = modifier.Cause.RuntimeId,
                    });
                }

                data.Activities.Add(dto);
            }
        }

        private static void ReadActivities(WorldState world, SaveGameData data)
        {
            for (int i = 0; i < data.Activities.Count; i++)
            {
                ActivityData dto = data.Activities[i];

                ActivitySpatialContext context = dto.SpatialKind == (int)SpatialContextKind.Traveling
                    ? ActivitySpatialContext.Traveling(new TransitDetails(
                        new LocationId(dto.TransitOriginLocationId),
                        new LocationId(dto.TransitDestinationLocationId),
                        new SimTime(dto.TransitDepartedAtMinutes),
                        new SimTime(dto.TransitArrivesAtMinutes),
                        new AuthoredId(dto.TransitTravelModeId),
                        dto.TransitTravelPlanId))
                    : ActivitySpatialContext.Located(new LocationId(dto.LocationId));

                var activity = new ActivityInstance(
                    new ActivityInstanceId(dto.Id),
                    new CharacterId(dto.CharacterId),
                    new AuthoredId(dto.DefinitionId),
                    new SimTime(dto.StartedAtMinutes),
                    context,
                    FromDto(dto.Progress),
                    FromDto(dto.Performance),
                    new CommitmentId(dto.SourceCommitmentId));

                for (int p = 0; p < dto.CommittedParameters.Count; p++)
                {
                    activity.CommitParameter(new AuthoredId(dto.CommittedParameters[p].Key), dto.CommittedParameters[p].Value);
                }

                for (int m = 0; m < dto.ActiveModifiers.Count; m++)
                {
                    ActivityModifierData modifier = dto.ActiveModifiers[m];
                    activity.AddModifier(new ActivityContextModifier(
                        new AuthoredId(modifier.ModifierId),
                        new SimTime(modifier.AppliedAtMinutes),
                        modifier.RateNumerator,
                        modifier.RateDenominator,
                        new EntityRef((EntityKind)modifier.CauseEntityKind, modifier.CauseRuntimeId)));
                }

                activity.SetPendingCompletionEvent(new ScheduledEventId(dto.PendingCompletionEventId));
                activity.RestoreStatus(
                    (ActivityStatus)dto.Status,
                    dto.HasAcceptedResult
                        ? new ActivityPerformanceResult(
                            (PerformanceGrade)dto.ResultGrade,
                            dto.ResultMagnitude,
                            (OutcomeSource)dto.ResultSource,
                            new AuthoredId(dto.ResultOutcomeId))
                        : (ActivityPerformanceResult?)null);

                world.Activities.Add(activity.Id, activity);
            }
        }

        // ---------- commitments ----------

        private static void WriteCommitments(WorldState world, SaveGameData data)
        {
            foreach (Commitment commitment in world.Commitments.All)
            {
                var dto = new CommitmentData
                {
                    Id = commitment.Id.Value,
                    CharacterId = commitment.CharacterId.Value,
                    Kind = commitment.Kind.Value,
                    EarliestStartMinutes = commitment.EarliestStart.TotalMinutes,
                    LatestStartMinutes = commitment.LatestStart.TotalMinutes,
                    ExpectedDurationMinutes = commitment.ExpectedDuration.TotalMinutes,
                    LocationId = commitment.LocationId.Value,
                    Priority = commitment.Priority,
                    ActivityDefinitionId = commitment.ActivityDefinitionId.Value,
                    SourceEntityKind = (int)commitment.Source.Kind,
                    SourceRuntimeId = commitment.Source.RuntimeId,
                    SourceTemplateId = commitment.SourceTemplateId.Value,
                    Status = (int)commitment.Status,
                    FulfillingActivityId = commitment.FulfillingActivityId.Value,
                };

                for (int i = 0; i < commitment.AdditionalParticipants.Count; i++)
                {
                    dto.AdditionalParticipants.Add(commitment.AdditionalParticipants[i].Value);
                }

                dto.Stakeholders = CommitmentAccountabilityDataMapper.WriteStakeholders(commitment.Stakeholders);
                dto.HasStakeholderSnapshot = true;
                dto.AccountabilityPolicy = CommitmentAccountabilityDataMapper.WritePolicy(commitment.AccountabilityPolicy);

                data.Commitments.Add(dto);
            }
        }

        private static void ReadCommitments(WorldState world, SaveGameData data)
        {
            for (int i = 0; i < data.Commitments.Count; i++)
            {
                CommitmentData dto = data.Commitments[i];

                var participants = new CharacterId[dto.AdditionalParticipants.Count];
                for (int p = 0; p < participants.Length; p++)
                {
                    participants[p] = new CharacterId(dto.AdditionalParticipants[p]);
                }

                var commitment = new Commitment(
                    new CommitmentId(dto.Id),
                    new CharacterId(dto.CharacterId),
                    new AuthoredId(dto.Kind),
                    new SimTime(dto.EarliestStartMinutes),
                    new SimTime(dto.LatestStartMinutes),
                    new SimDuration(dto.ExpectedDurationMinutes),
                    new LocationId(dto.LocationId),
                    dto.Priority,
                    new AuthoredId(dto.ActivityDefinitionId),
                    new EntityRef((EntityKind)dto.SourceEntityKind, dto.SourceRuntimeId),
                    participants,
                    new AuthoredId(dto.SourceTemplateId),
                    CommitmentAccountabilityDataMapper.ReadStakeholders(dto.Stakeholders, dto.HasStakeholderSnapshot),
                    CommitmentAccountabilityDataMapper.ReadPolicy(dto.AccountabilityPolicy));

                commitment.RestoreStatus((CommitmentStatus)dto.Status, new ActivityInstanceId(dto.FulfillingActivityId));
                world.Commitments.Add(commitment.Id, commitment);
            }
        }

        // ---------- groups ----------

        private static void WriteGroups(WorldState world, SaveGameData data)
        {
            foreach (Group group in world.Groups.All)
            {
                data.Groups.Add(new GroupData
                {
                    Id = group.Id.Value,
                    Kind = group.Kind.Value,
                    DisplayName = group.DisplayName,
                    PrimaryLocationId = group.PrimaryLocationId.Value,
                });

                foreach (CharacterId member in world.Memberships.MembersOf(group.Id))
                {
                    data.GroupMemberships.Add(new GroupMembershipData { GroupId = group.Id.Value, CharacterId = member.Value });
                }
            }
        }

        private static void ReadGroups(WorldState world, SaveGameData data)
        {
            for (int i = 0; i < data.Groups.Count; i++)
            {
                GroupData dto = data.Groups[i];
                var group = new Group(
                    new GroupId(dto.Id),
                    new AuthoredId(dto.Kind),
                    dto.DisplayName,
                    new LocationId(dto.PrimaryLocationId));

                world.Groups.Add(group.Id, group);
            }

            for (int i = 0; i < data.GroupMemberships.Count; i++)
            {
                GroupMembershipData membership = data.GroupMemberships[i];
                world.Memberships.Join(new GroupId(membership.GroupId), new CharacterId(membership.CharacterId));
            }
        }

        // ---------- employments ----------

        private static void WriteEmployments(WorldState world, SaveGameData data)
        {
            foreach (Employment employment in world.Employments.All)
            {
                var dto = new EmploymentData
                {
                    Id = employment.Id.Value,
                    EmployeeId = employment.EmployeeId.Value,
                    EmployerGroupId = employment.EmployerGroupId.Value,
                    DefinitionId = employment.DefinitionId.Value,
                    RoleId = employment.RoleId.Value,
                    WorkLocationId = employment.WorkLocationId.Value,
                    SupervisorId = employment.SupervisorId.Value,
                };

                for (int i = 0; i < employment.ObligationPatterns.Count; i++)
                {
                    EmploymentObligationPattern pattern = employment.ObligationPatterns[i];
                    dto.ObligationPatterns.Add(new EmploymentObligationPatternData
                    {
                        Id = pattern.Id.Value,
                        CommitmentKind = pattern.CommitmentKind.Value,
                        CycleLengthDays = pattern.CycleLengthDays,
                        ActiveDaysMask = pattern.ActiveDaysMask,
                        StartMinuteOfDay = pattern.StartMinuteOfDay,
                        DurationMinutes = pattern.Duration.TotalMinutes,
                        Priority = pattern.Priority,
                        ActivityDefinitionId = pattern.ActivityDefinitionId.Value,
                        StartWindowMinutes = pattern.StartWindow.TotalMinutes,
                        AccountabilityPolicy = CommitmentAccountabilityDataMapper.WritePolicy(pattern.AccountabilityPolicy),
                    });
                }
                data.Employments.Add(dto);
            }
        }

        private static void ReadEmployments(WorldState world, SaveGameData data)
        {
            for (int i = 0; i < data.Employments.Count; i++)
            {
                EmploymentData dto = data.Employments[i];
                var patterns = new EmploymentObligationPattern[dto.ObligationPatterns.Count];
                for (int p = 0; p < patterns.Length; p++)
                {
                    EmploymentObligationPatternData pattern = dto.ObligationPatterns[p];
                    patterns[p] = new EmploymentObligationPattern(
                        new AuthoredId(pattern.Id),
                        new AuthoredId(pattern.CommitmentKind),
                        pattern.CycleLengthDays,
                        pattern.ActiveDaysMask,
                        pattern.StartMinuteOfDay,
                        SimDuration.FromMinutes(pattern.DurationMinutes),
                        pattern.Priority,
                        new AuthoredId(pattern.ActivityDefinitionId),
                        SimDuration.FromMinutes(pattern.StartWindowMinutes),
                        CommitmentAccountabilityDataMapper.ReadPolicy(pattern.AccountabilityPolicy));
                }

                var employment = new Employment(
                    new EmploymentId(dto.Id),
                    new CharacterId(dto.EmployeeId),
                    new GroupId(dto.EmployerGroupId),
                    new AuthoredId(dto.DefinitionId),
                    new AuthoredId(dto.RoleId),
                    new LocationId(dto.WorkLocationId),
                    new CharacterId(dto.SupervisorId),
                    patterns);
                world.Employments.Add(employment.Id, employment);
            }
        }

        // ---------- relationships ----------

        private static void WriteRelationships(WorldState world, SaveGameData data)
        {
            foreach (Relationship relationship in world.Relationships.All)
            {
                data.Relationships.Add(new RelationshipData
                {
                    Id = relationship.Id.Value,
                    LowCharacterId = relationship.LowCharacterId.Value,
                    HighCharacterId = relationship.HighCharacterId.Value,
                    Kind = relationship.Kind.Value,
                    EstablishedAtMinutes = relationship.EstablishedAt.TotalMinutes,
                    LastInteractionAtMinutes = relationship.LastInteractionAt?.TotalMinutes ?? -1,
                    IsActive = relationship.IsActive,
                    LowToHigh = ToDto(relationship.LowToHigh),
                    HighToLow = ToDto(relationship.HighToLow),
                });
            }
        }

        private static void ReadRelationships(WorldState world, SaveGameData data)
        {
            for (int i = 0; i < data.Relationships.Count; i++)
            {
                RelationshipData dto = data.Relationships[i];
                var relationship = new Relationship(
                    new RelationshipId(dto.Id),
                    new CharacterId(dto.LowCharacterId),
                    new CharacterId(dto.HighCharacterId),
                    new AuthoredId(dto.Kind),
                    FromDto(dto.Affinity),
                    new SimTime(dto.EstablishedAtMinutes));

                relationship.RestoreState(
                    dto.LastInteractionAtMinutes >= 0 ? new SimTime(dto.LastInteractionAtMinutes) : (SimTime?)null,
                    dto.IsActive);

                RestoreDirection(relationship.LowToHigh, dto.LowToHigh);
                RestoreDirection(relationship.HighToLow, dto.HighToLow);

                world.Relationships.Add(relationship.Id, relationship);
            }
        }

        // ---------- decisions ----------

        private static void WriteDecisions(WorldState world, SaveGameData data)
        {
            foreach (Decision decision in world.Decisions.All)
            {
                var dto = new DecisionData
                {
                    Id = decision.Id.Value,
                    CharacterId = decision.CharacterId.Value,
                    DefinitionId = decision.DefinitionId.Value,
                    CreatedAtMinutes = decision.CreatedAt.TotalMinutes,
                    ResolveAtMinutes = decision.ResolveAt.TotalMinutes,
                    Status = (int)decision.Status,
                    Importance = decision.Importance,
                    InfluenceRevision = decision.InfluenceRevision,
                    PendingResolveEventId = decision.PendingResolveEventId.Value,
                    ResolutionHistoryEntryId = decision.ResolutionHistoryEntryId.Value,
                    ConflictScopeKind = decision.ConflictScope.ScopeKind.Value,
                    ConflictScopeEntityKind = (int)decision.ConflictScope.Subject.Kind,
                    ConflictScopeRuntimeId = decision.ConflictScope.Subject.RuntimeId,
                };
                if (decision.CommitmentConflictKey != null)
                {
                    dto.HasCommitmentConflict = true;
                    dto.ConflictInstanceRevision = decision.CommitmentConflictKey.ConflictInstanceRevision;
                    dto.LatestResolutionAtMinutes = decision.LatestResolutionAt.TotalMinutes;
                    for (int c = 0; c < decision.CommitmentConflictKey.ParticipatingCommitmentIds.Count; c++)
                        dto.ConflictCommitmentIds.Add(decision.CommitmentConflictKey.ParticipatingCommitmentIds[c].Value);
                }

                for (int i = 0; i < decision.Options.Count; i++)
                {
                    DecisionOption option = decision.Options[i];
                    var optionData = new DecisionOptionData
                    {
                        Id = option.Id.Value,
                        LabelId = option.LabelId.Value,
                        OrderIndex = option.OrderIndex,
                    };
                    foreach (KeyValuePair<AuthoredId, DecisionParameterValue> parameter in option.Context)
                    {
                        optionData.Context.Add(WriteDecisionParameter(parameter.Key, parameter.Value));
                    }
                    if (option.CommitmentResolutionPlan != null)
                    {
                        optionData.CommitmentResolutionPlan = WriteCommitmentPlan(option.CommitmentResolutionPlan);
                    }
                    dto.Options.Add(optionData);
                }

                for (int i = 0; i < decision.Influences.Count; i++)
                {
                    DecisionInfluence influence = decision.Influences[i];
                    dto.Influences.Add(new DecisionInfluenceData
                    {
                        Id = influence.Id.Value,
                        OptionId = influence.OptionId.Value,
                        Category = influence.Category.Value,
                        LabelId = influence.LabelId.Value,
                        BaseDieSides = influence.BaseDie.Sides,
                        BaseDieFixedResult = influence.BaseDie.FixedResult,
                        CurrentDieSides = influence.CurrentDie.Sides,
                        CurrentDieFixedResult = influence.CurrentDie.FixedResult,
                        Visibility = (int)influence.DefaultVisibility,
                        RollIndex = influence.RollIndex,
                        IsRetracted = influence.IsRetracted,
                        DependencyContextKind = influence.DependencyKey.ContextKind.Value,
                        DependencyEntityKind = (int)influence.DependencyKey.Subject.Kind,
                        DependencyRuntimeId = influence.DependencyKey.Subject.RuntimeId,
                        SubjectEntityKind = (int)influence.Subject.Kind,
                        SubjectRuntimeId = influence.Subject.RuntimeId,
                        Polarity = (int)influence.Polarity,
                        ReasonChannelId = influence.ReasonChannelId.Value,
                        ReasonBindingId = influence.ReasonBindingId.Value,
                        Evaluation = WriteReasonEvaluation(influence.Evaluation),
                    });
                }

                for (int i = 0; i < decision.Interventions.Count; i++)
                {
                    AppliedIntervention intervention = decision.Interventions[i];
                    dto.Interventions.Add(new AppliedInterventionData
                    {
                        InterventionDefinitionId = intervention.InterventionDefinitionId.Value,
                        TargetInfluenceId = intervention.TargetInfluenceId.Value,
                        CommandSequence = intervention.CommandSequence,
                        Kind = (int)intervention.Kind,
                        ReplacementDieSides = intervention.ReplacementDie.Sides,
                        ReplacementDieFixedResult = intervention.ReplacementDie.FixedResult,
                        ResourceKind = (int)intervention.ResourceKind,
                        ResourceCost = intervention.ResourceCost,
                    });
                }

                foreach (DecisionDependencyKey key in decision.DependencyKeys)
                {
                    dto.DependencyKeys.Add(new DependencyKeyData
                    {
                        ContextKind = key.ContextKind.Value,
                        SubjectEntityKind = (int)key.Subject.Kind,
                        SubjectRuntimeId = key.Subject.RuntimeId,
                    });
                }

                foreach (KeyValuePair<AuthoredId, long> parameter in decision.SnapshottedParameters)
                {
                    dto.SnapshottedParameters.Add(new AuthoredLongData { Key = parameter.Key.Value, Value = parameter.Value });
                }

                foreach (KeyValuePair<AuthoredId, DecisionParameterValue> parameter in decision.ContextParameters)
                {
                    dto.ContextParameters.Add(WriteDecisionParameter(parameter.Key, parameter.Value));
                }
                if (decision.ReasoningProgram != null)
                {
                    dto.ReasoningProgram = WriteReasoningProgram(decision.ReasoningProgram);
                }

                if (decision.Resolution != null)
                {
                    DecisionResolution resolution = decision.Resolution;
                    dto.HasResolution = true;
                    dto.ResolvedOptionId = resolution.ChosenOptionId.Value;
                    dto.ResolvedDegree = (int)resolution.Degree;
                    dto.ResolvedAtMinutes = resolution.ResolvedAt.TotalMinutes;
                    dto.ResolutionSource = (int)resolution.Source;

                    for (int i = 0; i < resolution.OptionTotals.Count; i++)
                    {
                        OptionTotal total = resolution.OptionTotals[i];
                        dto.OptionTotals.Add(new OptionTotalData
                        {
                            OptionId = total.OptionId.Value,
                            Total = total.Total,
                            OrderIndex = total.OrderIndex,
                        });
                    }

                    for (int i = 0; i < resolution.Rolls.Count; i++)
                    {
                        InfluenceRoll roll = resolution.Rolls[i];
                        dto.Rolls.Add(new InfluenceRollData
                        {
                            InfluenceId = roll.InfluenceId.Value,
                            OptionId = roll.OptionId.Value,
                            DieSides = roll.Die.Sides,
                            DieFixedResult = roll.Die.FixedResult,
                            Rolled = roll.Rolled,
                            RollIndex = roll.RollIndex,
                            Polarity = (int)roll.Polarity,
                            Reason = WriteFrozenReason(roll.Reason),
                        });
                    }
                    for (int i = 0; i < resolution.SupersededRolls.Count; i++)
                        dto.SupersededRolls.Add(WriteInfluenceRoll(resolution.SupersededRolls[i]));
                }

                if (decision.PendingResolution != null)
                {
                    PendingDecisionResolution pending = decision.PendingResolution;
                    dto.HasPendingResolution = true;
                    dto.PendingProducedAtMinutes = pending.ProducedAt.TotalMinutes;
                    dto.PendingExpiresAtMinutes = pending.ExpiresAt.TotalMinutes;
                    dto.PendingExpiryEventId = pending.ExpiryEventId.Value;
                    for (int i = 0; i < pending.AcceptedRolls.Count; i++) dto.PendingRolls.Add(WriteInfluenceRoll(pending.AcceptedRolls[i]));
                    for (int i = 0; i < pending.SupersededRolls.Count; i++) dto.SupersededRolls.Add(WriteInfluenceRoll(pending.SupersededRolls[i]));
                }

                data.Decisions.Add(dto);
            }
        }

        private static void ReadDecisions(WorldState world, SaveGameData data)
        {
            for (int i = 0; i < data.Decisions.Count; i++)
            {
                DecisionData dto = data.Decisions[i];

                var options = new DecisionOption[dto.Options.Count];
                for (int o = 0; o < options.Length; o++)
                {
                    DecisionOptionData option = dto.Options[o];
                    var context = new SortedDictionary<AuthoredId, DecisionParameterValue>();
                    for (int p = 0; p < option.Context.Count; p++)
                    {
                        DecisionParameterData parameter = option.Context[p];
                        context[new AuthoredId(parameter.Key)] = ReadDecisionParameter(parameter);
                    }
                    options[o] = new DecisionOption(
                        new AuthoredId(option.Id), new AuthoredId(option.LabelId), option.OrderIndex, context,
                        ReadCommitmentPlan(option.CommitmentResolutionPlan));
                }

                var decision = new Decision(
                    new DecisionId(dto.Id),
                    new CharacterId(dto.CharacterId),
                    new AuthoredId(dto.DefinitionId),
                    new SimTime(dto.CreatedAtMinutes),
                    new SimTime(dto.ResolveAtMinutes),
                    options,
                    new DecisionConflictScope(
                        new AuthoredId(dto.ConflictScopeKind),
                        new EntityRef((EntityKind)dto.ConflictScopeEntityKind, dto.ConflictScopeRuntimeId)),
                    dto.Importance);
                if (dto.HasCommitmentConflict)
                {
                    var participants = new CommitmentId[dto.ConflictCommitmentIds.Count];
                    for (int c = 0; c < participants.Length; c++) participants[c] = new CommitmentId(dto.ConflictCommitmentIds[c]);
                    decision.RestoreCommitmentConflict(
                        new CommitmentConflictKey(new CharacterId(dto.CharacterId), participants, dto.ConflictInstanceRevision),
                        new SimTime(dto.LatestResolutionAtMinutes));
                }

                // Ascending influence id, so restored ids match the ids interventions were bound to.
                dto.Influences.Sort((a, b) => a.Id.CompareTo(b.Id));

                for (int f = 0; f < dto.Influences.Count; f++)
                {
                    DecisionInfluenceData influence = dto.Influences[f];
                    decision.RestoreInfluence(
                        new DecisionInfluenceId(influence.Id),
                        new AuthoredId(influence.OptionId),
                        new AuthoredId(influence.Category),
                        new AuthoredId(influence.LabelId),
                        new Die(influence.BaseDieSides, influence.BaseDieFixedResult),
                        new Die(influence.CurrentDieSides, influence.CurrentDieFixedResult),
                        (InfluenceVisibility)influence.Visibility,
                        influence.RollIndex,
                        influence.IsRetracted,
                        new DecisionDependencyKey(
                            new AuthoredId(influence.DependencyContextKind),
                            new EntityRef((EntityKind)influence.DependencyEntityKind, influence.DependencyRuntimeId)),
                        new EntityRef((EntityKind)influence.SubjectEntityKind, influence.SubjectRuntimeId),
                        (InfluencePolarity)influence.Polarity,
                        new AuthoredId(influence.ReasonChannelId),
                        new AuthoredId(influence.ReasonBindingId),
                        ReadReasonEvaluation(influence.Evaluation));
                }

                for (int v = 0; v < dto.Interventions.Count; v++)
                {
                    AppliedInterventionData intervention = dto.Interventions[v];
                    decision.RestoreIntervention(new AppliedIntervention(
                        new AuthoredId(intervention.InterventionDefinitionId),
                        new DecisionInfluenceId(intervention.TargetInfluenceId),
                        intervention.CommandSequence,
                        (InterventionKind)intervention.Kind,
                        new Die(intervention.ReplacementDieSides, intervention.ReplacementDieFixedResult),
                        (InterventionResourceKind)intervention.ResourceKind,
                        intervention.ResourceCost));
                }

                for (int k = 0; k < dto.DependencyKeys.Count; k++)
                {
                    DependencyKeyData key = dto.DependencyKeys[k];
                    decision.RegisterDependency(new DecisionDependencyKey(
                        new AuthoredId(key.ContextKind),
                        new EntityRef((EntityKind)key.SubjectEntityKind, key.SubjectRuntimeId)));
                }

                for (int p = 0; p < dto.SnapshottedParameters.Count; p++)
                {
                    decision.SnapshotParameter(new AuthoredId(dto.SnapshottedParameters[p].Key), dto.SnapshottedParameters[p].Value);
                }

                for (int p = 0; p < dto.ContextParameters.Count; p++)
                {
                    DecisionParameterData parameter = dto.ContextParameters[p];
                    decision.SetContextParameter(new AuthoredId(parameter.Key), ReadDecisionParameter(parameter));
                }
                if (dto.ReasoningProgram != null)
                {
                    decision.RestoreReasoningProgram(ReadReasoningProgram(dto.ReasoningProgram));
                }

                DecisionResolution resolution = null;
                if (dto.HasResolution)
                {
                    var totals = new OptionTotal[dto.OptionTotals.Count];
                    for (int t = 0; t < totals.Length; t++)
                    {
                        OptionTotalData total = dto.OptionTotals[t];
                        totals[t] = new OptionTotal(new AuthoredId(total.OptionId), total.Total, total.OrderIndex);
                    }

                    var rolls = new InfluenceRoll[dto.Rolls.Count];
                    for (int r = 0; r < rolls.Length; r++)
                    {
                        InfluenceRollData roll = dto.Rolls[r];
                        rolls[r] = new InfluenceRoll(
                            new DecisionInfluenceId(roll.InfluenceId),
                            new AuthoredId(roll.OptionId),
                            new Die(roll.DieSides, roll.DieFixedResult),
                            roll.Rolled,
                            roll.RollIndex,
                            (InfluencePolarity)roll.Polarity,
                            ReadFrozenReason(roll.Reason));
                    }

                    resolution = new DecisionResolution(
                        new AuthoredId(dto.ResolvedOptionId),
                        (DegreeOfSuccess)dto.ResolvedDegree,
                        new SimTime(dto.ResolvedAtMinutes),
                        totals,
                        rolls,
                        (OutcomeSource)dto.ResolutionSource,
                        ReadInfluenceRolls(dto.SupersededRolls));
                }


                if (dto.HasPendingResolution)
                {
                    var accepted = new InfluenceRoll[dto.PendingRolls.Count];
                    var superseded = new InfluenceRoll[dto.SupersededRolls.Count];
                    for (int r = 0; r < accepted.Length; r++) accepted[r] = ReadInfluenceRoll(dto.PendingRolls[r]);
                    for (int r = 0; r < superseded.Length; r++) superseded[r] = ReadInfluenceRoll(dto.SupersededRolls[r]);
                    decision.RestorePendingResolution(new PendingDecisionResolution(
                        new SimTime(dto.PendingProducedAtMinutes), new SimTime(dto.PendingExpiresAtMinutes),
                        new ScheduledEventId(dto.PendingExpiryEventId), accepted, superseded));
                }

                decision.SetPendingResolveEvent(new ScheduledEventId(dto.PendingResolveEventId));
                if (dto.ResolutionHistoryEntryId > 0)
                {
                    decision.LinkResolutionHistory(new HistoryEntryId(dto.ResolutionHistoryEntryId));
                }
                decision.RestoreInfluenceRevision(dto.InfluenceRevision);
                decision.RestoreStatus((DecisionStatus)dto.Status, resolution);

                world.Decisions.Add(decision.Id, decision);
            }
        }

        private static InfluenceRollData WriteInfluenceRoll(InfluenceRoll roll) => new InfluenceRollData
        {
            InfluenceId = roll.InfluenceId.Value, OptionId = roll.OptionId.Value, DieSides = roll.Die.Sides,
            DieFixedResult = roll.Die.FixedResult, Rolled = roll.Rolled, RollIndex = roll.RollIndex,
            Polarity = (int)roll.Polarity, Reason = WriteFrozenReason(roll.Reason),
        };

        private static InfluenceRoll ReadInfluenceRoll(InfluenceRollData roll) => new InfluenceRoll(
            new DecisionInfluenceId(roll.InfluenceId), new AuthoredId(roll.OptionId),
            new Die(roll.DieSides, roll.DieFixedResult), roll.Rolled, roll.RollIndex,
            (InfluencePolarity)roll.Polarity, ReadFrozenReason(roll.Reason));

        private static InfluenceRoll[] ReadInfluenceRolls(List<InfluenceRollData> rolls)
        {
            var result = new InfluenceRoll[rolls.Count];
            for (int i = 0; i < result.Length; i++) result[i] = ReadInfluenceRoll(rolls[i]);
            return result;
        }

        private static CommitmentResolutionPlanData WriteCommitmentPlan(CommitmentResolutionPlan plan)
        {
            var dto = new CommitmentResolutionPlanData { PlanId = plan.PlanId.Value };
            for (int i = 0; i < plan.Preserve.Count; i++) dto.Preserve.Add(plan.Preserve[i].Value);
            for (int i = 0; i < plan.Defer.Count; i++) dto.Defer.Add(plan.Defer[i].Value);
            for (int i = 0; i < plan.Relinquish.Count; i++) dto.Relinquish.Add(plan.Relinquish[i].Value);
            return dto;
        }

        private static CommitmentResolutionPlan ReadCommitmentPlan(CommitmentResolutionPlanData dto)
        {
            if (dto == null) return null;
            var preserve = new CommitmentId[dto.Preserve.Count];
            var defer = new CommitmentId[dto.Defer.Count];
            var relinquish = new CommitmentId[dto.Relinquish.Count];
            for (int i = 0; i < preserve.Length; i++) preserve[i] = new CommitmentId(dto.Preserve[i]);
            for (int i = 0; i < defer.Length; i++) defer[i] = new CommitmentId(dto.Defer[i]);
            for (int i = 0; i < relinquish.Length; i++) relinquish[i] = new CommitmentId(dto.Relinquish[i]);
            return new CommitmentResolutionPlan(new AuthoredId(dto.PlanId), preserve, defer, relinquish);
        }

        private static DecisionParameterData WriteDecisionParameter(
            AuthoredId key,
            DecisionParameterValue value) => new DecisionParameterData
        {
            Key = key.Value,
            Kind = (int)value.Kind,
            Integer = value.Integer,
            AuthoredId = value.AuthoredId.Value,
            EntityKind = (int)value.Entity.Kind,
            RuntimeId = value.Entity.RuntimeId,
        };

        private static DecisionParameterValue ReadDecisionParameter(DecisionParameterData value)
        {
            switch ((DecisionParameterKind)value.Kind)
            {
                case DecisionParameterKind.Integer:
                    return DecisionParameterValue.FromInteger(value.Integer);
                case DecisionParameterKind.AuthoredId:
                    return DecisionParameterValue.FromAuthoredId(new AuthoredId(value.AuthoredId));
                case DecisionParameterKind.Entity:
                    return DecisionParameterValue.FromEntity(new EntityRef((EntityKind)value.EntityKind, value.RuntimeId));
                default:
                    throw new InvalidOperationException($"Unknown Decision parameter kind {value.Kind}.");
            }
        }

        private static DecisionReasonEvaluationData WriteReasonEvaluation(DecisionReasonEvaluation evaluation)
        {
            var data = new DecisionReasonEvaluationData
            {
                ExpectedScore = evaluation?.ExpectedScore ?? 0,
                OutputVariance = evaluation?.OutputVariance ?? 0,
            };
            if (evaluation == null) return data;
            for (int i = 0; i < evaluation.Signals.Count; i++)
            {
                DecisionSignalEvidence signal = evaluation.Signals[i];
                data.Signals.Add(new DecisionSignalEvidenceData
                {
                    SignalId = signal.SignalId.Value, Mean = signal.Mean, Variance = signal.Variance,
                    Applicability = (int)signal.Applicability, SourceRevision = signal.SourceRevision,
                });
            }
            for (int i = 0; i < evaluation.Contributions.Count; i++)
            {
                DecisionContributionEvidence contribution = evaluation.Contributions[i];
                data.Contributions.Add(new DecisionContributionEvidenceData
                {
                    Kind = contribution.Kind, SourceId = contribution.SourceId.Value, Amount = contribution.Amount,
                });
            }
            return data;
        }

        private static DecisionReasonEvaluation ReadReasonEvaluation(DecisionReasonEvaluationData data)
        {
            if (data == null) return new DecisionReasonEvaluation(0, 0);
            var signals = new DecisionSignalEvidence[data.Signals.Count];
            for (int i = 0; i < signals.Length; i++)
            {
                DecisionSignalEvidenceData signal = data.Signals[i];
                signals[i] = new DecisionSignalEvidence(
                    new AuthoredId(signal.SignalId), signal.Mean, signal.Variance,
                    (SignalApplicability)signal.Applicability, signal.SourceRevision);
            }
            var contributions = new DecisionContributionEvidence[data.Contributions.Count];
            for (int i = 0; i < contributions.Length; i++)
            {
                contributions[i] = new DecisionContributionEvidence(
                    data.Contributions[i].Kind, new AuthoredId(data.Contributions[i].SourceId),
                    data.Contributions[i].Amount);
            }
            return new DecisionReasonEvaluation(data.ExpectedScore, data.OutputVariance, signals, contributions);
        }

        private static FrozenDecisionReasonData WriteFrozenReason(FrozenDecisionReason reason) => reason == null
            ? null
            : new FrozenDecisionReasonData
            {
                CategoryId = reason.CategoryId.Value,
                LabelId = reason.LabelId.Value,
                ReasonChannelId = reason.ReasonChannelId.Value,
                BindingId = reason.BindingId.Value,
                SubjectEntityKind = (int)reason.Subject.Kind,
                SubjectRuntimeId = reason.Subject.RuntimeId,
                Visibility = (int)reason.Visibility,
                Evaluation = WriteReasonEvaluation(reason.Evaluation),
            };

        private static FrozenDecisionReason ReadFrozenReason(FrozenDecisionReasonData reason) => reason == null
            ? null
            : new FrozenDecisionReason(
                new AuthoredId(reason.CategoryId), new AuthoredId(reason.LabelId),
                new AuthoredId(reason.ReasonChannelId), new AuthoredId(reason.BindingId),
                new EntityRef((EntityKind)reason.SubjectEntityKind, reason.SubjectRuntimeId),
                ReadReasonEvaluation(reason.Evaluation),
                (InfluenceVisibility)reason.Visibility);

        private static DecisionReasoningProgramData WriteReasoningProgram(DecisionReasoningProgram program)
        {
            var result = new DecisionReasoningProgramData();
            for (int i = 0; i < program.Bindings.Count; i++)
            {
                CompiledConsiderationBinding binding = program.Bindings[i];
                var data = new CompiledConsiderationBindingData
                {
                    BindingId = binding.BindingId.Value,
                    ConsiderationId = binding.ConsiderationId.Value,
                    DefinitionVersion = binding.DefinitionVersion,
                    Field = WriteSignalField(binding.Field),
                    ReasonChannelId = binding.ReasonChannel.Id.Value,
                    ConsolidationPolicy = (int)binding.ReasonChannel.ConsolidationPolicy,
                    ScaleId = binding.Scale.Id.Value,
                    CategoryId = binding.CategoryId.Value,
                    PositiveLabelId = binding.PositiveLabelId.Value,
                    NegativeLabelId = binding.NegativeLabelId.Value,
                    Visibility = (int)binding.Visibility,
                };
                for (int p = 0; p < binding.ParameterSchema.Count; p++)
                {
                    ConsiderationParameter parameter = binding.ParameterSchema[p];
                    data.ParameterSchema.Add(new ConsiderationParameterData
                    {
                        Id = parameter.Id.Value, Kind = (int)parameter.Kind, Required = parameter.Required,
                    });
                }
                for (int p = 0; p < binding.ParameterBindings.Count; p++)
                {
                    CompiledParameterBinding parameter = binding.ParameterBindings[p];
                    data.ParameterBindings.Add(new CompiledParameterBindingData
                    {
                        ParameterId = parameter.ParameterId.Value,
                        Source = (int)parameter.Source,
                        SourceParameterId = parameter.SourceParameterId.Value,
                        Literal = WriteDecisionParameter(default, parameter.Literal),
                    });
                }
                for (int s = 0; s < binding.Signals.Count; s++)
                {
                    DecisionSignalRequest signal = binding.Signals[s];
                    data.Signals.Add(new DecisionSignalRequestData
                    {
                        SignalId = signal.SignalId.Value, ProviderId = signal.ProviderId.Value,
                    });
                }
                for (int t = 0; t < binding.Scale.Thresholds.Count; t++)
                {
                    ReasonDieThreshold threshold = binding.Scale.Thresholds[t];
                    data.ScaleThresholds.Add(new ReasonDieThresholdData
                    {
                        MinimumMagnitude = threshold.MinimumMagnitude, DieSides = threshold.Die.Sides,
                    });
                }
                result.Bindings.Add(data);
            }
            return result;
        }

        private static DecisionReasoningProgram ReadReasoningProgram(DecisionReasoningProgramData program)
        {
            var bindings = new CompiledConsiderationBinding[program.Bindings.Count];
            for (int i = 0; i < bindings.Length; i++)
            {
                CompiledConsiderationBindingData data = program.Bindings[i];
                var schema = new ConsiderationParameter[data.ParameterSchema.Count];
                for (int p = 0; p < schema.Length; p++)
                {
                    ConsiderationParameterData parameter = data.ParameterSchema[p];
                    schema[p] = new ConsiderationParameter(
                        new AuthoredId(parameter.Id), (DecisionParameterKind)parameter.Kind, parameter.Required);
                }
                var parameterBindings = new CompiledParameterBinding[data.ParameterBindings.Count];
                for (int p = 0; p < parameterBindings.Length; p++)
                {
                    CompiledParameterBindingData parameter = data.ParameterBindings[p];
                    parameterBindings[p] = new CompiledParameterBinding(
                        new AuthoredId(parameter.ParameterId), (ParameterBindingSource)parameter.Source,
                        new AuthoredId(parameter.SourceParameterId), ReadDecisionParameter(parameter.Literal));
                }
                var signals = new DecisionSignalRequest[data.Signals.Count];
                for (int s = 0; s < signals.Length; s++)
                {
                    signals[s] = new DecisionSignalRequest(
                        new AuthoredId(data.Signals[s].SignalId), new AuthoredId(data.Signals[s].ProviderId));
                }
                var thresholds = new ReasonDieThreshold[data.ScaleThresholds.Count];
                for (int t = 0; t < thresholds.Length; t++)
                {
                    thresholds[t] = new ReasonDieThreshold(
                        data.ScaleThresholds[t].MinimumMagnitude, new Die(data.ScaleThresholds[t].DieSides));
                }
                bindings[i] = new CompiledConsiderationBinding(
                    new AuthoredId(data.BindingId), new AuthoredId(data.ConsiderationId), data.DefinitionVersion,
                    schema, parameterBindings, signals, ReadSignalField(data.Field),
                    new ReasonChannelDefinition(
                        new AuthoredId(data.ReasonChannelId),
                        (ReasonChannelConsolidationPolicy)data.ConsolidationPolicy),
                    new ReasonScaleProfile(new AuthoredId(data.ScaleId), thresholds),
                    new AuthoredId(data.CategoryId), new AuthoredId(data.PositiveLabelId),
                    new AuthoredId(data.NegativeLabelId), (InfluenceVisibility)data.Visibility);
            }
            return new DecisionReasoningProgram(bindings);
        }

        private static SignalFieldDefinitionData WriteSignalField(SignalFieldDefinition field)
        {
            var data = new SignalFieldDefinitionData
            {
                Id = field.Id.Value, Bias = field.Bias, Revision = field.Revision,
            };
            for (int i = 0; i < field.LinearTerms.Count; i++)
            {
                SignalLinearTerm term = field.LinearTerms[i];
                data.LinearTerms.Add(new SignalLinearTermData
                {
                    Signal = term.Signal.Value, Coefficient = term.Coefficient, Provenance = term.Provenance.Value,
                });
            }
            for (int i = 0; i < field.PairwiseTerms.Count; i++)
            {
                SignalPairwiseTerm term = field.PairwiseTerms[i];
                data.PairwiseTerms.Add(new SignalPairwiseTermData
                {
                    First = term.Pair.First.Value, Second = term.Pair.Second.Value,
                    Coefficient = term.Coefficient, Provenance = term.Provenance.Value,
                });
            }
            foreach (KeyValuePair<AuthoredId, long> ideal in field.IdealPoint)
            {
                data.IdealPoint.Add(new AuthoredLongData { Key = ideal.Key.Value, Value = ideal.Value });
            }
            for (int i = 0; i < field.IdealFactors.Count; i++)
            {
                SignalIdealFactor factor = field.IdealFactors[i];
                var factorData = new SignalIdealFactorData { Id = factor.Id.Value, Provenance = factor.Provenance.Value };
                for (int c = 0; c < factor.Coefficients.Count; c++)
                {
                    SignalLinearTerm term = factor.Coefficients[c];
                    factorData.Coefficients.Add(new SignalLinearTermData
                    {
                        Signal = term.Signal.Value, Coefficient = term.Coefficient, Provenance = term.Provenance.Value,
                    });
                }
                data.IdealFactors.Add(factorData);
            }
            return data;
        }

        private static SignalFieldDefinition ReadSignalField(SignalFieldDefinitionData data)
        {
            var linear = new SignalLinearTerm[data.LinearTerms.Count];
            for (int i = 0; i < linear.Length; i++)
            {
                linear[i] = new SignalLinearTerm(
                    new AuthoredId(data.LinearTerms[i].Signal), data.LinearTerms[i].Coefficient,
                    new AuthoredId(data.LinearTerms[i].Provenance));
            }
            var pairwise = new SignalPairwiseTerm[data.PairwiseTerms.Count];
            for (int i = 0; i < pairwise.Length; i++)
            {
                pairwise[i] = new SignalPairwiseTerm(
                    new AuthoredId(data.PairwiseTerms[i].First), new AuthoredId(data.PairwiseTerms[i].Second),
                    data.PairwiseTerms[i].Coefficient, new AuthoredId(data.PairwiseTerms[i].Provenance));
            }
            var ideal = new SortedDictionary<AuthoredId, long>();
            for (int i = 0; i < data.IdealPoint.Count; i++)
            {
                ideal[new AuthoredId(data.IdealPoint[i].Key)] = data.IdealPoint[i].Value;
            }
            var factors = new SignalIdealFactor[data.IdealFactors.Count];
            for (int i = 0; i < factors.Length; i++)
            {
                SignalIdealFactorData factor = data.IdealFactors[i];
                var coefficients = new SignalLinearTerm[factor.Coefficients.Count];
                for (int c = 0; c < coefficients.Length; c++)
                {
                    coefficients[c] = new SignalLinearTerm(
                        new AuthoredId(factor.Coefficients[c].Signal), factor.Coefficients[c].Coefficient,
                        new AuthoredId(factor.Coefficients[c].Provenance));
                }
                factors[i] = new SignalIdealFactor(
                    new AuthoredId(factor.Id), coefficients, new AuthoredId(factor.Provenance));
            }
            return new SignalFieldDefinition(
                new AuthoredId(data.Id), data.Bias, linear, pairwise, ideal, factors, data.Revision);
        }

        // ---------- knowledge ----------

        private static void WriteKnowledge(WorldState world, SaveGameData data)
        {
            foreach (KnowledgeEntry entry in world.Knowledge.AllObservers)
            {
                data.Knowledge.Add(new KnowledgeEntryData
                {
                    ObserverKind = (int)entry.Observer.Kind,
                    ObserverCharacterId = entry.Observer.CharacterId.Value,
                    FactKind = entry.Key.Kind.Value,
                    SubjectEntityKind = (int)entry.Key.Subject.Kind,
                    SubjectRuntimeId = entry.Key.Subject.RuntimeId,
                    Qualifier = entry.Key.Qualifier.Value,
                    ObservedBand = entry.ObservedValue.Band.Value,
                    ObservedMagnitude = entry.ObservedValue.Magnitude ?? 0,
                    HasObservedMagnitude = entry.ObservedValue.Magnitude.HasValue,
                    ObservedAtMinutes = entry.ObservedAt.TotalMinutes,
                    Confidence = (int)entry.Confidence,
                    SourceChannelId = entry.Source.ChannelId.Value,
                    InformantEntityKind = (int)entry.Source.Informant.Kind,
                    InformantRuntimeId = entry.Source.Informant.RuntimeId,
                    SourceHistoryEntryId = entry.Source.SourceHistoryEntryId.Value,
                    SourceOutcomeId = entry.Source.SourceOutcomeId.Value,
                });
            }

            foreach (KeyValuePair<SocialBeliefKey, BeliefDistribution> pair in world.Knowledge.SocialBeliefs)
            {
                var belief = new SocialBeliefData
                {
                    ObserverKind = (int)pair.Key.Observer.Kind,
                    ObserverCharacterId = pair.Key.Observer.CharacterId.Value,
                    TargetCharacterId = pair.Key.Target.Value,
                    EvidenceRevision = pair.Value.EvidenceRevision,
                };
                SocialBeliefMetadata metadata = world.Knowledge.SocialBeliefMetadataOf(pair.Key);
                belief.Retention = (int)metadata.Retention;
                belief.LastUpdatedAtMinutes = metadata.LastUpdatedAt.TotalMinutes;
                WriteVector(pair.Value.Mean, belief.Mean);
                foreach (KeyValuePair<SocialDimensionPair, long> covariance in pair.Value.CovarianceTerms)
                {
                    belief.Covariance.Add(new CovarianceData
                    {
                        FirstDimension = covariance.Key.First.Value,
                        SecondDimension = covariance.Key.Second.Value,
                        Value = covariance.Value,
                    });
                }
                data.SocialBeliefs.Add(belief);
            }
        }

        private static void ReadKnowledge(WorldState world, SaveGameData data)
        {
            for (int i = 0; i < data.Knowledge.Count; i++)
            {
                KnowledgeEntryData dto = data.Knowledge[i];
                world.Knowledge.Record(new KnowledgeEntry(
                    new FactKey(
                        new AuthoredId(dto.FactKind),
                        new EntityRef((EntityKind)dto.SubjectEntityKind, dto.SubjectRuntimeId),
                        new AuthoredId(dto.Qualifier)),
                    new ObservedValue(
                        new AuthoredId(dto.ObservedBand),
                        dto.HasObservedMagnitude ? dto.ObservedMagnitude : (long?)null),
                    new SimTime(dto.ObservedAtMinutes),
                    (KnowledgeConfidence)dto.Confidence,
                    new DiscoverySource(
                        new AuthoredId(dto.SourceChannelId),
                        new EntityRef((EntityKind)dto.InformantEntityKind, dto.InformantRuntimeId),
                        new HistoryEntryId(dto.SourceHistoryEntryId),
                        new CommitmentOutcomeId(dto.SourceOutcomeId)),
                    new ObserverRef((ObserverKind)dto.ObserverKind, new CharacterId(dto.ObserverCharacterId))));
            }

            for (int i = 0; i < data.SocialBeliefs.Count; i++)
            {
                SocialBeliefData dto = data.SocialBeliefs[i];
                var belief = new BeliefDistribution(ReadVector(dto.Mean), dto.EvidenceRevision);
                for (int c = 0; c < dto.Covariance.Count; c++)
                {
                    CovarianceData covariance = dto.Covariance[c];
                    belief.SetCovariance(
                        new AuthoredId(covariance.FirstDimension),
                        new AuthoredId(covariance.SecondDimension),
                        covariance.Value);
                }
                world.Knowledge.SetSocialBelief(
                    new ObserverRef((ObserverKind)dto.ObserverKind, new CharacterId(dto.ObserverCharacterId)),
                    new CharacterId(dto.TargetCharacterId),
                    belief,
                    new SimTime(dto.LastUpdatedAtMinutes),
                    (SocialBeliefRetention)dto.Retention);
            }
        }

        // ---------- attention ----------

        private static void WriteAttention(WorldState world, SaveGameData data)
        {
            foreach (Character character in world.Characters.All)
            {
                WatchState watch = world.Attention.WatchStateOf(character.Id);
                if (watch.IsFollowed)
                {
                    data.Attention.FollowedCharacters.Add(character.Id.Value);
                }

                AttentionPolicy policy = world.Attention.PolicyFor(character.Id);
                if (policy != AttentionPolicy.Normal)
                {
                    data.Attention.CharacterPolicies.Add(new CharacterPolicyData
                    {
                        CharacterId = character.Id.Value,
                        Policy = (int)policy,
                    });
                }

                int ordinal = world.Attention.ObservationOrdinal(character.Id);
                if (ordinal != 0)
                {
                    data.Attention.ObservationOrdinals.Add(new ObservationOrdinalData
                    {
                        CharacterId = character.Id.Value,
                        Ordinal = ordinal,
                    });
                }
            }

            foreach (DecisionId held in world.Attention.HeldDecisions)
            {
                data.Attention.HeldDecisions.Add(held.Value);
            }

            foreach (Decision decision in world.Decisions.All)
            {
                AttentionPolicy policy = world.Attention.PolicyFor(decision.Id);
                if (policy != AttentionPolicy.Normal)
                {
                    data.Attention.DecisionPolicies.Add(new DecisionPolicyData
                    {
                        DecisionId = decision.Id.Value,
                        Policy = (int)policy,
                    });
                }
            }
        }

        private static void ReadAttention(WorldState world, SaveGameData data)
        {
            for (int i = 0; i < data.Attention.FollowedCharacters.Count; i++)
            {
                var character = new CharacterId(data.Attention.FollowedCharacters[i]);
                world.Attention.SetWatchState(character, world.Attention.WatchStateOf(character).WithFollowed(true));
            }

            for (int i = 0; i < data.Attention.CharacterPolicies.Count; i++)
            {
                CharacterPolicyData policy = data.Attention.CharacterPolicies[i];
                world.Attention.SetPolicy(new CharacterId(policy.CharacterId), (AttentionPolicy)policy.Policy);
            }

            for (int i = 0; i < data.Attention.DecisionPolicies.Count; i++)
            {
                DecisionPolicyData policy = data.Attention.DecisionPolicies[i];
                world.Attention.SetPolicy(new DecisionId(policy.DecisionId), (AttentionPolicy)policy.Policy);
            }

            for (int i = 0; i < data.Attention.HeldDecisions.Count; i++)
            {
                world.Attention.Hold(new DecisionId(data.Attention.HeldDecisions[i]));
            }

            for (int i = 0; i < data.Attention.ObservationOrdinals.Count; i++)
            {
                ObservationOrdinalData ordinal = data.Attention.ObservationOrdinals[i];
                world.Attention.RestoreObservationOrdinal(new CharacterId(ordinal.CharacterId), ordinal.Ordinal);
            }
        }

        // ---------- history ----------

        private static void WriteHistory(WorldState world, SaveGameData data)
        {
            foreach (HistoryEntry entry in world.HistoryLedger.SignificantAndLegacy)
            {
                var dto = new HistoryEntryData
                {
                    Id = entry.Id.Value,
                    Kind = entry.Kind.Value,
                    OccurredAtMinutes = entry.OccurredAt.TotalMinutes,
                    Tier = (int)entry.Tier,
                    Summary = entry.Summary,
                    SourceOutcomeId = entry.SourceOutcomeId.Value,
                };

                for (int i = 0; i < entry.Subjects.Count; i++)
                {
                    dto.Subjects.Add(new EntityRefData
                    {
                        EntityKind = (int)entry.Subjects[i].Kind,
                        RuntimeId = entry.Subjects[i].RuntimeId,
                    });
                }

                data.SignificantHistory.Add(dto);
            }
        }

        private static void ReadHistory(WorldState world, SaveGameData data)
        {
            for (int i = 0; i < data.SignificantHistory.Count; i++)
            {
                HistoryEntryData dto = data.SignificantHistory[i];

                var subjects = new EntityRef[dto.Subjects.Count];
                for (int s = 0; s < subjects.Length; s++)
                {
                    subjects[s] = new EntityRef((EntityKind)dto.Subjects[s].EntityKind, dto.Subjects[s].RuntimeId);
                }

                world.HistoryLedger.Restore(new HistoryEntry(
                    new HistoryEntryId(dto.Id),
                    new AuthoredId(dto.Kind),
                    new SimTime(dto.OccurredAtMinutes),
                    (RetentionTier)dto.Tier,
                    dto.Summary,
                    subjects,
                    new CommitmentOutcomeId(dto.SourceOutcomeId)));
            }
        }

        // ---------- shared ----------

        private static void WriteVector(SocialVector vector, List<AuthoredLongData> target)
        {
            foreach (KeyValuePair<AuthoredId, long> value in vector.All)
            {
                target.Add(new AuthoredLongData { Key = value.Key.Value, Value = value.Value });
            }
        }

        private static SocialVector ReadVector(List<AuthoredLongData> source)
        {
            var vector = new SocialVector();
            for (int i = 0; i < source.Count; i++)
            {
                vector.Set(new AuthoredId(source[i].Key), source[i].Value);
            }
            return vector;
        }

        private static void WriteTags(WeightedTagSet tags, List<AuthoredLongData> target)
        {
            foreach (KeyValuePair<AuthoredId, long> value in tags.All)
            {
                target.Add(new AuthoredLongData { Key = value.Key.Value, Value = value.Value });
            }
        }

        private static void ReadTags(WeightedTagSet tags, List<AuthoredLongData> source, int revision)
        {
            for (int i = 0; i < source.Count; i++)
            {
                tags.Restore(new AuthoredId(source[i].Key), source[i].Value);
            }
            tags.RestoreRevision(revision);
        }

        private static AppraisalFieldData ToDto(AppraisalField field)
        {
            var dto = new AppraisalFieldData
            {
                LensId = field.LensId.Value,
                Bias = field.Bias,
                CalibrationProfileId = field.CalibrationProfileId.Value,
                Revision = field.Revision,
            };
            WriteLinear(field.LinearTerms, dto.LinearTerms);
            WritePairwise(field.PairwiseTerms, dto.PairwiseTerms);
            WriteVector(field.IdealPoint, dto.IdealPoint);
            WriteFactors(field.IdealFactors, dto.IdealFactors);
            for (int i = 0; i < field.ContextModifiers.Count; i++)
            {
                AppraisalContextModifier modifier = field.ContextModifiers[i];
                var modifierDto = new AppraisalContextModifierData
                {
                    ContextId = modifier.ContextId.Value,
                    BiasDelta = modifier.BiasDelta,
                    Provenance = modifier.Provenance.Value,
                };
                WriteLinear(modifier.LinearDeltas, modifierDto.LinearDeltas);
                WritePairwise(modifier.PairwiseDeltas, modifierDto.PairwiseDeltas);
                WriteVector(modifier.IdealPointDelta, modifierDto.IdealPointDelta);
                WriteFactors(modifier.IdealFactorDeltas, modifierDto.IdealFactorDeltas);
                dto.ContextModifiers.Add(modifierDto);
            }
            return dto;
        }

        private static AppraisalField FromDto(AppraisalFieldData dto, CharacterId observerId)
        {
            var modifiers = new AppraisalContextModifier[dto.ContextModifiers.Count];
            for (int i = 0; i < modifiers.Length; i++)
            {
                AppraisalContextModifierData source = dto.ContextModifiers[i];
                modifiers[i] = new AppraisalContextModifier(
                    new AuthoredId(source.ContextId),
                    source.BiasDelta,
                    ReadLinear(source.LinearDeltas),
                    ReadPairwise(source.PairwiseDeltas),
                    ReadVector(source.IdealPointDelta),
                    ReadFactors(source.IdealFactorDeltas),
                    new AuthoredId(source.Provenance));
            }

            return new AppraisalField(
                observerId,
                new AuthoredId(dto.LensId),
                dto.Bias,
                ReadLinear(dto.LinearTerms),
                ReadPairwise(dto.PairwiseTerms),
                ReadVector(dto.IdealPoint),
                ReadFactors(dto.IdealFactors),
                modifiers,
                new AuthoredId(dto.CalibrationProfileId),
                dto.Revision);
        }

        private static void WriteLinear(IReadOnlyList<SocialLinearTerm> source, List<SocialTermData> target)
        {
            for (int i = 0; i < source.Count; i++)
            {
                target.Add(new SocialTermData
                {
                    FirstDimension = source[i].Dimension.Value,
                    Coefficient = source[i].Coefficient,
                    Provenance = source[i].Provenance.Value,
                });
            }
        }

        private static SocialLinearTerm[] ReadLinear(List<SocialTermData> source)
        {
            var result = new SocialLinearTerm[source.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new SocialLinearTerm(
                    new AuthoredId(source[i].FirstDimension),
                    source[i].Coefficient,
                    new AuthoredId(source[i].Provenance));
            }
            return result;
        }

        private static void WritePairwise(IReadOnlyList<SocialPairwiseTerm> source, List<SocialTermData> target)
        {
            for (int i = 0; i < source.Count; i++)
            {
                target.Add(new SocialTermData
                {
                    FirstDimension = source[i].Pair.First.Value,
                    SecondDimension = source[i].Pair.Second.Value,
                    Coefficient = source[i].Coefficient,
                    Provenance = source[i].Provenance.Value,
                });
            }
        }

        private static SocialPairwiseTerm[] ReadPairwise(List<SocialTermData> source)
        {
            var result = new SocialPairwiseTerm[source.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new SocialPairwiseTerm(
                    new AuthoredId(source[i].FirstDimension),
                    new AuthoredId(source[i].SecondDimension),
                    source[i].Coefficient,
                    new AuthoredId(source[i].Provenance));
            }
            return result;
        }

        private static void WriteFactors(IReadOnlyList<IdealFactor> source, List<IdealFactorData> target)
        {
            for (int i = 0; i < source.Count; i++)
            {
                var dto = new IdealFactorData { Id = source[i].Id.Value, Provenance = source[i].Provenance.Value };
                WriteLinear(source[i].Coefficients, dto.Coefficients);
                target.Add(dto);
            }
        }

        private static IdealFactor[] ReadFactors(List<IdealFactorData> source)
        {
            var result = new IdealFactor[source.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new IdealFactor(
                    new AuthoredId(source[i].Id),
                    ReadLinear(source[i].Coefficients),
                    new AuthoredId(source[i].Provenance));
            }
            return result;
        }

        private static DirectionalRelationshipData ToDto(DirectionalRelationshipState direction)
        {
            var dto = new DirectionalRelationshipData
            {
                ObserverId = direction.ObserverId.Value,
                TargetId = direction.TargetId.Value,
                Familiarity = (int)direction.FamiliarityAt(direction.LastInteractionAt ?? direction.EstablishedAt),
                FamiliarityProgression = ToDto(direction.FamiliarityProgression),
                HasFamiliarityProgression = true,
                ExposureMinutes = direction.ExposureMinutes,
                LastInteractionAtMinutes = direction.LastInteractionAt?.TotalMinutes ?? -1,
                Revision = direction.Revision,
            };
            foreach (KeyValuePair<AuthoredId, AnalyticalProgression> channel in direction.Channels)
            {
                dto.Channels.Add(new RelationshipChannelData
                {
                    ChannelId = channel.Key.Value,
                    Progression = ToDto(channel.Value),
                });
            }
            for (int i = 0; i < direction.Memories.Count; i++)
            {
                RelationshipMemory memory = direction.Memories[i];
                var memoryDto = new RelationshipMemoryData
                {
                    MemoryKind = memory.MemoryKind.Value,
                    OccurredAtMinutes = memory.OccurredAt.TotalMinutes,
                    ExplanationId = memory.ExplanationId.Value,
                    SourceHistoryEntryId = memory.SourceHistoryEntryId.Value,
                    SourceOutcomeId = memory.SourceOutcomeId.Value,
                };
                foreach (KeyValuePair<AuthoredId, long> effect in memory.ChannelEffects)
                {
                    memoryDto.ChannelEffects.Add(new AuthoredLongData { Key = effect.Key.Value, Value = effect.Value });
                }
                dto.Memories.Add(memoryDto);
            }
            return dto;
        }

        private static void RestoreDirection(DirectionalRelationshipState direction, DirectionalRelationshipData dto)
        {
            if (dto == null || dto.ObserverId == 0)
            {
                return;
            }
            for (int i = 0; i < dto.Channels.Count; i++)
            {
                direction.SetChannel(new AuthoredId(dto.Channels[i].ChannelId), FromDto(dto.Channels[i].Progression));
            }
            for (int i = 0; i < dto.Memories.Count; i++)
            {
                RelationshipMemoryData source = dto.Memories[i];
                var effects = new SortedDictionary<AuthoredId, long>();
                for (int e = 0; e < source.ChannelEffects.Count; e++)
                {
                    effects.Add(new AuthoredId(source.ChannelEffects[e].Key), source.ChannelEffects[e].Value);
                }
                direction.AddMemory(new RelationshipMemory(
                    new AuthoredId(source.MemoryKind),
                    new SimTime(source.OccurredAtMinutes),
                    new AuthoredId(source.ExplanationId),
                    effects,
                    new HistoryEntryId(source.SourceHistoryEntryId),
                    new CommitmentOutcomeId(source.SourceOutcomeId)));
            }
            direction.RestoreState(
                dto.HasFamiliarityProgression
                    ? FromDto(dto.FamiliarityProgression)
                    : AnalyticalProgression.Constant(dto.Familiarity, new SimTime(dto.LastInteractionAtMinutes < 0 ? 0 : dto.LastInteractionAtMinutes), 0, 10000),
                dto.ExposureMinutes,
                dto.LastInteractionAtMinutes >= 0 ? new SimTime(dto.LastInteractionAtMinutes) : (SimTime?)null,
                dto.Revision);
        }

        private static ProgressionData ToDto(AnalyticalProgression progression) => new ProgressionData
        {
            ValueAtAnchor = progression.ValueAtAnchor,
            AnchoredAtMinutes = progression.AnchoredAt.TotalMinutes,
            RateNumerator = progression.RatePerMinuteNumerator,
            RateDenominator = progression.RatePerMinuteDenominator,
            MinValue = progression.MinValue,
            MaxValue = progression.MaxValue,
        };

        private static AnalyticalProgression FromDto(ProgressionData dto) => AnalyticalProgression.Linear(
            dto.ValueAtAnchor,
            new SimTime(dto.AnchoredAtMinutes),
            dto.RateNumerator,
            dto.RateDenominator <= 0 ? 1 : dto.RateDenominator,
            dto.MinValue,
            dto.MaxValue);
    }
}
