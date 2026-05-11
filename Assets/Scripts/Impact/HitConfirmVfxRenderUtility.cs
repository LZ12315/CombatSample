using UnityEngine;

public static class HitConfirmVfxRenderUtility
{
    public const string HitConfirmVfxLayerName = "DeferredVFX";
    public const int OccluderStencilRef = 1;

    public static int HitConfirmVfxLayer => LayerMask.NameToLayer(HitConfirmVfxLayerName);

    public static bool TrySetHitConfirmLayer(GameObject root)
    {
        if (root == null) return false;

        int layer = HitConfirmVfxLayer;
        if (layer < 0)
            return false;

        SetLayerRecursively(root.transform, layer);
        return true;
    }

    static void SetLayerRecursively(Transform current, int layer)
    {
        current.gameObject.layer = layer;
        for (int i = 0; i < current.childCount; i++)
            SetLayerRecursively(current.GetChild(i), layer);
    }
}
