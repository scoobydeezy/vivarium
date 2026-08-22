using System;
using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Scheduling;

namespace Vivarium.Application.Persistence
{
    /// <summary>
    /// Converts one event type's payload to and from flat save data.
    /// <para>
    /// Explicit codecs rather than reflective serialization: the persisted layout of a scheduled event
    /// is save schema (§39), and it should change only when someone deliberately changes it.
    /// </para>
    /// </summary>
    public interface IScheduledEventPayloadCodec
    {
        AuthoredId EventType { get; }

        ScheduledEventPayloadData Encode(IScheduledEventPayload payload);

        IScheduledEventPayload Decode(ScheduledEventPayloadData data);
    }

    /// <summary>Registry of payload codecs, keyed by authored event type.</summary>
    public sealed class ScheduledEventPayloadCodecRegistry
    {
        private readonly Dictionary<AuthoredId, IScheduledEventPayloadCodec> _codecs =
            new Dictionary<AuthoredId, IScheduledEventPayloadCodec>();

        public void Register(IScheduledEventPayloadCodec codec)
        {
            if (codec == null)
            {
                throw new ArgumentNullException(nameof(codec));
            }

            if (_codecs.ContainsKey(codec.EventType))
            {
                throw new InvalidOperationException($"A payload codec is already registered for '{codec.EventType}'.");
            }

            _codecs.Add(codec.EventType, codec);
        }

        public ScheduledEventPayloadData Encode(AuthoredId eventType, IScheduledEventPayload payload) =>
            Resolve(eventType).Encode(payload);

        public IScheduledEventPayload Decode(AuthoredId eventType, ScheduledEventPayloadData data) =>
            Resolve(eventType).Decode(data);

        private IScheduledEventPayloadCodec Resolve(AuthoredId eventType) =>
            _codecs.TryGetValue(eventType, out IScheduledEventPayloadCodec codec)
                ? codec
                : throw new KeyNotFoundException(
                    $"No payload codec registered for event type '{eventType}'. A scheduled event that cannot round-trip would silently vanish from the save (§40).");

        /// <summary>Registers codecs for the event types the Domain ships with.</summary>
        public static ScheduledEventPayloadCodecRegistry WithBuiltIns()
        {
            var registry = new ScheduledEventPayloadCodecRegistry();
            registry.Register(new ActivityStartPayloadCodec());
            registry.Register(new ActivityCompletionPayloadCodec());
            registry.Register(new TravelArrivalPayloadCodec());
            registry.Register(new NeedThresholdPayloadCodec());
            registry.Register(new CommitmentWindowExpiredPayloadCodec());
            registry.Register(new DecisionResolvePayloadCodec());
            return registry;
        }
    }

    internal static class PayloadData
    {
        internal static ScheduledEventPayloadData Of(string[] strings, long[] numbers)
        {
            var data = new ScheduledEventPayloadData();
            if (strings != null)
            {
                data.Strings.AddRange(strings);
            }

            if (numbers != null)
            {
                data.Numbers.AddRange(numbers);
            }

            return data;
        }

        internal static string String(ScheduledEventPayloadData data, int index) =>
            index < data.Strings.Count ? data.Strings[index] : null;

        internal static long Number(ScheduledEventPayloadData data, int index) =>
            index < data.Numbers.Count ? data.Numbers[index] : 0;
    }

    /// <summary>Codec for <see cref="ActivityStartPayload"/>.</summary>
    public sealed class ActivityStartPayloadCodec : IScheduledEventPayloadCodec
    {
        public AuthoredId EventType => ScheduledEventTypes.ActivityStart;

        public ScheduledEventPayloadData Encode(IScheduledEventPayload payload)
        {
            var typed = (ActivityStartPayload)payload;
            return PayloadData.Of(
                new[] { typed.ActivityDefinitionId.Value },
                new long[] { typed.CharacterId.Value, typed.CommitmentId.Value, typed.LocationId.Value });
        }

        public IScheduledEventPayload Decode(ScheduledEventPayloadData data) => new ActivityStartPayload(
            new CharacterId((int)PayloadData.Number(data, 0)),
            new CommitmentId((int)PayloadData.Number(data, 1)),
            new AuthoredId(PayloadData.String(data, 0)),
            new LocationId((int)PayloadData.Number(data, 2)));
    }

    /// <summary>Codec for <see cref="ActivityCompletionPayload"/>.</summary>
    public sealed class ActivityCompletionPayloadCodec : IScheduledEventPayloadCodec
    {
        public AuthoredId EventType => ScheduledEventTypes.ActivityComplete;

        public ScheduledEventPayloadData Encode(IScheduledEventPayload payload)
        {
            var typed = (ActivityCompletionPayload)payload;
            return PayloadData.Of(null, new long[] { typed.ActivityInstanceId.Value, typed.CharacterId.Value });
        }

        public IScheduledEventPayload Decode(ScheduledEventPayloadData data) => new ActivityCompletionPayload(
            new ActivityInstanceId((int)PayloadData.Number(data, 0)),
            new CharacterId((int)PayloadData.Number(data, 1)));
    }

    /// <summary>Codec for <see cref="TravelArrivalPayload"/>.</summary>
    public sealed class TravelArrivalPayloadCodec : IScheduledEventPayloadCodec
    {
        public AuthoredId EventType => ScheduledEventTypes.TravelArrival;

        public ScheduledEventPayloadData Encode(IScheduledEventPayload payload)
        {
            var typed = (TravelArrivalPayload)payload;
            return PayloadData.Of(
                null,
                new long[] { typed.ActivityInstanceId.Value, typed.CharacterId.Value, typed.DestinationLocationId.Value });
        }

        public IScheduledEventPayload Decode(ScheduledEventPayloadData data) => new TravelArrivalPayload(
            new ActivityInstanceId((int)PayloadData.Number(data, 0)),
            new CharacterId((int)PayloadData.Number(data, 1)),
            new LocationId((int)PayloadData.Number(data, 2)));
    }

    /// <summary>Codec for <see cref="NeedThresholdPayload"/>.</summary>
    public sealed class NeedThresholdPayloadCodec : IScheduledEventPayloadCodec
    {
        public AuthoredId EventType => ScheduledEventTypes.NeedThreshold;

        public ScheduledEventPayloadData Encode(IScheduledEventPayload payload)
        {
            var typed = (NeedThresholdPayload)payload;
            return PayloadData.Of(new[] { typed.NeedId.Value }, new long[] { typed.CharacterId.Value, typed.Threshold });
        }

        public IScheduledEventPayload Decode(ScheduledEventPayloadData data) => new NeedThresholdPayload(
            new CharacterId((int)PayloadData.Number(data, 0)),
            new AuthoredId(PayloadData.String(data, 0)),
            PayloadData.Number(data, 1));
    }

    /// <summary>Codec for <see cref="CommitmentWindowExpiredPayload"/>.</summary>
    public sealed class CommitmentWindowExpiredPayloadCodec : IScheduledEventPayloadCodec
    {
        public AuthoredId EventType => ScheduledEventTypes.CommitmentWindowExpired;

        public ScheduledEventPayloadData Encode(IScheduledEventPayload payload)
        {
            var typed = (CommitmentWindowExpiredPayload)payload;
            return PayloadData.Of(null, new long[] { typed.CommitmentId.Value, typed.CharacterId.Value });
        }

        public IScheduledEventPayload Decode(ScheduledEventPayloadData data) => new CommitmentWindowExpiredPayload(
            new CommitmentId((int)PayloadData.Number(data, 0)),
            new CharacterId((int)PayloadData.Number(data, 1)));
    }

    /// <summary>Codec for <see cref="DecisionResolvePayload"/>.</summary>
    public sealed class DecisionResolvePayloadCodec : IScheduledEventPayloadCodec
    {
        public AuthoredId EventType => ScheduledEventTypes.DecisionResolve;

        public ScheduledEventPayloadData Encode(IScheduledEventPayload payload)
        {
            var typed = (DecisionResolvePayload)payload;
            return PayloadData.Of(null, new long[] { typed.DecisionId.Value, typed.CharacterId.Value });
        }

        public IScheduledEventPayload Decode(ScheduledEventPayloadData data) => new DecisionResolvePayload(
            new DecisionId((int)PayloadData.Number(data, 0)),
            new CharacterId((int)PayloadData.Number(data, 1)));
    }
}
