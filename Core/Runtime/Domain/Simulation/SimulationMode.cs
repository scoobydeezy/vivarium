namespace Vivarium.Domain.Simulation
{
    /// <summary>
    /// The explicit execution context every advance carries (§21).
    /// <para>
    /// Core physical and game rules are identical across modes. What may vary is anything that depends
    /// on <i>player availability</i>: whether a notification fires now, whether a decision may stay
    /// held, whether an offline recap entry is generated, whether presentation animation is skipped.
    /// </para>
    /// <para>
    /// Offline progression is therefore <b>not</b> "run Live very fast" — it is a formally represented
    /// mode (invariant 31).
    /// </para>
    /// </summary>
    public enum SimulationMode
    {
        /// <summary>The player is present and watching.</summary>
        Live = 0,

        /// <summary>The player asked to skip ahead and is still present.</summary>
        PlayerFastForward = 1,

        /// <summary>
        /// Catching up elapsed real-world time. The duration comes from the persisted offline anchor
        /// and <c>IRealWorldClock</c>, computed outside the Domain (§21, §38, invariant 32).
        /// </summary>
        OfflineCatchUp = 2,
    }
}
