using Vivarium.Domain.Common;

namespace Vivarium.Domain.Attention
{
    /// <summary>
    /// The <b>one canonical watch signal</b> for a character (§20.1).
    /// <para>
    /// Observation and Attention answer different questions — "what can the player learn?" versus
    /// "what should surface or become interactively playable?" — but they must never independently
    /// track whether the player is watching Mina. Both consume this (invariant 8).
    /// </para>
    /// <para>
    /// Mixed durability by design: <see cref="IsFollowed"/> is a durable player setting and belongs in
    /// the save; <see cref="IsVisible"/> is ephemeral camera state and does not (§8, §20.1). The
    /// persistence mapper keeps only the durable flags.
    /// </para>
    /// </summary>
    public readonly struct WatchState
    {
        public WatchState(bool isFollowed, bool isVisible, bool isSelected, bool isProfileOpen)
        {
            IsFollowed = isFollowed;
            IsVisible = isVisible;
            IsSelected = isSelected;
            IsProfileOpen = isProfileOpen;
        }

        /// <summary>Durable: the player asked to follow this character. Save state.</summary>
        public bool IsFollowed { get; }

        /// <summary>Ephemeral: currently visible for meaningful observation. Not save state.</summary>
        public bool IsVisible { get; }

        /// <summary>Ephemeral: currently selected.</summary>
        public bool IsSelected { get; }

        /// <summary>Ephemeral: their profile is open.</summary>
        public bool IsProfileOpen { get; }

        /// <summary>Whether the player is meaningfully watching, by any route.</summary>
        public bool IsWatched => IsFollowed || IsVisible || IsSelected || IsProfileOpen;

        /// <summary>Whether the character is close enough to attention to justify observation input (§25).</summary>
        public bool SupportsObservation => IsVisible || IsProfileOpen || IsFollowed;

        public WatchState WithFollowed(bool value) => new WatchState(value, IsVisible, IsSelected, IsProfileOpen);

        public WatchState WithVisible(bool value) => new WatchState(IsFollowed, value, IsSelected, IsProfileOpen);

        public WatchState WithSelected(bool value) => new WatchState(IsFollowed, IsVisible, value, IsProfileOpen);

        public WatchState WithProfileOpen(bool value) => new WatchState(IsFollowed, IsVisible, IsSelected, value);

        /// <summary>Clears everything ephemeral. Used when restoring a save (§38).</summary>
        public WatchState DurableOnly() => new WatchState(IsFollowed, false, false, false);

        public override string ToString() =>
            $"{(IsFollowed ? "F" : "-")}{(IsVisible ? "V" : "-")}{(IsSelected ? "S" : "-")}{(IsProfileOpen ? "P" : "-")}";
    }

    /// <summary>
    /// Attention policy for a character or a decision (§20). Names are not frozen by the architecture.
    /// </summary>
    public enum AttentionPolicy
    {
        /// <summary>Default handling.</summary>
        Normal = 0,

        /// <summary>Automatically hold qualifying newly-created Decisions.</summary>
        AutoHold = 1,

        /// <summary>Withhold auto-resolution so the player can intervene.</summary>
        Hold = 2,

        /// <summary>Suppress surfacing.</summary>
        Quiet = 3,
    }
}
