using System.Collections.Generic;
using UnityEngine;
using Animancer; 

public class ActorLocomotion : MonoBehaviour
{
    [Header("组件引用")]
    [SerializeField] private Actor _actor;

    [Header("配置")]
    [SerializeField, Tooltip("基础移动速度")] 
    private float _baseMoveSpeed = 5f;
    
    [SerializeReference, SubclassSelector, Tooltip("Locomotion自身的准入条件组")] 
    private List<ActionCondition> _entryConditions = new List<ActionCondition>();

    [SerializeField, Tooltip("默认的移动模式 (如: 普通自由移动)")]
    private LocomotionModeAsset _defaultMode;

    [SerializeField, Tooltip("特殊的移动模式列表 (按Priority仲裁)")]
    private List<LocomotionModeAsset> _specialModes = new List<LocomotionModeAsset>();

    // 是否被激活（StartLocomotion）
    private bool _isActive = false;
    // 缓存当前正在执行的模式
    private LocomotionModeAsset _currentMode;

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
        _actor.movement.SetMovementMode(ActorMovement.MovementMode.RootMotion);
        
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
        {
            return;
        }
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
        // 限制输入向量的模长最大为1，解决斜向加速问题
        Vector2 rawInput = _actor.logicInput.MoveInput;
        Vector2 moveInput = Vector2.ClampMagnitude(rawInput, 1f);
        float currentSpeed = _baseMoveSpeed * (_currentMode != null ? _currentMode.SpeedMultiplier : 1f);

        if (moveInput.sqrMagnitude > 0.01f)
        {
            // 1. 计算世界空间下的移动方向，并设置物理速度 (这部分不变，摇杆推哪往哪走)
            Vector3 targetVelocityDir = _actor.cameraControl.CalculateWorldDirection(moveInput);
            _actor.movement.SetCodeVelocity(targetVelocityDir * (moveInput.magnitude * currentSpeed));

            // 🌟 核心修正：判断当前是否是锁定模式
            bool isLockOn = _actor.cameraControl.CinemachineState != Enums.PlayerCameraState.Free;

            if (isLockOn)
            {
                // ==========================================
                // 锁定模式 (Lock)：八向移动跑法
                // ==========================================
                // 1. 旋转：强制面朝敌人 (或者面朝相机前方)
                Transform enemy = _actor.combater.CombatTarget?.transform;
                if (enemy != null)
                {
                    Vector3 dirToEnemy = enemy.position - _actor.transform.position;
                    dirToEnemy.y = 0;
                    if (dirToEnemy.sqrMagnitude > 0.001f)
                    {
                        _actor.movement.UpdateRotation(dirToEnemy.normalized);
                    }
                }
                else 
                {
                    // 如果没有敌人但处于某种锁定态，面朝相机正前方
                    Vector3 camForward = Camera.main.transform.forward;
                    camForward.y = 0;
                    _actor.movement.UpdateRotation(camForward.normalized);
                }

                // 2. 动画：原汁原味注入摇杆 (X左右，Y前后)，触发侧滑和后退动画
                InjectAnimancerParameter(moveInput);
            }
            else
            {
                // ==========================================
                // 自由模式 (Free)：传统跟随摇杆跑法
                // ==========================================
                // 1. 旋转：转向摇杆推的方向
                _actor.movement.UpdateRotation(targetVelocityDir);

                // 2. 动画：因为角色已经转过去面朝目标了，所以对他来说永远是在"往前走"
                // 强行把 X 轴设为 0，Y 轴设为摇杆推力，只触发 Run Forward 动画！
                InjectAnimancerParameter(new Vector2(0f, moveInput.magnitude));
            }
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