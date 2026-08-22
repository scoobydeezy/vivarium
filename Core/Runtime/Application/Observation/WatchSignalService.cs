using System;
using Vivarium.Domain.Attention;
using Vivarium.Domain.Common;
using Vivarium.Domain.Knowledge;
using Vivarium.Domain.Observation;
using Vivarium.Domain.Simulation;

namespace Vivarium.Application.Observation
{
    /// <summary>
    /// The single canonical source of watch signals (§20.1, §25).
    /// <para>
    /// Observation and Attention both read <see cref="WatchState"/> from here. Neither keeps its own
    /// idea of whether the player is watching Mina — that duplication is exactly what invariant 8
    /// forbids, because the two copies inevitably disagree.
    /// </para>
    /// <para>
    /// Semantic transitions only. Presentation calls this when something meaningful changes — became
    /// visible, selected, followed — never once per rendered frame (§25).
    /// </para>
    /// </summary>
    public sealed class WatchSignalService
    {
        private readonly KnowledgeDiscoveryService _discovery;

        public WatchSignalService(KnowledgeDiscoveryService discovery)
        {
            _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        }

        /// <summary>Sets the durable follow flag.</summary>
        public void SetFollowed(WorldState world, CharacterId character, bool followed)
        {
            WatchState state = world.Attention.WatchStateOf(character);
            world.Attention.SetWatchState(character, state.WithFollowed(followed));
        }

        /// <summary>
        /// Reports that a character became, or stopped being, visible for meaningful observation.
        /// Becoming visible is an observation opportunity, so discovery runs (§24).
        /// </summary>
        public void SetVisible(SimulationContext context, CharacterId character, bool visible)
        {
            WorldState world = context.World;
            WatchState previous = world.Attention.WatchStateOf(character);
            world.Attention.SetWatchState(character, previous.WithVisible(visible));

            if (visible && !previous.IsVisible)
            {
                Observe(context, character, ObservationKind.BeginObserving, DiscoveryChannels.DirectObservation);
            }
        }

        public void SetSelected(WorldState world, CharacterId character, bool selected)
        {
            WatchState state = world.Attention.WatchStateOf(character);
            world.Attention.SetWatchState(character, state.WithSelected(selected));
        }

        /// <summary>
        /// Reports the profile being opened or closed. Opening is a stronger discovery channel than
        /// passive visibility.
        /// </summary>
        public void SetProfileOpen(SimulationContext context, CharacterId character, bool open)
        {
            WorldState world = context.World;
            WatchState previous = world.Attention.WatchStateOf(character);
            world.Attention.SetWatchState(character, previous.WithProfileOpen(open));

            if (open && !previous.IsProfileOpen)
            {
                Observe(context, character, ObservationKind.InspectCharacter, DiscoveryChannels.Inspection);
            }
        }

        /// <summary>
        /// Runs one observation through the discovery pipeline.
        /// <para>
        /// The observation itself reveals nothing; it creates the opportunity, and Knowledge rules
        /// decide what it teaches (§25).
        /// </para>
        /// </summary>
        public void Observe(SimulationContext context, CharacterId character, ObservationKind kind, AuthoredId channelId, int difficultyBasisPoints = 0)
        {
            WorldState world = context.World;

            var observation = new Domain.Observation.Observation(kind, character.ToRef(), world.Clock.Now, channelId);
            int ordinal = world.Attention.NextObservationOrdinal(character);

            _discovery.Discover(
                world,
                observation.Subject,
                new DiscoveryChannel(channelId, difficultyBasisPoints),
                context,
                ordinal);

            if (context.Trace.IsEnabled)
            {
                context.Trace.Record("observation", $"{world.Clock.Now} {observation}");
            }
        }
    }
}
