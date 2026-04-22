using UnityEngine;

/// <summary>
/// 挂在场景任意物体上，勾选 <see cref="logVfxAnchorDiagnostics"/> 后播放：命中时输出命中体与 VFX 参考体是否一致、高度差。验证完取消勾选即可。
/// </summary>
[DisallowMultipleComponent]
public class HitVfxAnchorDiagnosticToggler : MonoBehaviour
{
    [SerializeField, Tooltip("开启时 HitVfxAnchor 相关逻辑会打 [HitVfxDiag] 日志。")]
    private bool logVfxAnchorDiagnostics;

    private void OnEnable() => Apply();

    private void OnValidate() => Apply();

    private void Update() => Apply();

    private void OnDisable() => Apply();

    private void OnDestroy() => HitVfxAnchorDiagnostics.LogEnabled = false;

    private void Apply() => HitVfxAnchorDiagnostics.LogEnabled = logVfxAnchorDiagnostics && isActiveAndEnabled;
}
