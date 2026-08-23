using System;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Social
{
    /// <summary>Applies explicit, conservative long-timescale personality or preference changes.</summary>
    public sealed class SocialDriftService
    {
        public void ApplyPersonalityDelta(WorldState world, CharacterId characterId, AuthoredId dimension, long delta)
        {
            Character character = world.Characters.Get(characterId);
            SocialVector changed = character.Personality.Copy();
            changed.Set(dimension, changed[dimension] + delta);
            character.SetPersonality(changed);
            world.BumpRevision(new RevisionKey(characterId.ToRef(), RevisionAspects.Personality));
        }

        public void MarkPreferenceFieldChanged(WorldState world, CharacterId characterId, AuthoredId lensId)
        {
            Character character = world.Characters.Get(characterId);
            if (!character.TryGetAppraisalField(lensId, out AppraisalField field))
            {
                throw new InvalidOperationException($"{characterId} has no field for {lensId}.");
            }
            field.MarkDrifted();
            world.BumpRevision(new RevisionKey(
                characterId.ToRef(),
                RevisionAspects.Scoped(RevisionAspects.AppraisalField, lensId)));
        }
    }
}
