using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

public class ActionHitBoxBehavior : ActionBehaviourBase
{
    public Transform boneTransform;
    public ActionHitBoxConfig hitboxConfig;
    public AttackImpactConfig impactConfig;
    public AttackDataConfig dataConfig;

    public GameObject hitboxObject;
    public CapsuleCollider collider;

    protected override void OnClipPlay(Playable playable)
    {
        CreateHitbox();
    }

    protected override void OnClipFinish(bool isNormal)
    {
        DestroyHitbox();
    }

    #region HitBox管理方法

    private void CreateHitbox()
    {
        if (hitboxObject != null || boneTransform == null) return;

        hitboxObject = new GameObject("HitBox");
        hitboxObject.hideFlags = HideFlags.HideInHierarchy;

        // 添加Collider
        collider = hitboxObject.AddComponent<CapsuleCollider>();
        collider.isTrigger = true;

        // 添加AttackHandler
        var hitbox = collider.gameObject.AddComponent<AttackHandler>();
        hitbox.Init(actor.combater, dataConfig);

        // 注册攻击事件
        hitbox.RegisterHitStartEvent(this, OnHitStart);
        hitbox.RegisterHitOverEvent(this, OnHitOver);

        //添加辅助组件Updater
        var updater = hitboxObject.AddComponent<HitBoxUpdater>();
        updater.Init(this);
    }

    public void UpdateHitbox()
    {
        if (hitboxObject == null || boneTransform == null) return;

        // 更新位置和旋转
        hitboxObject.transform.position = boneTransform.TransformPoint(hitboxConfig.center);
        hitboxObject.transform.rotation = boneTransform.rotation * hitboxConfig.rotation;

        if (collider == null) return;

        // 更新碰撞体参数
        collider.height = hitboxConfig.height;
        collider.radius = hitboxConfig.radius;

        // 设置胶囊方向（默认为Y轴）
        collider.direction = 1; // 1 = Y轴
    }

    private void DestroyHitbox()
    {
        if (hitboxObject == null) return;

        // 取消注册攻击事件
        var hitbox = collider.gameObject.GetComponent<AttackHandler>();
        hitbox.UnregisterHitStartEvent(this);
        hitbox.UnregisterHitOverEvent(this);

        // 销毁辅助组件
        var updater = hitboxObject.GetComponent<HitBoxUpdater>();
        if (updater != null)
        {
            if (Application.isPlaying)
                Object.Destroy(updater);
            else
                Object.DestroyImmediate(updater);
        }

            // 销毁游戏对象
        if (Application.isPlaying)
            Object.Destroy(hitboxObject);
        else
            Object.DestroyImmediate(hitboxObject);

        hitboxObject = null;
        collider = null;
    }

    #endregion

    #region 攻击反馈方法

    void OnHitStart(AttackHitData data)
    {
        switch (impactConfig.impactType)
        {
            case Enums.HitImpactType.HitStop:
                actor.StartCoroutine(HitStop(impactConfig.stopTime));
                break;
            case Enums.HitImpactType.HitStick:
                actor.actionPlayer.SetSpeed(impactConfig.stickStrength);
                break;
        }
    }

    void OnHitOver(AttackHitData data)
    {
        switch (impactConfig.impactType)
        {
            case Enums.HitImpactType.HitStick:
                actor.actionPlayer.SetSpeed(1);
                break;
        }
    }

    IEnumerator HitStop(float stopTime)
    {
        actor.actionPlayer.Stop();

        yield return new WaitForSeconds(stopTime);

        actor.actionPlayer.Resume();
    }

    #endregion

    #region 辅助工具

    //辅助更新组件 用于更新HitBox状态 否则其相对动画会慢一帧
    [ExecuteInEditMode]
    private class HitBoxUpdater : MonoBehaviour
    {
        private ActionHitBoxBehavior _clip;

        public void Init(ActionHitBoxBehavior clip)
        {
            _clip = clip;
        }

        private void Update()
        {
            UpdateInEditMode();
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorApplication.update += UpdateInEditMode;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorApplication.update -= UpdateInEditMode;
#endif
        }

        private void UpdateInEditMode()
        {
            if (_clip != null && _clip.state == Enums.ActionClipState.Play)
            {
                _clip.UpdateHitbox();
            }
        }
    }

    #endregion

}