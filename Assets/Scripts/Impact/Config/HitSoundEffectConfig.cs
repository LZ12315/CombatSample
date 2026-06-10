using System;
using UnityEngine;

[Serializable]
[AddTypeMenu("Hit Sound", 3)]
public class HitSoundEffectConfig : ImpactEffectConfig
{
    [Tooltip("Pick one clip at random. Empty = no sound.")]
    public AudioClip[] clips;

    [Range(0f, 1f)]
    public float volume = 0.8f;

    [Tooltip("Random pitch around 1 ± this value.")]
    [Range(0f, 0.3f)]
    public float pitchVariation = 0.1f;
}
