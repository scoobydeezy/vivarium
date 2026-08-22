using Vivarium.Domain.Common;

namespace Vivarium.Domain.Activities
{
    /// <summary>
    /// Where an outcome came from (§29.6). Decisions and Activities share this small provenance
    /// convention while remaining separate entities.
    /// </summary>
    public enum OutcomeSource
    {
        /// <summary>Resolved by the simulation itself — always available, never optional.</summary>
        Automatic = 0,

        /// <summary>Supplied by the player through a validated command, e.g. a mini-game result.</summary>
        PlayerProvided = 1,
    }

    /// <summary>Normalized outcome tiers shared by both resolution paths.</summary>
    public enum PerformanceGrade
    {
        Failure = 0,
        Poor = 1,
        Adequate = 2,
        Good = 3,
        Excellent = 4,
    }

    /// <summary>
    /// Content-agnostic normalized outcome of an Activity (§29.6).
    /// <para>
    /// Deliberately <b>not</b> raw UI telemetry: no timings, no button presses, no score curve. A
    /// mini-game submits a grade and magnitude through <c>SubmitActivityPerformanceCommand</c>, and the
    /// Domain treats it as external input analogous to a decision intervention — which is why
    /// player-played results do not threaten determinism.
    /// </para>
    /// </summary>
    public readonly struct ActivityPerformanceResult
    {
        public ActivityPerformanceResult(
            PerformanceGrade grade,
            long magnitude,
            OutcomeSource source,
            AuthoredId outcomeId = default)
        {
            Grade = grade;
            Magnitude = magnitude;
            Source = source;
            OutcomeId = outcomeId;
        }

        public PerformanceGrade Grade { get; }

        /// <summary>Integral outcome magnitude — units produced, quality points, basis points (§16).</summary>
        public long Magnitude { get; }

        /// <summary>
        /// Automatic or player-provided. Diagnostics log this explicitly so a trace never confuses a
        /// human-played outcome with an RNG result (§53).
        /// </summary>
        public OutcomeSource Source { get; }

        /// <summary>Optional authored outcome id for content-specific consequences.</summary>
        public AuthoredId OutcomeId { get; }

        public static ActivityPerformanceResult Automatic(PerformanceGrade grade, long magnitude, AuthoredId outcomeId = default) =>
            new ActivityPerformanceResult(grade, magnitude, OutcomeSource.Automatic, outcomeId);

        public static ActivityPerformanceResult FromPlayer(PerformanceGrade grade, long magnitude, AuthoredId outcomeId = default) =>
            new ActivityPerformanceResult(grade, magnitude, OutcomeSource.PlayerProvided, outcomeId);

        public override string ToString() => $"{Grade}({Magnitude}) via {Source}";
    }
}
