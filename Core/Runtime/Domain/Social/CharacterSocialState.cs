using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Social
{
    /// <summary>Stable authored tags with deterministic integral intensity, kept outside personality.</summary>
    public sealed class WeightedTagSet
    {
        private readonly SortedDictionary<AuthoredId, long> _intensities =
            new SortedDictionary<AuthoredId, long>();

        public int Revision { get; private set; }
        public IReadOnlyDictionary<AuthoredId, long> All => _intensities;

        public long Intensity(AuthoredId tag) => _intensities.TryGetValue(tag, out long value) ? value : 0;

        public void Set(AuthoredId tag, long intensity)
        {
            if (!tag.IsSet)
            {
                throw new ArgumentException("A value/interest tag needs a stable authored id.", nameof(tag));
            }

            _intensities[tag] = IntegerMath.Clamp(intensity, -10000, 10000);
            Revision++;
        }

        public void Restore(AuthoredId tag, long intensity) =>
            _intensities[tag] = IntegerMath.Clamp(intensity, -10000, 10000);

        public void RestoreRevision(int revision) => Revision = revision;
    }

    public static class AffectKinds
    {
        public static readonly AuthoredId Stress = new AuthoredId("affect.stress");
        public static readonly AuthoredId Arousal = new AuthoredId("affect.arousal");
        public static readonly AuthoredId Irritation = new AuthoredId("affect.irritation");
        public static readonly AuthoredId Fear = new AuthoredId("affect.fear");
        public static readonly AuthoredId Confidence = new AuthoredId("affect.confidence");
        public static readonly AuthoredId Loneliness = new AuthoredId("affect.loneliness");
    }

    /// <summary>Fast-changing analytical affect, explicitly separate from durable relationship channels.</summary>
    public sealed class AffectState
    {
        private readonly SortedDictionary<AuthoredId, AnalyticalProgression> _values =
            new SortedDictionary<AuthoredId, AnalyticalProgression>();
        private readonly SortedDictionary<AuthoredId, int> _revisions =
            new SortedDictionary<AuthoredId, int>();

        public IReadOnlyDictionary<AuthoredId, AnalyticalProgression> All => _values;

        public long ValueAt(AuthoredId kind, SimTime at) =>
            _values.TryGetValue(kind, out AnalyticalProgression value) ? value.ValueAt(at) : 0;

        public int Revision(AuthoredId kind) => _revisions.TryGetValue(kind, out int value) ? value : 0;

        public void Set(AuthoredId kind, AnalyticalProgression value)
        {
            if (!kind.IsSet)
            {
                throw new ArgumentException("An affect value needs a stable authored id.", nameof(kind));
            }
            _values[kind] = value;
            _revisions[kind] = Revision(kind) + 1;
        }

        public void ApplyDelta(AuthoredId kind, SimTime at, long delta)
        {
            AnalyticalProgression current = _values.TryGetValue(kind, out AnalyticalProgression existing)
                ? existing
                : AnalyticalProgression.Constant(0, at);
            long bounded = IntegerMath.Clamp(current.ValueAt(at) + delta, -10000, 10000);
            Set(kind, AnalyticalProgression.Constant(bounded, at));
        }

        public void Restore(AuthoredId kind, AnalyticalProgression value, int revision)
        {
            _values[kind] = value;
            _revisions[kind] = revision;
        }
    }
}
