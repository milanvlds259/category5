using UnityEngine;
using System.Collections.Generic;

namespace Category5.UI
{
    // type of entity for minimap icon styling
    public enum TrackableType
    {
        Player,
        Enemy,
        Boss
    }

    // component that registers an entity to appear on the minimap radar
    // uses static self-registration pattern (like PlayerSpawnPoint)
    // attach to any gameobject that should appear as an icon on the minimap
    public class MinimapTrackable : MonoBehaviour
    {
        // static list of all active trackables
        private static List<MinimapTrackable> _allTrackables = new List<MinimapTrackable>();

        [Header("trackable settings")]
        [SerializeField] private TrackableType trackableType = TrackableType.Enemy;
        [SerializeField] private Color iconColor = Color.red;
        [SerializeField] private float iconSizeMultiplier = 1f;

        // accessors
        public TrackableType TrackableType => trackableType;
        public Color IconColor => iconColor;
        public float IconSizeMultiplier => iconSizeMultiplier;

        private void OnEnable()
        {
            if (!_allTrackables.Contains(this))
            {
                _allTrackables.Add(this);
            }
        }

        private void OnDisable()
        {
            _allTrackables.Remove(this);
        }

        // returns a copy of all currently active trackables
        public static List<MinimapTrackable> GetAll()
        {
            return new List<MinimapTrackable>(_allTrackables);
        }

        // returns the count of active trackables (avoids allocation)
        public static int Count => _allTrackables.Count;

        // direct access for iteration without allocation (just in case)
        public static List<MinimapTrackable> AllTrackables => _allTrackables;

        // configure this trackable at runtime
        public void Configure(TrackableType type, Color color, float sizeMultiplier = 1f)
        {
            trackableType = type;
            iconColor = color;
            iconSizeMultiplier = sizeMultiplier;
        }
    }
}
