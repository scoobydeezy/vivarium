using System;

namespace Vivarium.Domain.Common
{
    /// <summary>
    /// Outcome of a validated operation. Failure carries a stable authored reason code so that
    /// UI availability and command execution can share one authoritative rule evaluation (§19, §57).
    /// </summary>
    public readonly struct Result
    {
        private Result(bool isSuccess, AuthoredId reason, string detail)
        {
            IsSuccess = isSuccess;
            Reason = reason;
            Detail = detail;
        }

        public bool IsSuccess { get; }

        public bool IsFailure => !IsSuccess;

        /// <summary>Stable authored failure code, e.g. <c>decision.intervention.already_applied</c>.</summary>
        public AuthoredId Reason { get; }

        /// <summary>Optional diagnostic detail. Never used for authoritative branching.</summary>
        public string Detail { get; }

        public static Result Ok() => new Result(true, AuthoredId.None, null);

        public static Result Fail(AuthoredId reason, string detail = null) => new Result(false, reason, detail);

        public override string ToString() => IsSuccess ? "Ok" : $"Fail({Reason}{(Detail == null ? string.Empty : ": " + Detail)})";
    }

    /// <summary>A <see cref="Result"/> carrying a value on success.</summary>
    public readonly struct Result<T>
    {
        private readonly T _value;

        private Result(bool isSuccess, T value, AuthoredId reason, string detail)
        {
            IsSuccess = isSuccess;
            _value = value;
            Reason = reason;
            Detail = detail;
        }

        public bool IsSuccess { get; }

        public bool IsFailure => !IsSuccess;

        public AuthoredId Reason { get; }

        public string Detail { get; }

        public T Value => IsSuccess
            ? _value
            : throw new InvalidOperationException($"Result has no value; it failed with '{Reason}'.");

        public bool TryGetValue(out T value)
        {
            value = _value;
            return IsSuccess;
        }

        public static Result<T> Ok(T value) => new Result<T>(true, value, AuthoredId.None, null);

        public static Result<T> Fail(AuthoredId reason, string detail = null) => new Result<T>(false, default, reason, detail);

        public Result WithoutValue() => IsSuccess ? Result.Ok() : Result.Fail(Reason, Detail);
    }
}
