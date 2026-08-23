using Vivarium.Application.Persistence;

namespace Vivarium.Infrastructure.Persistence
{
    /// <summary>
    /// Promotes the legacy undirected affinity/familiarity pair into two identical directional
    /// starting states. Subsequent social evidence and history are free to make them diverge.
    /// </summary>
    public sealed class SaveV1ToV2SocialMigration : ISaveMigration
    {
        public int FromSchemaVersion => 1;
        public int ToSchemaVersion => 2;

        public void Apply(SaveGameData data)
        {
            for (int i = 0; i < data.Relationships.Count; i++)
            {
                RelationshipData relationship = data.Relationships[i];
                relationship.LowToHigh = CreateDirection(
                    relationship.LowCharacterId,
                    relationship.HighCharacterId,
                    relationship);
                relationship.HighToLow = CreateDirection(
                    relationship.HighCharacterId,
                    relationship.LowCharacterId,
                    relationship);
            }
        }

        private static DirectionalRelationshipData CreateDirection(
            int observer,
            int target,
            RelationshipData relationship)
        {
            var direction = new DirectionalRelationshipData
            {
                ObserverId = observer,
                TargetId = target,
                Familiarity = relationship.Familiarity,
                FamiliarityProgression = new ProgressionData
                {
                    ValueAtAnchor = relationship.Familiarity,
                    AnchoredAtMinutes = relationship.LastInteractionAtMinutes < 0 ? relationship.EstablishedAtMinutes : relationship.LastInteractionAtMinutes,
                    RateDenominator = 1,
                    MinValue = 0,
                    MaxValue = 10000,
                },
                HasFamiliarityProgression = true,
                ExposureMinutes = 0,
                LastInteractionAtMinutes = relationship.LastInteractionAtMinutes,
                Revision = 0,
            };
            direction.Channels.Add(new RelationshipChannelData
            {
                ChannelId = "relationship.channel.affection",
                Progression = Copy(relationship.Affinity),
            });
            return direction;
        }

        private static ProgressionData Copy(ProgressionData source) => new ProgressionData
        {
            ValueAtAnchor = source.ValueAtAnchor,
            AnchoredAtMinutes = source.AnchoredAtMinutes,
            RateNumerator = source.RateNumerator,
            RateDenominator = source.RateDenominator,
            MinValue = source.MinValue,
            MaxValue = source.MaxValue,
        };
    }
}
