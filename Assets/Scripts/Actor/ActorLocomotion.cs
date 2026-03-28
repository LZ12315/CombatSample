using System.Collections.Generic;
using UnityEngine;
using Animancer;

/// <summary>
/// 在 locomotion 激活时执行移动与移动动画；意图由 <see cref="SetIntent"/> 提供（玩家经 <see cref="ActorLogicInput"/>，敌人可由 AI 填写）。
/// </summary>
[DefaultExecutionOrder(200)]
public class ActorLocomotion : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Actor _actor;

    [Header("Settings")]
    [SerializeField, Tooltip("Base move speed")]
    private float _baseMoveSpeed = 5f;

    [SerializeReference, SubclassSelector, Tooltip("Checks before this locomotion can run.")]
    private List<ActionCondition> _entryConditions = new List<ActionCondition>();

    [SerializeField, Tooltip("Default move mode (e.g. free walk)")]
    private LocomotionModeAsset _defaultMode;

    [SerializeField, Tooltip("Extra move modes. Higher priority wins.")]
    private List<LocomotionModeAsset> _specialModes = new List<LocomotionModeAsset>();

    private bool _isActive;
    private LocomotionModeAsset _currentMode;
    private LocomotionIntent _intent = LocomotionIntent.Idle;

    public bool IsActive => _isActive;

    public bool CheckConditions()
    {
        if (_entryConditions == null || _entryConditions.Count == 0) return true;
        foreach (var condition in _entryConditions)
        {
            if (!condition.Check(_actor)) return false;
        }
        return true;
    }

    /// <summary>每帧由 <see cref="ActorLogicInput"/> 或 AI 写入；未激活时仍会更新缓存。</summary>
    public void SetIntent(in LocomotionIntent intent)
    {
        _intent = intent;
    }

    public void ClearIntent()
    {
        _intent = LocomotionIntent.Idle;
    }

    public void StartLocomotion()
    {
        _isActive = true;

        _actor.movement.SetMovementMode(ActorMovement.MovementMode.CodeDriven);

        EvaluateAndPlayMode();
    }

    public void StopLocomotion()
    {
        _isActive = false;

        _actor.movement.SetCodeVelocity(Vector3.zero);
        _actor.movement.SetMovementMode(ActorMovement.MovementMode.RootMotion);

        _currentMode = null;
        ClearIntent();
    }

    private void Update()
    {
        if (!_isActive)
        {
            return;
        }
        EvaluateAndPlayMode();
        ApplyIntent();
    }

    private void EvaluateAndPlayMode()
    {
        LocomotionModeAsset targetMode = _defaultMode;
        int highestPriority = -1;

        foreach (var mode in _specialModes)
        {
            if (mode != null && mode.CheckConditions(_actor))
            {
                int modePriority = (int)mode.Priority;
                if (modePriority > highestPriority)
                {
                    highestPriority = modePriority;
                    targetMode = mode;
                }
            }
        }

        if (targetMode != _currentMode)
        {
            _currentMode = targetMode;

            if (_currentMode != null && _currentMode.Mixer != null)
            {
                _actor.animancer.Play(_currentMode.Mixer, _currentMode.FadeTime);
            }
        }
    }

    private void ApplyIntent()
    {
        float speedMul = _currentMode != null ? _currentMode.SpeedMultiplier : 1f;
        float speed = _baseMoveSpeed * speedMul;

        bool hasMove = _intent.MoveStrength > 0.01f && _intent.WorldMoveDirection.sqrMagnitude > 0.0001f;

        if (hasMove)
        {
            Vector3 dir = _intent.WorldMoveDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
            {
                _actor.movement.SetCodeVelocity(Vector3.zero);
                InjectAnimancerParameter(Vector2.zero);
                return;
            }
            dir.Normalize();

            _actor.movement.SetCodeVelocity(dir * (_intent.MoveStrength * speed));
        }
        else
        {
            _actor.movement.SetCodeVelocity(Vector3.zero);
        }

        Vector3 face = _intent.FacingDirection;
        face.y = 0f;
        if (face.sqrMagnitude > 0.0001f)
        {
            _actor.movement.UpdateRotation(face.normalized);
        }
        else if (hasMove)
        {
            Vector3 d = _intent.WorldMoveDirection;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f)
                _actor.movement.UpdateRotation(d.normalized);
        }

        InjectAnimancerParameter(hasMove ? _intent.Mixer2D : Vector2.zero);
    }

    private void InjectAnimancerParameter(Vector2 input)
    {
        if (_currentMode == null || _currentMode.Mixer == null) return;
        AnimancerState state = _actor.animancer.States.GetOrCreate(_currentMode.Mixer);

        if (state is MixerState<Vector2> mixer2D)
        {
            mixer2D.Parameter = input;
        }
        else if (state is MixerState<float> mixer1D)
        {
            mixer1D.Parameter = input.magnitude;
        }
    }
}
