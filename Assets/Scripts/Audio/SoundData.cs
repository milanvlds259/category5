using UnityEngine;

namespace Category5.Audio
{
    // scriptable object for designer-friendly audio configuration
    // create via: right-click > Create > Category5 > Sound Data
    [CreateAssetMenu(fileName = "NewSound", menuName = "Category5/Sound Data")]
    public class SoundData : ScriptableObject
    {
        [Header("audio clip")]
        [Tooltip("the main audio clip to play. if multiple clips are assigned, one will be randomly selected")]
        public AudioClip[] clips;
        
        [Header("volume")]
        [Range(0f, 1f)]
        [Tooltip("base volume for this sound")]
        public float volume = 1f;
        
        [Range(0f, 0.5f)]
        [Tooltip("random volume variation (+/-) applied each time the sound plays")]
        public float volumeVariation = 0.1f;
        
        [Header("pitch")]
        [Range(0.1f, 3f)]
        [Tooltip("base pitch for this sound")]
        public float pitch = 1f;
        
        [Range(0f, 0.5f)]
        [Tooltip("random pitch variation (+/-) applied each time the sound plays")]
        public float pitchVariation = 0.1f;
        
        [Header("spatial settings")]
        [Tooltip("if true, sound plays in 3D space and attenuates with distance")]
        public bool is3D = true;
        
        [Range(0f, 50f)]
        [Tooltip("minimum distance before volume starts to attenuate")]
        public float minDistance = 1f;
        
        [Range(1f, 100f)]
        [Tooltip("maximum distance at which sound can still be heard")]
        public float maxDistance = 30f;
        
        [Header("playback")]
        [Tooltip("if true, this sound will loop")]
        public bool loop = false;
        
        [Range(0, 256)]
        [Tooltip("audio source priority (0 = highest priority, 256 = lowest). lower priority sounds may be culled when too many are playing")]
        public int priority = 128;
        
        [Header("mixer")]
        [Tooltip("optional: audio mixer group to route this sound through (SFX, Music, UI, etc)")]
        public UnityEngine.Audio.AudioMixerGroup mixerGroup;
        
        // returns a random clip from the clips array
        public AudioClip GetClip()
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[Random.Range(0, clips.Length)];
        }
        
        // returns volume with random variation applied
        public float GetVolume()
        {
            return volume + Random.Range(-volumeVariation, volumeVariation);
        }
        
        // returns pitch with random variation applied
        public float GetPitch()
        {
            return pitch + Random.Range(-pitchVariation, pitchVariation);
        }
    }
}
