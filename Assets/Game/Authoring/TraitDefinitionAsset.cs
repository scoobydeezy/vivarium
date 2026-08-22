using System.Collections.Generic;
using UnityEngine;
using Vivarium.Domain.Characters;
using Vivarium.Domain.Common;
using Vivarium.Domain.Knowledge;

namespace Vivarium.Unity.Authoring
{
    /// <summary>
    /// Designer-facing authoring asset for a trait (§41).
    /// <para>
    /// ScriptableObjects are <b>authoring tools, not authoritative Domain types</b>. This one exists so
    /// the Inspector workflow stays pleasant; <see cref="ToDefinition"/> converts it into the Unity-free
    /// <see cref="TraitDefinition"/> the simulation actually consumes.
    /// </para>
    /// <para>
    /// Note that saves persist <see cref="authoredId"/> — a stable string — and never a reference to this
    /// asset (§39). Renaming or moving the asset is safe; changing the id is a content migration.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Vivarium/Trait Definition", fileName = "trait_")]
    public sealed class TraitDefinitionAsset : ScriptableObject
    {
        [Tooltip("Stable authored id, e.g. trait.ambitious. Persisted in saves — treat changes as migrations.")]
        [SerializeField] private string authoredId = "trait.";

        [SerializeField] private string displayName = string.Empty;

        [Tooltip("How the player can come to know this trait.")]
        [SerializeField] private DiscoveryChannelEntry[] discoverableThrough = new DiscoveryChannelEntry[0];

        [Tooltip("Whether changing this asset mid-session is a balance-only change (§42).")]
        [SerializeField] private bool hotReloadSafe = true;

        public string AuthoredId => authoredId;

        /// <summary>Converts authoring data into the immutable Domain definition.</summary>
        public TraitDefinition ToDefinition()
        {
            var channels = new List<DiscoveryChannel>(discoverableThrough.Length);
            for (int i = 0; i < discoverableThrough.Length; i++)
            {
                channels.Add(discoverableThrough[i].ToChannel());
            }

            return new TraitDefinition(new AuthoredId(authoredId), displayName, channels, hotReloadSafe);
        }

        /// <summary>Authoring-time validation, surfaced before gameplay rather than at runtime (§42).</summary>
        public IEnumerable<string> Validate()
        {
            if (string.IsNullOrEmpty(authoredId) || authoredId.EndsWith("."))
            {
                yield return $"{name}: authored id '{authoredId}' is incomplete.";
            }

            if (!authoredId.StartsWith("trait."))
            {
                yield return $"{name}: trait ids should be namespaced 'trait.<something>'.";
            }

            if (discoverableThrough.Length == 0)
            {
                yield return $"{name}: no discovery channels, so the player can never learn this trait.";
            }
        }
    }

    /// <summary>Inspector-friendly discovery channel.</summary>
    [System.Serializable]
    public struct DiscoveryChannelEntry
    {
        [Tooltip("Authored channel id, e.g. discovery.conversation.")]
        public string channelId;

        [Range(0, 10000)]
        [Tooltip("Difficulty in basis points. 0 always yields; 9000 is very hard to learn.")]
        public int difficultyBasisPoints;

        public DiscoveryChannel ToChannel() => new DiscoveryChannel(new AuthoredId(channelId), difficultyBasisPoints);
    }
}
