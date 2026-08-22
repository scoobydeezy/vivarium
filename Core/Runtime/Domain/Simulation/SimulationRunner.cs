using System;
using Vivarium.Domain.Scheduling;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Simulation
{
    /// <summary>
    /// Notified at quiescent boundaries, where the world is safe to read (§13.1).
    /// <para>
    /// The Application layer implements this to publish projections. During long offline catch-up it
    /// may publish progress periodically — but only at safe boundaries, never mid-step.
    /// </para>
    /// </summary>
    public interface IQuiescenceObserver
    {
        void OnQuiescence(WorldState world, SimulationContext context);
    }

    /// <summary>
    /// Advances authoritative time (§9, §11.1, §13).
    /// <para>
    /// The single owner of world mutation. Simulation execution is single-threaded — which does not
    /// mean it must run on Unity's main thread, only that no two threads mutate
    /// <see cref="WorldState"/> concurrently (§13).
    /// </para>
    /// <para>
    /// Nothing here consults render time. <c>Time.deltaTime</c> must never reach a game rule (§9).
    /// </para>
    /// </summary>
    public sealed class SimulationRunner
    {
        private readonly SettlementLoop _settlement;
        private readonly IQuiescenceObserver _observer;

        public SimulationRunner(SettlementLoop settlement, IQuiescenceObserver observer = null)
        {
            _settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            _observer = observer;
        }

        /// <summary>Instants settled since construction (benchmarking, §49).</summary>
        public long InstantsSettled { get; private set; }

        /// <summary>Scheduled events and Domain Events processed since construction (§49).</summary>
        public long WorkProcessed { get; private set; }

        /// <summary>
        /// Settles the current instant and publishes quiescence. Called after a command mutates state,
        /// so the command's consequences land before the next command runs (§2.2.1).
        /// </summary>
        public void SettleAndPublish(SimulationContext context)
        {
            WorkProcessed += _settlement.SettleCurrentInstant(context.World, context);
            InstantsSettled++;
            _observer?.OnQuiescence(context.World, context);
        }

        /// <summary>
        /// Runs the world forward to <paramref name="target"/>, settling each instant that has work.
        /// <para>
        /// Time only moves to instants that matter: with analytical progressions doing the continuous
        /// work (§10), an empty day costs one hop rather than 1,440 ticks.
        /// </para>
        /// </summary>
        /// <param name="publishEveryInstants">
        /// During offline catch-up, publish progress every N settled instants. 0 publishes only at the
        /// end. Publication still happens exclusively at quiescent boundaries (§13.1).
        /// </param>
        public void AdvanceUntil(SimTime target, SimulationContext context, int publishEveryInstants = 0)
        {
            WorldState world = context.World;

            if (target < world.Clock.Now)
            {
                throw new InvalidOperationException($"Cannot advance to {target}; the world is already at {world.Clock.Now}.");
            }

            // Anything left at the current instant settles before time moves.
            WorkProcessed += _settlement.SettleCurrentInstant(world, context);
            InstantsSettled++;

            int sinceLastPublish = 0;

            while (true)
            {
                ScheduledEvent next = world.Scheduler.PeekNext();
                if (next == null || next.DueAt > target)
                {
                    break;
                }

                world.Clock.AdvanceTo(next.DueAt);
                WorkProcessed += _settlement.SettleCurrentInstant(world, context);
                InstantsSettled++;

                if (publishEveryInstants > 0 && ++sinceLastPublish >= publishEveryInstants)
                {
                    sinceLastPublish = 0;
                    _observer?.OnQuiescence(world, context);
                }
            }

            world.Clock.AdvanceTo(target);
            WorkProcessed += _settlement.SettleCurrentInstant(world, context);
            InstantsSettled++;

            _observer?.OnQuiescence(world, context);
        }

        public void AdvanceBy(SimDuration duration, SimulationContext context, int publishEveryInstants = 0)
        {
            if (duration.IsNegative)
            {
                throw new ArgumentOutOfRangeException(nameof(duration), "Simulation time cannot move backwards.");
            }

            AdvanceUntil(context.World.Clock.Now.Plus(duration), context, publishEveryInstants);
        }
    }
}
