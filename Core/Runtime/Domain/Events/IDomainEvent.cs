using Vivarium.Domain.Common;

namespace Vivarium.Domain.Events
{
    /// <summary>
    /// Something that just happened: <c>MinaQuitJob</c> (§12).
    /// <para>
    /// A Domain Event is <b>not</b> a scheduled event (which is something that <i>may</i> happen) and
    /// <b>not</b> a presentation notification (which is something the player might care about). Those
    /// three never share one global bus.
    /// </para>
    /// <para>
    /// Transient by default: a Domain Event is not save state unless something promotes it into History
    /// or another persistent entity (§12, §37).
    /// </para>
    /// </summary>
    public interface IDomainEvent
    {
        /// <summary>Stable authored type used to resolve the ordered handler chain, e.g. <c>domain.employment.ended</c>.</summary>
        AuthoredId EventType { get; }
    }
}
