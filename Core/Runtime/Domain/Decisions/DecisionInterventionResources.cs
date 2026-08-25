using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Time;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Events;

namespace Vivarium.Domain.PlayerAgency
{
    using Decisions;

    /// <summary>Authored tuning for a non-Nudge intervention resource.</summary>
    public readonly struct InterventionResourcePolicy
    {
        public InterventionResourcePolicy(int initialBalance, int cap, int refreshAmount = 0, SimDuration refreshPeriod = default)
        {
            if (initialBalance < 0 || cap < 1 || initialBalance > cap) throw new ArgumentOutOfRangeException(nameof(initialBalance));
            if (refreshAmount < 0 || (refreshAmount > 0 && refreshPeriod.TotalMinutes <= 0)) throw new ArgumentOutOfRangeException(nameof(refreshAmount));
            InitialBalance = initialBalance;
            Cap = cap;
            RefreshAmount = refreshAmount;
            RefreshPeriod = refreshPeriod;
        }

        public int InitialBalance { get; }
        public int Cap { get; }
        public int RefreshAmount { get; }
        public SimDuration RefreshPeriod { get; }
        public bool Refreshes => RefreshAmount > 0;
    }

    public sealed class DecisionInterventionResources
    {
        private readonly SortedDictionary<InterventionResourceKind, ResourceState> _states =
            new SortedDictionary<InterventionResourceKind, ResourceState>();

        public IEnumerable<KeyValuePair<InterventionResourceKind, ResourceState>> All => _states;

        public bool IsConfigured(InterventionResourceKind kind) => _states.ContainsKey(kind);

        public void Configure(InterventionResourceKind kind, InterventionResourcePolicy policy, SimTime now)
        {
            if (kind != InterventionResourceKind.ReRoll && kind != InterventionResourceKind.ReplacementDie)
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (_states.ContainsKey(kind)) return;
            _states.Add(kind, new ResourceState(policy.InitialBalance, policy.Cap, 0,
                policy.RefreshAmount, policy.RefreshPeriod,
                policy.Refreshes ? now + policy.RefreshPeriod : default));
        }

        public bool CanSpend(InterventionResourceKind kind, int amount) =>
            amount >= 0 && _states.TryGetValue(kind, out ResourceState state) && state.Balance >= amount;

        public bool TrySpend(InterventionResourceKind kind, int amount)
        {
            if (!CanSpend(kind, amount)) return false;
            ResourceState state = _states[kind];
            _states[kind] = state.WithBalance(state.Balance - amount, state.Revision + (amount > 0 ? 1 : 0));
            return true;
        }

        public void Refund(InterventionResourceKind kind, int amount)
        {
            if (amount <= 0 || !_states.TryGetValue(kind, out ResourceState state)) return;
            int balance = Math.Min(state.Cap, checked(state.Balance + amount));
            _states[kind] = state.WithBalance(balance, state.Revision + (balance != state.Balance ? 1 : 0));
        }

        public void Refresh(InterventionResourceKind kind, SimTime at)
        {
            if (!_states.TryGetValue(kind, out ResourceState state) || !state.Refreshes || at < state.NextRefreshAt) return;
            int balance = Math.Min(state.Cap, checked(state.Balance + state.RefreshAmount));
            SimTime next = state.NextRefreshAt;
            do next += state.RefreshPeriod; while (next <= at);
            _states[kind] = new ResourceState(balance, state.Cap, state.Revision + (balance != state.Balance ? 1 : 0),
                state.RefreshAmount, state.RefreshPeriod, next);
        }

        public bool TryGet(InterventionResourceKind kind, out ResourceState state) => _states.TryGetValue(kind, out state);

        public void Restore(InterventionResourceKind kind, ResourceState state) => _states[kind] = state;
    }

    public readonly struct ResourceState
    {
        public ResourceState(int balance, int cap, int revision, int refreshAmount, SimDuration refreshPeriod, SimTime nextRefreshAt)
        {
            if (cap < 1 || balance < 0 || balance > cap) throw new ArgumentOutOfRangeException(nameof(balance));
            if (revision < 0 || refreshAmount < 0 || (refreshAmount > 0 && refreshPeriod.TotalMinutes <= 0))
                throw new ArgumentOutOfRangeException(nameof(revision));
            Balance = balance; Cap = cap; Revision = revision; RefreshAmount = refreshAmount;
            RefreshPeriod = refreshPeriod; NextRefreshAt = nextRefreshAt;
        }
        public int Balance { get; }
        public int Cap { get; }
        public int Revision { get; }
        public int RefreshAmount { get; }
        public SimDuration RefreshPeriod { get; }
        public SimTime NextRefreshAt { get; }
        public bool Refreshes => RefreshAmount > 0 && RefreshPeriod.TotalMinutes > 0;
        public ResourceState WithBalance(int balance, int revision) =>
            new ResourceState(balance, Cap, revision, RefreshAmount, RefreshPeriod, NextRefreshAt);
    }

    public static class DecisionInterventionResourceEvents
    {
        public static readonly AuthoredId Refresh = new AuthoredId("event.player.intervention_resource_refresh");

        public static void EnsureScheduled(WorldState world, InterventionResourceKind kind)
        {
            if (!world.InterventionResources.TryGet(kind, out ResourceState state) || !state.Refreshes) return;
            foreach (ScheduledEvent pending in world.Scheduler.PendingEvents)
                if (pending.EventType == Refresh && pending.Payload is InterventionResourceRefreshPayload payload && payload.Kind == kind)
                    return;
            world.Scheduler.Schedule(state.NextRefreshAt, SchedulePhase.Preparation, Refresh,
                new InterventionResourceRefreshPayload(kind));
        }
    }

    public sealed class InterventionResourceRefreshPayload : IScheduledEventPayload
    {
        public InterventionResourceRefreshPayload(InterventionResourceKind kind) => Kind = kind;
        public InterventionResourceKind Kind { get; }
    }

    public sealed class InterventionResourceRefreshHandler : ScheduledEventHandler<InterventionResourceRefreshPayload>
    {
        public InterventionResourceRefreshHandler() : base(DecisionInterventionResourceEvents.Refresh) { }
        protected override bool CanExecute(WorldState world, InterventionResourceRefreshPayload payload) =>
            world.InterventionResources.TryGet(payload.Kind, out ResourceState state) && state.Refreshes;
        protected override void Execute(WorldState world, InterventionResourceRefreshPayload payload, SimulationContext context)
        {
            world.InterventionResources.Refresh(payload.Kind, world.Clock.Now);
            DecisionInterventionResourceEvents.EnsureScheduled(world, payload.Kind);
        }
    }


    public sealed class InterventionResourceDissolutionRefundHandler : DomainEventHandler<DecisionDissolvedEvent>
    {
        public InterventionResourceDissolutionRefundHandler() : base(DecisionDomainEventTypes.DecisionDissolved) { }
        protected override void Handle(DecisionDissolvedEvent domainEvent, WorldState world, SimulationContext context)
        {
            for (int i = 0; i < domainEvent.InterventionsToRefund.Count; i++)
            {
                AppliedIntervention intervention = domainEvent.InterventionsToRefund[i];
                if (intervention.ResourceKind == InterventionResourceKind.ReRoll ||
                    intervention.ResourceKind == InterventionResourceKind.ReplacementDie)
                    world.InterventionResources.Refund(intervention.ResourceKind, intervention.ResourceCost);
            }
        }
    }
}
