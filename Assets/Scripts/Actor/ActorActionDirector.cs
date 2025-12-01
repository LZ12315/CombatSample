using CombatSample.Consts;
using System;
using UnityEngine;
using UnityEngine.Playables;

public class ActorActionDirector : MonoBehaviour
{
    [Header("配置")]
    public Actor actor;
    public PlayableDirector director;

    [Header("属性")]
    public bool controlActionTransition = false;

    [Header("数据")]
    private ActionInstance currentAction;
    public ActionInstance CurrentAction => currentAction;

    private void Awake()
    {
        director.extrapolationMode = DirectorWrapMode.None;
        director.stopped += OnActionStopped;
    }

    private void Update()
    {
        if (currentAction != null)
        {
            double timelineProgress = director.time / director.duration;
            CurrentAction.UpdateRuntimeData(timelineProgress, CurrentAction.RuntimeData.phase);
        }

        if (controlActionTransition)
        {
            ActionAsset nextAction = CurrentAction.CheckTransitions();
            if (nextAction != null)
                SwitchAction(nextAction);
        }
    }

    void StopAction()
    {
        director.Stop();

        if (controlActionTransition)
            CurrentAction.DisableTransitions();
    }

    void PlayAction(ActionAsset actionToPlay)
    {
        director.time = 0.0;
        director.playableAsset = actionToPlay.TimelineAsset;
        director.Play();

        currentAction = actionToPlay.CreateActionInstance();
        if (controlActionTransition)
            CurrentAction.EnableTransitions(actor);
    }

    public void SwitchAction(ActionAsset nextAction)
    {
        if (nextAction == null || director == null) return;

        StopAction();
        PlayAction(nextAction);
    }

    private void OnActionStopped(PlayableDirector director){  }

    #region Timeline控制方法

    private double lastDirectorSpeed = 0;

    public void SetTimelineSpeed(double speed)
    {
        if(director == null) return;

        lastDirectorSpeed = director.playableGraph.GetRootPlayable(0).GetSpeed();
        director.playableGraph.GetRootPlayable(0).SetSpeed(speed);
    }

    public void RestoreTimelineSpeed()
    {
        if (director == null) return;

        director.playableGraph.GetRootPlayable(0).SetSpeed(lastDirectorSpeed);
    }

    // 有问题
    public void PauseTimeline()
    {
        director.Pause();
    }

    // 待处理 
    public void ResumeTimeline()
    {
        director.Resume();
    }

    #endregion


}