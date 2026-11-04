using UnityEngine;

namespace Survive.Economy
{
    [DisallowMultipleComponent]
    public sealed class PlayerRewardFeedback : MonoBehaviour
    {
        private AudioSource rewardAudio;
        public int RewardsPlayed { get; private set; }

        public void Play(AudioClip clip)
        {
            if (clip == null) return;
            if (rewardAudio == null)
            {
                rewardAudio = gameObject.AddComponent<AudioSource>();
                rewardAudio.playOnAwake = false;
                rewardAudio.spatialBlend = 0f;
                rewardAudio.volume = .55f;
                rewardAudio.priority = 64;
            }
            rewardAudio.PlayOneShot(clip);
            RewardsPlayed++;
        }
    }
}
