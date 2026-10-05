using UnityEngine;

[CreateAssetMenu(menuName = "CombatSample/Locomotion/Mixer", fileName = "LocomotionMixer")]
public sealed class LocomotionMixerAsset : LocomotionAsset
{
    public override LocomotionRuntime CreateRuntime() => new LocomotionMixerRuntime(this);
}
