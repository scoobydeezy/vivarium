namespace Vivarium.Domain.Scheduling
{
    /// <summary>
    /// Deterministic within-instant execution order (§11): events at the same <c>DueAt</c> run in
    /// ascending phase, then ascending <c>EventSequence</c>.
    /// <para>
    /// An executing event may not schedule same-instant work into an <i>earlier</i> phase — that would
    /// insert work before its own cause (§11.4). Values are persisted: append only, never renumber.
    /// </para>
    /// </summary>
    public enum SchedulePhase
    {
        /// <summary>Bookkeeping that must precede anything else at this instant.</summary>
        Preparation = 0,

        /// <summary>Expiries, timeouts, and cancellations coming due.</summary>
        Expiration = 10,

        /// <summary>Analytical threshold crossings: needs, production, recovery (§10.2).</summary>
        Progression = 20,

        /// <summary>Activity starts, completions, arrivals, and transitions (§29.5).</summary>
        Activity = 30,

        /// <summary>Decision generation and resolution (§18).</summary>
        Decision = 40,

        /// <summary>Social interaction opportunities arising from shared context (§32).</summary>
        Social = 50,

        /// <summary>Consequences applied after the acting phases have settled.</summary>
        Consequence = 60,

        /// <summary>Index maintenance, retention, and history compaction (§37).</summary>
        Bookkeeping = 70,
    }
}
