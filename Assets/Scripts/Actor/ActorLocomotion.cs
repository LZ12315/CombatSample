using System.Collections.Generic;
using UnityEngine;
using Animancer; 

[RequireComponent(typeof(Actor))]
public class ActorLocomotion : MonoBehaviour
{
    private Actor _actor;

    [Header("基础配置")]
    [SerializeField, Tooltip("基础移动速度")] 
    private float _baseMoveSpeed = 5f;
    
    [SerializeReference, SubclassSelector, Tooltip("Locomotion自身的准入条件组")] 
    private List<ActionCondition> _entryConditions = new List<ActionCondition>();

    [Header("移动模式配置")]
    [SerializeField, Tooltip("默认的移动模式 (无需条件)")]
    private LocomotionModeAsset _defaultMode;

    [SerializeField, Tooltip("特殊的移动模式列表")]
    private List<LocomotionModeAsset> _specialModes = new List<LocomotionModeAsset>();

    private LocomotionModeAsset _currentMode;

    private void Awake()
    {
        _actor = GetComponent<Actor>();
    }

    public bool CheckConditions() 
    {
        if (_entryConditions == null || _entryConditions.Count == 0) return true;
        foreach (var condition in _entryConditions) 
        {
            if (!condition.Check(_actor)) return false;
        }
        return true;
    }

    private void OnEnable()
    {
        if (_actor == null) return; 

        _actor.movement.SetMovementMode(ActorMovement.MovementMode.CodeDriven);
        EvaluateAndPlayMode(); 
    }

    private void OnDisable()
    {
        if (_actor == null) return;
        _actor.movement.SetCodeVelocity(Vector3.zero);
        _currentMode = null; 
    }

    private void Update()
    {
        EvaluateAndPlayMode();
        ProcessLocomotion();
    }

    private void EvaluateAndPlayMode()
    {
        LocomotionModeAsset targetMode = _defaultMode;
        int highestPriority = -1; // 记录当前找到的最高优先级

        // 遍历所有特殊模式，找出满足条件且优先级最高的
        foreach (var mode in _specialModes)
        {
            if (mode != null && mode.CheckConditions(_actor))
            {
                int modePriority = (int)mode.Priority;
                // 如果优先级更高，则替换为目标模式
                if (modePriority > highestPriority)
                {
                    highestPriority = modePriority;
                    targetMode = mode;
                }
            }
        }

        // 如果目标模式发生了改变，执行平滑切换
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
            Vector3 targetDir = _actor.cameraControl.CalculateWorldDirection(moveInput);
            _actor.movement.UpdateRotation(targetDir);

            Vector3 targetVelocity = targetDir * (moveInput.magnitude * currentSpeed);
            _actor.movement.SetCodeVelocity(targetVelocity);
            
            InjectAnimancerParameter(moveInput);
        }
        else
        {
            _actor.movement.SetCodeVelocity(Vector3.zero);
            InjectAnimancerParameter(Vector2.zero);
        }
    }

    // 独立出参数注入方法，处理不同类型的混合树
    private void InjectAnimancerParameter(Vector2 input)
    {
        if (_currentMode == null || _currentMode.Mixer == null) return;

        // 通过 TransitionAsset 获取正在运行的 AnimancerState
        AnimancerState state = _actor.animancer.States.GetOrCreate(_currentMode.Mixer);

        // 判断当前播放的是什么类型的动画，并注入相应的参数
        if (state is MixerState<Vector2> mixer2D)
        {
            // 如果是 2D 八向/自由视角混合树，传入 Vector2
            mixer2D.Parameter = input;
        }
        else if (state is MixerState<float> mixer1D)
        {
            // 如果是 1D 混合树 (如 Idle-Walk-Run)，传入推杆力度
            mixer1D.Parameter = input.magnitude; 
        }
    }
}