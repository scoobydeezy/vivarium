using System;
using System.Collections.Generic;

namespace Vivarium.Domain.Decisions
{
    /// <summary>
    /// Bounds on how many decisions the player may keep held (§20).
    /// <para>
    /// <b>Held decisions must never grow without bound.</b> Because a character may hold several
    /// concurrent decisions (§17.1), the per-character cap governs the total across all of them, not
    /// one cap per decision (invariant 33).
    /// </para>
    /// <para>
    /// Overflow behaviour is deterministic: lowest importance → oldest creation → lowest DecisionId.
    /// The evicted decision auto-resolves and is reported in the recap (§20).
    /// </para>
    /// </summary>
    public sealed class DecisionHoldPolicy
    {
        public DecisionHoldPolicy(int maxGlobalHeld, int maxHeldPerCharacter)
        {
            if (maxGlobalHeld < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxGlobalHeld));
            }

            if (maxHeldPerCharacter < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHeldPerCharacter));
            }

            MaxGlobalHeld = maxGlobalHeld;
            MaxHeldPerCharacter = maxHeldPerCharacter;
        }

        public int MaxGlobalHeld { get; }

        /// <summary>Total held decisions allowed for one character, across all of their concurrent decisions.</summary>
        public int MaxHeldPerCharacter { get; }

        public bool GlobalCapacityExceeded(int currentGlobalHeld) => currentGlobalHeld > MaxGlobalHeld;

        public bool CharacterCapacityExceeded(int currentHeldForCharacter) => currentHeldForCharacter > MaxHeldPerCharacter;

        /// <summary>
        /// Chooses which held decision gives way when capacity is exceeded. Total ordering, so the
        /// choice is reproducible.
        /// </summary>
        public Decision SelectOverflowVictim(IEnumerable<Decision> heldDecisions)
        {
            Decision victim = null;
            foreach (Decision candidate in heldDecisions)
            {
                if (candidate == null || !candidate.IsActive)
                {
                    continue;
                }

                if (victim == null || Precedes(candidate, victim))
                {
                    victim = candidate;
                }
            }

            return victim;
        }

        private static bool Precedes(Decision candidate, Decision incumbent)
        {
            if (candidate.Importance != incumbent.Importance)
            {
                return candidate.Importance < incumbent.Importance;
            }

            if (candidate.CreatedAt != incumbent.CreatedAt)
            {
                return candidate.CreatedAt < incumbent.CreatedAt;
            }

            return candidate.Id.Value < incumbent.Id.Value;
        }
    }
}
