using System.Collections.Generic;
using UnityEngine;
using Animancer; 

[RequireComponent(typeof(Actor))]
public class ActorLocomotion : MonoBehaviour
{
    private Actor _actor;
    private bool _isActive = false;

    [Header("配置")]
    [SerializeField, Tooltip("基础移动速度")] 
    private float _baseMoveSpeed = 5f;
    
    [SerializeReference, SubclassSelector, Tooltip("Locomotion自身的准入条件组")] 
    private List<ActionCondition> _entryConditions = new List<ActionCondition>();

    [SerializeField, Tooltip("默认的移动模式 (如: 普通自由移动)")]
    private LocomotionModeAsset _defaultMode;

    [SerializeField, Tooltip("特殊的移动模式列表 (按Priority仲裁)")]
    private List<LocomotionModeAsset> _specialModes = new List<LocomotionModeAsset>();

    // 缓存当前正在执行的模式
    private LocomotionModeAsset _currentMode;

    private void Awake()
    {
        _actor = GetComponent<Actor>();
    }

    // ==========================================
    // 供外部调用的显式接口
    // ==========================================

    public bool CheckConditions() 
    {
        if (_entryConditions == null || _entryConditions.Count == 0) return true;
        foreach (var condition in _entryConditions) 
        {
            if (!condition.Check(_actor)) return false;
        }
        return true;
    }

    public void StartLocomotion()
    {
        _isActive = true; // 记下自己被唤醒了
        
        _actor.movement.SetMovementMode(ActorMovement.MovementMode.CodeDriven);
        
        // 唤醒瞬间立刻评估并播放动画，保证0帧延迟的无缝衔接
        EvaluateAndPlayMode(); 
    }

    public void StopLocomotion()
    {
        _isActive = false; // 记下自己被关停了
        
        // 彻底刹车
        _actor.movement.SetCodeVelocity(Vector3.zero);
        
        // 清理缓存，保证下次重新启动时正常触发动画 Play
        _currentMode = null; 
    }

    // ==========================================
    // 业务逻辑 (小脑本职工作)
    // ==========================================

    private void Update()
    {
        // 极其纯粹：只要我没被激活，我就休息。
        if (!_isActive) 
            return;

        EvaluateAndPlayMode();
        ProcessLocomotion();
    }

    private void EvaluateAndPlayMode()
    {
        LocomotionModeAsset targetMode = _defaultMode;
        int highestPriority = -1; 

        // 寻找优先级最高的特殊模式（比如冲刺、瘸腿走）
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

        // 如果模式改变（或者刚被 StartLocomotion 唤醒），命令 Animancer 切换动画
        if (targetMode != _currentMode)
        {
            _currentMode = targetMode;

            if (_currentMode != null && _currentMode.Mixer != null)
            {
                _actor.animancer.Play(_currentMode.Mixer, _currentMode.FadeTime);
            }
        }
    }

    private void ProcessLocomotion()
    {
        Vector2 moveInput = _actor.logicInput.MoveInput; 
        float currentSpeed = _baseMoveSpeed * (_currentMode != null ? _currentMode.SpeedMultiplier : 1f);

        if (moveInput.sqrMagnitude > 0.01f)
        {
            // 算方向、转体
            Vector3 targetDir = _actor.cameraControl.CalculateWorldDirection(moveInput);
            _actor.movement.UpdateRotation(targetDir);

            // 算速度、移动
            Vector3 targetVelocity = targetDir * (moveInput.magnitude * currentSpeed);
            _actor.movement.SetCodeVelocity(targetVelocity);
            
            // 给混合树喂参数
            InjectAnimancerParameter(moveInput);
        }
        else
        {
            // 没推摇杆时刹车，并让混合树回到 Idle
            _actor.movement.SetCodeVelocity(Vector3.zero);
            InjectAnimancerParameter(Vector2.zero);
        }
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