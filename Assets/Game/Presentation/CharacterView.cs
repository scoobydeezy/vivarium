using UnityEngine;
using Vivarium.Application.Queries;
using Vivarium.Domain.Common;

namespace Vivarium.Unity.Presentation
{
    /// <summary>
    /// The Unity representation of a simulated character (§43).
    /// <para>
    /// It <b>represents</b> a character; it does not own one. Everything authoritative lives behind
    /// <see cref="CharacterId"/> in the simulation, and this object only ever asks: where is this
    /// character, what are they doing, and what presentation state should show that?
    /// </para>
    /// <para>
    /// Deliberately has no <c>Update</c> that touches simulation state (§50). It refreshes when handed a
    /// new read model at a quiescent boundary, and interpolates visuals between refreshes — because
    /// travel progress is analytically derivable, so smooth movement needs no authoritative ticking
    /// (§29.2, invariant 42).
    /// </para>
    /// <para>
    /// If a character is not visible, no GameObject needs to exist at all. Thousands of simulated
    /// characters do not imply thousands of active Unity objects (invariant 69).
    /// </para>
    /// </summary>
    public sealed class CharacterView : MonoBehaviour
    {
        [SerializeField] private Transform indicatorRoot;

        [SerializeField] private float moveSmoothing = 8f;

        private Vector3 _targetPosition;
        private bool _hasTarget;

        /// <summary>Which simulated character this object stands for. Assigned by the view pool.</summary>
        public CharacterId CharacterId { get; private set; }

        /// <summary>The most recent read model this view rendered.</summary>
        public CharacterProfileView Profile { get; private set; }

        public void Bind(CharacterId characterId)
        {
            CharacterId = characterId;
            Profile = null;
            _hasTarget = false;
        }

        /// <summary>
        /// Applies a projection published at a quiescent boundary (§13.1).
        /// <para>
        /// The view never reads mutable Domain entities — only read models (§35, invariant 56).
        /// </para>
        /// </summary>
        public void Apply(CharacterProfileView profile, Vector3 worldPosition)
        {
            Profile = profile;
            _targetPosition = worldPosition;

            if (!_hasTarget)
            {
                transform.position = worldPosition;
                _hasTarget = true;
            }

            if (indicatorRoot != null)
            {
                indicatorRoot.gameObject.SetActive(profile != null && profile.IsFollowed);
            }
        }

        /// <summary>
        /// Purely cosmetic smoothing. Nothing here feeds back into the simulation, and
        /// <c>Time.deltaTime</c> is used only for rendering (§9).
        /// </summary>
        private void Update()
        {
            if (!_hasTarget)
            {
                return;
            }

            transform.position = Vector3.Lerp(transform.position, _targetPosition, 1f - Mathf.Exp(-moveSmoothing * Time.deltaTime));
        }
    }
}
