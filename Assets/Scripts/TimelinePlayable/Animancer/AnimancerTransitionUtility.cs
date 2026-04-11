using Animancer;

public enum AnimancerTransitionKind
{
    Unknown = 0,
    Clip = 1,
    Directional = 2,
    Mixer2D = 3,
    Linear = 4,
}

public static class AnimancerTransitionUtility
{
    private static bool TryGetTransition(TransitionAsset asset, out ITransitionDetailed transition)
    {
        transition = null;
        if (asset == null || !asset.HasTransition)
            return false;

        transition = asset.Transition;
        return transition != null;
    }

    public static AnimancerTransitionKind GetKind(TransitionAsset asset)
    {
        if (!TryGetTransition(asset, out var transition))
            return AnimancerTransitionKind.Unknown;

        return transition switch
        {
            DirectionalClipTransition => AnimancerTransitionKind.Directional,
            ClipTransition => AnimancerTransitionKind.Clip,
            LinearMixerTransition => AnimancerTransitionKind.Linear,
            MixerTransition2D => AnimancerTransitionKind.Mixer2D,
            _ => AnimancerTransitionKind.Unknown,
        };
    }

    public static double GetDuration(TransitionAsset asset)
    {
        if (!TryGetTransition(asset, out var transition))
            return 0d;

        if (transition.MaximumDuration > 0f)
            return transition.MaximumDuration;

        if (transition is ClipTransition clipTransition && clipTransition.Clip != null)
            return clipTransition.Clip.length;

        if (transition is DirectionalClipTransition directionalTransition &&
            directionalTransition.AnimationSet != null &&
            directionalTransition.AnimationSet.GetClip(0) != null)
            return directionalTransition.AnimationSet.GetClip(0).length;

        return 0d;
    }

    public static string GetDisplayName(TransitionAsset asset)
    {
        if (asset == null)
            return "Animancer";

        if (!TryGetTransition(asset, out var transition))
            return string.IsNullOrWhiteSpace(asset.name) ? "Animancer" : asset.name;

        if (transition is DirectionalClipTransition directionalTransition &&
            directionalTransition.AnimationSet != null)
            return $"{(string.IsNullOrWhiteSpace(asset.name) ? "Animancer" : asset.name)} [Directional]";

        if (transition is ClipTransition clipTransition && clipTransition.Clip != null)
            return clipTransition.Clip.name;

        string baseName = string.IsNullOrWhiteSpace(asset.name) ? "Animancer" : asset.name;

        return GetKind(asset) switch
        {
            AnimancerTransitionKind.Directional => $"{baseName} [Directional]",
            AnimancerTransitionKind.Mixer2D => $"{baseName} [2D]",
            AnimancerTransitionKind.Linear => $"{baseName} [Linear]",
            _ => baseName,
        };
    }
}
