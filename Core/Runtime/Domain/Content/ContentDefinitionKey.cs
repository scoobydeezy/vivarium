using System;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Content
{
    public enum ContentDefinitionFamily
    {
        Trait = 0,
        Need = 1,
        Activity = 2,
        Decision = 3,
        Intervention = 4,
        LocationKind = 5,
        CommitmentTemplate = 6,
        AppraisalCalibration = 7,
        SocialEvidence = 8,
        CommitmentAccountabilityPolicy = 9,
        SocialPressure = 10,
        Employment = 11,
        DecisionImportancePolicy = 12,
    }

    public readonly struct ContentDefinitionKey : IEquatable<ContentDefinitionKey>
    {
        public ContentDefinitionKey(ContentDefinitionFamily family, AuthoredId id)
        {
            Family = family;
            Id = id;
        }

        public ContentDefinitionFamily Family { get; }
        public AuthoredId Id { get; }

        public bool Equals(ContentDefinitionKey other) => Family == other.Family && Id == other.Id;
        public override bool Equals(object obj) => obj is ContentDefinitionKey other && Equals(other);
        public override int GetHashCode() => ((int)Family * 397) ^ Id.GetHashCode();
        public override string ToString() => $"{Family}:{Id}";
    }
}
