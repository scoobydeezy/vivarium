using System;
using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Attention;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Groups;
using Vivarium.Domain.History;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Randomness;
using Vivarium.Domain.Relationships;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Spatial;
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

        public SaveGameMapper(ScheduledEventPayloadCodecRegistry payloadCodecs)
        {
            _payloadCodecs = payloadCodecs ?? throw new ArgumentNullException(nameof(payloadCodecs));
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
            };

            RuntimeIdCounters counters = world.RuntimeIds.Snapshot();
            data.RuntimeIdCounters = new RuntimeIdCountersData
            {
                Characters = counters.Characters,
                Activities = counters.Activities,
                Commitments = counters.Commitments,
                Relationships = counters.Relationships,
                Decisions = counters.Decisions,
                Locations = counters.Locations,
                Groups = counters.Groups,
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
                    data.RuntimeIdCounters.Relationships,
                    data.RuntimeIdCounters.Decisions,
                    data.RuntimeIdCounters.Locations,
                    data.RuntimeIdCounters.Groups,
                    data.RuntimeIdCounters.ScheduledEvents,
                    data.RuntimeIdCounters.HistoryEntries,
                    data.RuntimeIdCounters.EventSequence));

            ReadSpatial(world, data);
            ReadCharacters(world, data);
            ReadActivities(world, data);
            ReadCommitments(world, data);
            ReadGroups(world, data);
            ReadRelationships(world, data);
            ReadDecisions(world, data);
            ReadKnowledge(world, data);
            ReadAttention(world, data);
            ReadHistory(world, data);
            ReadRevisions(world, data);
            ReadScheduler(world, data);

            // Canonical state is in; derived structures are rebuilt from it and never trusted from disk.
            world.RebuildDerivedIndexes();

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
                });
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
                world.Locations.Add(new LocationNode(
                    new LocationId(location.Id),
                    new LocationId(location.ParentLocationId),
                    new AuthoredId(location.LocationKindId),
                    location.DisplayName,
                    location.IsOccupiable,
                    location.Capacity));
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
                    new AuthoredId(dto.SourceTemplateId));

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
                    Affinity = ToDto(relationship.Affinity),
                    Familiarity = relationship.Familiarity,
                    EstablishedAtMinutes = relationship.EstablishedAt.TotalMinutes,
                    LastInteractionAtMinutes = relationship.LastInteractionAt?.TotalMinutes ?? -1,
                    IsActive = relationship.IsActive,
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
                    dto.Familiarity,
                    dto.LastInteractionAtMinutes >= 0 ? new SimTime(dto.LastInteractionAtMinutes) : (SimTime?)null,
                    dto.IsActive);

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
                    ConflictScopeKind = decision.ConflictScope.ScopeKind.Value,
                    ConflictScopeEntityKind = (int)decision.ConflictScope.Subject.Kind,
                    ConflictScopeRuntimeId = decision.ConflictScope.Subject.RuntimeId,
                };

                for (int i = 0; i < decision.Options.Count; i++)
                {
                    DecisionOption option = decision.Options[i];
                    dto.Options.Add(new DecisionOptionData
                    {
                        Id = option.Id.Value,
                        LabelId = option.LabelId.Value,
                        OrderIndex = option.OrderIndex,
                    });
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
                        CurrentDieSides = influence.CurrentDie.Sides,
                        Visibility = (int)influence.DefaultVisibility,
                        RollIndex = influence.RollIndex,
                        IsRetracted = influence.IsRetracted,
                        DependencyContextKind = influence.DependencyKey.ContextKind.Value,
                        DependencyEntityKind = (int)influence.DependencyKey.Subject.Kind,
                        DependencyRuntimeId = influence.DependencyKey.Subject.RuntimeId,
                        SubjectEntityKind = (int)influence.Subject.Kind,
                        SubjectRuntimeId = influence.Subject.RuntimeId,
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
                            Rolled = roll.Rolled,
                            RollIndex = roll.RollIndex,
                        });
                    }
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
                    options[o] = new DecisionOption(new AuthoredId(option.Id), new AuthoredId(option.LabelId), option.OrderIndex);
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
                        new Die(influence.BaseDieSides),
                        new Die(influence.CurrentDieSides),
                        (InfluenceVisibility)influence.Visibility,
                        influence.RollIndex,
                        influence.IsRetracted,
                        new DecisionDependencyKey(
                            new AuthoredId(influence.DependencyContextKind),
                            new EntityRef((EntityKind)influence.DependencyEntityKind, influence.DependencyRuntimeId)),
                        new EntityRef((EntityKind)influence.SubjectEntityKind, influence.SubjectRuntimeId));
                }

                for (int v = 0; v < dto.Interventions.Count; v++)
                {
                    AppliedInterventionData intervention = dto.Interventions[v];
                    decision.RestoreIntervention(new AppliedIntervention(
                        new AuthoredId(intervention.InterventionDefinitionId),
                        new DecisionInfluenceId(intervention.TargetInfluenceId),
                        intervention.CommandSequence));
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
                            new Die(roll.DieSides),
                            roll.Rolled,
                            roll.RollIndex);
                    }

                    resolution = new DecisionResolution(
                        new AuthoredId(dto.ResolvedOptionId),
                        (DegreeOfSuccess)dto.ResolvedDegree,
                        new SimTime(dto.ResolvedAtMinutes),
                        totals,
                        rolls,
                        (OutcomeSource)dto.ResolutionSource);
                }

                decision.SetPendingResolveEvent(new ScheduledEventId(dto.PendingResolveEventId));
                decision.RestoreInfluenceRevision(dto.InfluenceRevision);
                decision.RestoreStatus((DecisionStatus)dto.Status, resolution);

                world.Decisions.Add(decision.Id, decision);
            }
        }

        // ---------- knowledge ----------

        private static void WriteKnowledge(WorldState world, SaveGameData data)
        {
            foreach (KnowledgeEntry entry in world.Knowledge.All)
            {
                data.Knowledge.Add(new KnowledgeEntryData
                {
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
                });
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
                        new HistoryEntryId(dto.SourceHistoryEntryId))));
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
                    subjects));
            }
        }

        // ---------- shared ----------

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
