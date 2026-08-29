using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Vivarium.Application.Commands;
using Vivarium.Application.Persistence;
using Vivarium.Application.Queries;
using Vivarium.Application.Session;
using Vivarium.Domain.Attention;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;
using Vivarium.Infrastructure.Bootstrap;
using Vivarium.Infrastructure.Clock;
using Vivarium.Infrastructure.Persistence;

namespace Vivarium.SimRunner
{
    /// <summary>
    /// Runs the Phase 7 longitudinal audit over the same production-shaped MPS world used by Unity.
    /// The orchestration supplies player Commands and time only; all reported life and causality comes
    /// back through existing Application projections.
    /// </summary>
    public static class MvpExperienceAudit
    {
        private const long Seed = 827119;
        private static readonly SimDuration AuditDuration = SimDuration.FromDays(2);
        private static readonly SimDuration DecisionSetupDuration =
            SimDuration.FromHours(5).Plus(SimDuration.FromMinutes(35));

        public static MvpExperienceAuditResult Run()
        {
            AuditHost control = Create();
            SimTime controlStart = control.Host.World.Clock.Now;
            control.Host.Session.Advance(DecisionSetupDuration, SimulationMode.Live);
            Decision controlDecision = FindActiveDecision(
                control.Host.World,
                control.Layout.Mina,
                SampleContent.DecisionLeaveWork);
            if (!control.Host.World.Attention.IsHeld(controlDecision.Id))
                throw new InvalidOperationException("Mina's qualifying control Decision did not Auto-Hold.");
            Require(control.Host.Session.Execute(new ReleaseDecisionCommand(controlDecision.Id)),
                "release Mina's control Decision without influencing it");
            control.Host.Session.Advance(AuditDuration - DecisionSetupDuration, SimulationMode.Live);
            MvpExperienceBranchReport controlReport = Report(
                "intervention-free/live",
                control,
                SimulationMode.Live,
                controlStart);

            AuditHost influencedLive = Create();
            SimTime influencedStart = influencedLive.Host.World.Clock.Now;
            Require(influencedLive.Host.Session.Execute(
                new SetLocationAvailabilityCommand(influencedLive.Layout.Commons, open: false)),
                "close the Commons");
            influencedLive.Host.Session.Advance(SimDuration.FromHours(5), SimulationMode.Live);
            Require(influencedLive.Host.Session.Execute(
                new FollowCharacterCommand(influencedLive.Layout.Mina, true)), "Follow Mina");
            Require(influencedLive.Host.Session.Execute(
                new BeginObservingCharacterCommand(influencedLive.Layout.Mina)), "observe Mina");
            Require(influencedLive.Host.Session.Execute(
                new InspectCharacterCommand(influencedLive.Layout.Mina)), "inspect Mina");
            influencedLive.Host.Session.Advance(SimDuration.FromMinutes(35), SimulationMode.Live);

            Decision minaDecision = FindActiveDecision(
                influencedLive.Host.World,
                influencedLive.Layout.Mina,
                SampleContent.DecisionLeaveWork);
            if (!influencedLive.Host.World.Attention.IsHeld(minaDecision.Id))
            {
                var reasonScores = new List<string>();
                for (int i = 0; i < minaDecision.Influences.Count; i++)
                    reasonScores.Add(minaDecision.Influences[i].LabelId + "=" +
                        minaDecision.Influences[i].Evaluation.ExpectedScore);
                throw new InvalidOperationException(
                    "Mina's qualifying Decision did not Auto-Hold during the audit: importance=" +
                    minaDecision.Importance + ", floor=" + influencedLive.Catalog.DecisionImportancePolicy.AutoHoldFloor +
                    ", reasons=" + string.Join(", ", reasonScores) + ".");
            }
            DecisionInfluence pressure = FindInfluence(minaDecision, SampleContent.InfluenceBadWorkContext);
            Require(influencedLive.Host.Session.Execute(new ApplyDecisionInterventionCommand(
                minaDecision.Id,
                SampleContent.InterventionStepUp,
                pressure.Id)), "Emphasize Mina's visible Work-context reason");
            Require(influencedLive.Host.Session.Execute(new ReleaseDecisionCommand(minaDecision.Id)),
                "release Mina's Decision");
            Require(influencedLive.Host.Session.Execute(
                new SetLocationAvailabilityCommand(influencedLive.Layout.Commons, open: true)),
                "reopen the Commons");
            Require(influencedLive.Host.Session.Execute(
                new SetAttentionPolicyCommand(influencedLive.Layout.Mina, AttentionPolicy.Normal)),
                "return Mina to Normal Attention");

            SaveGameData checkpoint = influencedLive.Host.Session.Save("phase7b-checkpoint");
            SimTime recapSince = influencedLive.Host.World.Clock.Now;
            AuditHost influencedOffline = Restore(influencedLive, checkpoint);
            SimDuration remaining = AuditDuration - DecisionSetupDuration;
            influencedLive.Host.Session.Advance(remaining, SimulationMode.Live);
            influencedOffline.Host.Session.Advance(remaining, SimulationMode.OfflineCatchUp);

            MvpExperienceBranchReport liveReport = Report(
                "intervention-heavy/live",
                influencedLive,
                SimulationMode.Live,
                recapSince);
            MvpExperienceBranchReport offlineReport = Report(
                "intervention-heavy/offline",
                influencedOffline,
                SimulationMode.OfflineCatchUp,
                recapSince);

            return new MvpExperienceAuditResult(
                new[] { controlReport, liveReport, offlineReport },
                liveReport.ContinuationFingerprint == offlineReport.ContinuationFingerprint);
        }

        private static MvpExperienceBranchReport Report(
            string name,
            AuditHost audit,
            SimulationMode recapMode,
            SimTime recapSince)
        {
            WorldState world = audit.Host.World;
            var profileProjector = new CharacterProfileProjector();
            var characters = new List<MvpCharacterAuditRow>();
            var issues = new List<string>();

            foreach (Domain.Characters.Character character in world.Characters.All)
            {
                Require(audit.Host.Session.Execute(new InspectCharacterCommand(character.Id)),
                    "inspect " + character.DisplayName);
                if (!profileProjector.TryProject(world, character.Id, out CharacterProfileView profile))
                {
                    issues.Add(character.DisplayName + " has no character-profile projection.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(profile.CurrentActivityLabel) ||
                    string.Equals(profile.CurrentActivityLabel, "unknown", StringComparison.OrdinalIgnoreCase))
                    issues.Add(character.DisplayName + " has no legible current Activity.");
                if (string.IsNullOrWhiteSpace(profile.LocationLabel) ||
                    string.Equals(profile.LocationLabel, "unknown", StringComparison.OrdinalIgnoreCase))
                    issues.Add(character.DisplayName + " has no legible current location.");

                characters.Add(new MvpCharacterAuditRow(
                    profile.CharacterId,
                    profile.DisplayName,
                    profile.CurrentActivityLabel,
                    profile.LocationLabel,
                    profile.Schedule.Entries.Count,
                    profile.Decisions.Count,
                    profile.RecentHistory.Count,
                    profile.KnownNeeds.Count,
                    profile.KnownRelationships.Count));
            }

            var decisionProjector = new DecisionProjector(audit.Catalog.Interventions);
            var decisions = new List<MvpDecisionAuditRow>();
            foreach (Decision decision in world.Decisions.All)
            {
                DecisionView view = decisionProjector.Project(world, decision);
                int visibleReasons = 0;
                for (int option = 0; option < view.Options.Count; option++)
                {
                    DecisionOptionView optionView = view.Options[option];
                    if (string.IsNullOrWhiteSpace(optionView.Label))
                        issues.Add("Decision #" + view.DecisionId + " has an unlabeled option.");
                    visibleReasons += optionView.Influences.Count;
                }
                if (view.Options.Count < 2)
                    issues.Add("Decision #" + view.DecisionId + " does not expose a meaningful alternative.");
                if (view.Resolution != null && view.Resolution.Reasons.Count == 0)
                    issues.Add("Resolved Decision #" + view.DecisionId + " has no frozen explanation.");

                decisions.Add(new MvpDecisionAuditRow(
                    view.DecisionId,
                    view.CharacterName,
                    view.DefinitionId,
                    view.StatusLabel,
                    view.Options.Count,
                    visibleReasons,
                    view.Resolution?.Reasons.Count ?? 0,
                    view.AppliedInterventions.Count));
            }

            NotificationRecapView recap = new NotificationRecapProjector(
                audit.Catalog.DecisionImportancePolicy).Project(
                    world,
                    recapMode,
                    recapSince,
                    maximumGroups: 12);
            if (recapMode == SimulationMode.OfflineCatchUp && !recap.IsOfflineRecap)
                issues.Add("Offline continuation did not produce an offline recap.");

            string fingerprint = Fingerprint(
                world.Clock.Now.ToString(),
                world.Activities.Count,
                world.Decisions.Count,
                world.Knowledge.Count,
                world.Nudges.Balance,
                world.Locations.Get(audit.Layout.Commons).IsOpen,
                characters,
                decisions);
            return new MvpExperienceBranchReport(
                name,
                world.Clock.Now.ToString(),
                fingerprint,
                world.Activities.Count,
                world.Decisions.Count,
                world.HistoryLedger.Count,
                world.Knowledge.Count,
                world.Nudges.Balance,
                world.Locations.Get(audit.Layout.Commons).IsOpen,
                recap.Entries,
                characters,
                decisions,
                issues);
        }

        private static string Fingerprint(
            string finalTime,
            int activityCount,
            int decisionCount,
            int knowledgeCount,
            int nudgeBalance,
            bool commonsOpen,
            IReadOnlyList<MvpCharacterAuditRow> characters,
            IReadOnlyList<MvpDecisionAuditRow> decisions)
        {
            var text = new StringBuilder();
            text.Append(finalTime).Append('|').Append(activityCount).Append('|').Append(decisionCount)
                .Append('|').Append(knowledgeCount).Append('|').Append(nudgeBalance).Append('|').Append(commonsOpen);
            for (int i = 0; i < characters.Count; i++)
            {
                MvpCharacterAuditRow row = characters[i];
                text.Append('|').Append(row.CharacterId).Append(':').Append(row.Activity).Append('@').Append(row.Location)
                    .Append(':').Append(row.ScheduleCount).Append(':').Append(row.DecisionCount)
                    .Append(':').Append(row.KnownNeedCount).Append(':').Append(row.KnownRelationshipCount);
            }
            for (int i = 0; i < decisions.Count; i++)
            {
                MvpDecisionAuditRow row = decisions[i];
                text.Append('|').Append(row.DecisionId).Append(':').Append(row.DefinitionId).Append(':').Append(row.Status)
                    .Append(':').Append(row.OptionCount).Append(':').Append(row.VisibleReasonCount)
                    .Append(':').Append(row.FrozenReasonCount).Append(':').Append(row.InterventionCount);
            }
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
        }

        private static AuditHost Create()
        {
            DefinitionCatalog catalog = SampleContent.Build();
            var store = new InMemorySaveGameStore();
            var clock = new FixedRealWorldClock(1000000000000L);
            SimulationHost host = SimulationBootstrapper.CreateNewWorld(
                Seed,
                SimTime.FromClockTime(0, 7, 0),
                catalog,
                1,
                null,
                store,
                clock);
            return new AuditHost(catalog, host, MinimumPlayableWorld.Populate(host), store, clock);
        }

        private static AuditHost Restore(AuditHost source, SaveGameData saved)
        {
            WorldState restoredWorld = source.Host.SaveMapper.Restore(saved);
            SimulationHost restored = SimulationBootstrapper.CreateFromRestoredWorld(
                restoredWorld,
                source.Catalog,
                saved.LastCommandSequence,
                1,
                null,
                source.Store,
                source.Clock);
            MinimumPlayableWorld.ConfigureScenarioServices(restored);
            return new AuditHost(source.Catalog, restored, source.Layout, source.Store, source.Clock);
        }

        private static Decision FindActiveDecision(
            WorldState world,
            CharacterId characterId,
            AuthoredId definitionId)
        {
            foreach (Decision decision in world.Decisions.All)
                if (decision.IsActive && decision.CharacterId == characterId && decision.DefinitionId == definitionId)
                    return decision;
            throw new InvalidOperationException(
                characterId + " did not produce active Decision '" + definitionId + "' during the audit.");
        }

        private static DecisionInfluence FindInfluence(Decision decision, AuthoredId labelId)
        {
            for (int i = 0; i < decision.Influences.Count; i++)
                if (decision.Influences[i].LabelId == labelId) return decision.Influences[i];
            throw new InvalidOperationException(
                "Decision " + decision.Id + " has no Influence labeled '" + labelId + "'.");
        }

        private static void Require(Result result, string action)
        {
            if (result.IsFailure)
                throw new InvalidOperationException("Could not " + action + ": " + result.Reason);
        }

        private sealed class AuditHost
        {
            public AuditHost(
                DefinitionCatalog catalog,
                SimulationHost host,
                MinimumPlayableWorldLayout layout,
                InMemorySaveGameStore store,
                FixedRealWorldClock clock)
            {
                Catalog = catalog;
                Host = host;
                Layout = layout;
                Store = store;
                Clock = clock;
            }

            public DefinitionCatalog Catalog { get; }
            public SimulationHost Host { get; }
            public MinimumPlayableWorldLayout Layout { get; }
            public InMemorySaveGameStore Store { get; }
            public FixedRealWorldClock Clock { get; }
        }
    }

    public sealed class MvpExperienceAuditResult
    {
        public MvpExperienceAuditResult(
            IReadOnlyList<MvpExperienceBranchReport> branches,
            bool continuationsEquivalent)
        {
            Branches = branches;
            ContinuationsEquivalent = continuationsEquivalent;
        }

        public IReadOnlyList<MvpExperienceBranchReport> Branches { get; }
        public bool ContinuationsEquivalent { get; }
        public bool Passed
        {
            get
            {
                if (!ContinuationsEquivalent) return false;
                for (int i = 0; i < Branches.Count; i++)
                    if (Branches[i].Issues.Count > 0) return false;
                return true;
            }
        }
    }

    public sealed class MvpExperienceBranchReport
    {
        public MvpExperienceBranchReport(
            string name,
            string finalTime,
            string continuationFingerprint,
            int activityCount,
            int decisionCount,
            int historyCount,
            int knowledgeCount,
            int nudgeBalance,
            bool commonsOpen,
            IReadOnlyList<NotificationEntryView> recap,
            IReadOnlyList<MvpCharacterAuditRow> characters,
            IReadOnlyList<MvpDecisionAuditRow> decisions,
            IReadOnlyList<string> issues)
        {
            Name = name;
            FinalTime = finalTime;
            ContinuationFingerprint = continuationFingerprint;
            ActivityCount = activityCount;
            DecisionCount = decisionCount;
            HistoryCount = historyCount;
            KnowledgeCount = knowledgeCount;
            NudgeBalance = nudgeBalance;
            CommonsOpen = commonsOpen;
            Recap = recap;
            Characters = characters;
            Decisions = decisions;
            Issues = issues;
        }

        public string Name { get; }
        public string FinalTime { get; }
        /// <summary>
        /// Stable fingerprint of the projection-visible continuation state. Recent-history counts and
        /// recap layout are intentionally excluded because recent entries may be pruned on save and
        /// offline recap is deliberately grouped differently.
        /// </summary>
        public string ContinuationFingerprint { get; }
        public int ActivityCount { get; }
        public int DecisionCount { get; }
        public int HistoryCount { get; }
        public int KnowledgeCount { get; }
        public int NudgeBalance { get; }
        public bool CommonsOpen { get; }
        public IReadOnlyList<NotificationEntryView> Recap { get; }
        public IReadOnlyList<MvpCharacterAuditRow> Characters { get; }
        public IReadOnlyList<MvpDecisionAuditRow> Decisions { get; }
        public IReadOnlyList<string> Issues { get; }
    }

    public sealed class MvpCharacterAuditRow
    {
        public MvpCharacterAuditRow(
            int characterId,
            string name,
            string activity,
            string location,
            int scheduleCount,
            int decisionCount,
            int historyCount,
            int knownNeedCount,
            int knownRelationshipCount)
        {
            CharacterId = characterId;
            Name = name;
            Activity = activity;
            Location = location;
            ScheduleCount = scheduleCount;
            DecisionCount = decisionCount;
            HistoryCount = historyCount;
            KnownNeedCount = knownNeedCount;
            KnownRelationshipCount = knownRelationshipCount;
        }

        public int CharacterId { get; }
        public string Name { get; }
        public string Activity { get; }
        public string Location { get; }
        public int ScheduleCount { get; }
        public int DecisionCount { get; }
        public int HistoryCount { get; }
        public int KnownNeedCount { get; }
        public int KnownRelationshipCount { get; }
    }

    public sealed class MvpDecisionAuditRow
    {
        public MvpDecisionAuditRow(
            int decisionId,
            string characterName,
            string definitionId,
            string status,
            int optionCount,
            int visibleReasonCount,
            int frozenReasonCount,
            int interventionCount)
        {
            DecisionId = decisionId;
            CharacterName = characterName;
            DefinitionId = definitionId;
            Status = status;
            OptionCount = optionCount;
            VisibleReasonCount = visibleReasonCount;
            FrozenReasonCount = frozenReasonCount;
            InterventionCount = interventionCount;
        }

        public int DecisionId { get; }
        public string CharacterName { get; }
        public string DefinitionId { get; }
        public string Status { get; }
        public int OptionCount { get; }
        public int VisibleReasonCount { get; }
        public int FrozenReasonCount { get; }
        public int InterventionCount { get; }
    }
}
