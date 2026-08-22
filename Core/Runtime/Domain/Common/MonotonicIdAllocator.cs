using System;

namespace Vivarium.Domain.Common
{
    /// <summary>
    /// The only sanctioned way to mint authoritative runtime identity. Never <c>Guid.NewGuid()</c> (§7).
    /// </summary>
    public sealed class MonotonicIdAllocator<TId> : IIdAllocator<TId>
    {
        private readonly Func<int, TId> _factory;
        private int _issued;

        /// <param name="factory">Wraps a raw counter value into the typed id.</param>
        /// <param name="alreadyIssued">Restored counter from a save. Ids at or below this are spent forever.</param>
        public MonotonicIdAllocator(Func<int, TId> factory, int alreadyIssued = 0)
        {
            if (alreadyIssued < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(alreadyIssued));
            }

            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _issued = alreadyIssued;
        }

        public int IssuedCount => _issued;

        public TId Next()
        {
            if (_issued == int.MaxValue)
            {
                throw new InvalidOperationException("Runtime id family exhausted; ids are never reused (§7.1).");
            }

            _issued++;
            return _factory(_issued);
        }
    }
}
