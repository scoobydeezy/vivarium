namespace Vivarium.Domain.Common
{
    /// <summary>
    /// Deterministic monotonic allocator for one runtime-id family (§7).
    /// <para>
    /// Counters are authoritative save state. Identical initial state plus identical ordered inputs
    /// plus identical execution order must produce identical ids, so allocation must never depend on
    /// wall-clock time, GUIDs, or hash-container iteration order.
    /// </para>
    /// </summary>
    public interface IIdAllocator<TId>
    {
        /// <summary>Allocates the next id and advances the counter.</summary>
        TId Next();

        /// <summary>The raw counter value, for persistence. Ids already issued are never reused (§7.1).</summary>
        int IssuedCount { get; }
    }
}
