using System;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Events;
using Vivarium.Domain.History;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.PlayerAgency
{
    /// <summary>The locked MVP Nudge policy. Balance is authoritative; boundaries derive from SimTime.</summary>
    public static class NudgePolicy
    {
        public const int InitialBalance = 3;
        public const int Cap = 3;
        public const long RegenerationPeriodMinutes = 8 * 60;
    }

    /// <summary>Authoritative spendable Nudge state.</summary>
    public sealed class NudgeAccount
    {
        public NudgeAccount(int balance = NudgePolicy.InitialBalance, int revision = 0)
        {
            if (balance < 0 || balance > NudgePolicy.Cap)
            {
                throw new ArgumentOutOfRangeException(nameof(balance));
            }

            if (revision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision));
            }

            Balance = balance;
            Revision = revision;
        }

        public int Balance { get; private set; }

        public int Cap => NudgePolicy.Cap;

        public int Revision { get; private set; }

        public bool CanSpend(int amount) => amount >= 0 && Balance >= amount;

        public bool TrySpend(int amount)
        {
            if (!CanSpend(amount))
            {
                return false;
            }

            if (amount > 0)
            {
                Balance -= amount;
                Revision++;
            }

            return true;
        }

        /// <summary>Applies one capped refund event and returns the amount actually restored.</summary>
        public int Refund(int amount) => Increase(amount);

        /// <summary>Applies one capped regeneration event and returns the amount actually restored.</summary>
        public int Regenerate(int amount = 1) => Increase(amount);

        private int Increase(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            int before = Balance;
            long candidate = (long)Balance + amount;
            Balance = candidate >= Cap ? Cap : (int)candidate;
            int applied = Balance - before;
            if (applied > 0)
            {
                Revision++;
            }
            return applied;
        }
    }

    public static class PlayerAgencyScheduledEventTypes
    {
        public static readonly AuthoredId NudgeRegeneration = new AuthoredId("event.player.nudge_regeneration");
    }

    /// <summary>Marker payload: the due time already identifies the deterministic boundary.</summary>
    public sealed class NudgeRegenerationPayload : IScheduledEventPayload
    {
    }

    public static class NudgeRegenerationSchedule
    {
        public static SimTime NextBoundaryAfter(SimTime instant)
        {
            long period = NudgePolicy.RegenerationPeriodMinutes;
            long next = ((instant.TotalMinutes / period) + 1) * period;
            return new SimTime(next);
        }

        public static void EnsureScheduled(WorldState world)
        {
            foreach (ScheduledEvent pending in world.Scheduler.PendingEvents)
            {
                if (pending.EventType == PlayerAgencyScheduledEventTypes.NudgeRegeneration)
                {
                    return;
                }
            }

            ScheduleNext(world);
        }

        public static void ScheduleNext(WorldState world)
        {
            world.Scheduler.Schedule(
                NextBoundaryAfter(world.Clock.Now),
                SchedulePhase.Preparation,
                PlayerAgencyScheduledEventTypes.NudgeRegeneration,
                new NudgeRegenerationPayload());
        }
    }

    public enum NudgeBalanceChangeKind
    {
        Spent = 0,
        Refunded = 1,
        Regenerated = 2,
    }

    public static class PlayerAgencyDomainEventTypes
    {
        public static readonly AuthoredId NudgeBalanceChanged = new AuthoredId("domain.player.nudge_balance_changed");
    }

    public sealed class NudgeBalanceChangedEvent : IDomainEvent
    {
        public NudgeBalanceChangedEvent(
            NudgeBalanceChangeKind kind,
            int requestedAmount,
            int appliedAmount,
            int balance,
            DecisionId decisionId = default,
            CharacterId characterId = default,
            AuthoredId interventionDefinitionId = default)
        {
            Kind = kind;
            RequestedAmount = requestedAmount;
            AppliedAmount = appliedAmount;
            Balance = balance;
            DecisionId = decisionId;
            CharacterId = characterId;
            InterventionDefinitionId = interventionDefinitionId;
        }

        public AuthoredId EventType => PlayerAgencyDomainEventTypes.NudgeBalanceChanged;
        public NudgeBalanceChangeKind Kind { get; }
        public int RequestedAmount { get; }
        public int AppliedAmount { get; }
        public int Balance { get; }
        public DecisionId DecisionId { get; }
        public CharacterId CharacterId { get; }
        public AuthoredId InterventionDefinitionId { get; }
    }

    public sealed class NudgeRegenerationHandler : ScheduledEventHandler<NudgeRegenerationPayload>
    {
        public NudgeRegenerationHandler() : base(PlayerAgencyScheduledEventTypes.NudgeRegeneration)
        {
        }

        protected override bool CanExecute(WorldState world, NudgeRegenerationPayload payload) => true;

        protected override void Execute(WorldState world, NudgeRegenerationPayload payload, SimulationContext context)
        {
            int applied = world.Nudges.Regenerate();
            if (applied > 0)
            {
                world.Publish(new NudgeBalanceChangedEvent(
                    NudgeBalanceChangeKind.Regenerated,
                    1,
                    applied,
                    world.Nudges.Balance));
            }

            NudgeRegenerationSchedule.ScheduleNext(world);
        }
    }

    /// <summary>Returns snapshotted Nudge spend one intervention at a time when a Decision dissolves.</summary>
    public sealed class NudgeDissolutionRefundHandler : DomainEventHandler<DecisionDissolvedEvent>
    {
        public NudgeDissolutionRefundHandler() : base(DecisionDomainEventTypes.DecisionDissolved)
        {
        }

        protected override void Handle(DecisionDissolvedEvent domainEvent, WorldState world, SimulationContext context)
        {
            for (int i = 0; i < domainEvent.InterventionsToRefund.Count; i++)
            {
                AppliedIntervention intervention = domainEvent.InterventionsToRefund[i];
                if (intervention.ResourceKind != InterventionResourceKind.Nudge || intervention.ResourceCost <= 0)
                {
                    continue;
                }

                int applied = world.Nudges.Refund(intervention.ResourceCost);
                world.Publish(new NudgeBalanceChangedEvent(
                    NudgeBalanceChangeKind.Refunded,
                    intervention.ResourceCost,
                    applied,
                    world.Nudges.Balance,
                    domainEvent.DecisionId,
                    domainEvent.CharacterId,
                    intervention.InterventionDefinitionId));
            }
        }
    }

    public sealed class NudgeBalanceHistoryHandler : DomainEventHandler<NudgeBalanceChangedEvent>
    {
        public static readonly AuthoredId HistoryKind = new AuthoredId("history.player.nudge_balance_changed");

        public NudgeBalanceHistoryHandler() : base(PlayerAgencyDomainEventTypes.NudgeBalanceChanged)
        {
        }

        protected override void Handle(NudgeBalanceChangedEvent domainEvent, WorldState world, SimulationContext context)
        {
            EntityRef[] subjects = domainEvent.DecisionId.IsSet
                ? new[] { domainEvent.CharacterId.ToRef(), domainEvent.DecisionId.ToRef() }
                : new EntityRef[0];
            world.HistoryLedger.Record(
                HistoryKind,
                world.Clock.Now,
                RetentionTier.Recent,
                $"{domainEvent.Kind}: {domainEvent.AppliedAmount}/{domainEvent.RequestedAmount}; balance {domainEvent.Balance}/{world.Nudges.Cap}",
                subjects);
        }
    }
}
