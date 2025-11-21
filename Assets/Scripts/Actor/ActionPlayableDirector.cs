using CombatSample.Consts;
using System;
using UnityEngine;
using UnityEngine.Playables;

public class ActionPlayableDirector : MonoBehaviour
{
    public Actor actor;
    public PlayableDirector director;
   
    [SerializeField] private ActionAsset actionPlaying;
    public ActionAsset ActionPlaying => actionPlaying;

    private void Awake()
    {
        director.extrapolationMode = DirectorWrapMode.None;
        director.stopped += OnActionStopped;
    }

    private void Update()
    {
        if (actionPlaying != null)
        {
            double timelineProgress = director.time / director.duration;
            actionPlaying.actionAssetData.nomalizedTime = timelineProgress;
        }
    }

    private void OnDestroy()
    {
        _timelineEventManager.ClearAllSubscriptions();
    }

    public void PlayAction(ActionAsset action)
    {
        if (action == null || director == null) return;

        director.Stop();
        director.playableAsset = action.TimelineAsset;
        director.time = 0.0;
        director.Play();

        actionPlaying = action;
    }

    private void OnActionStopped(PlayableDirector director)
    {
        if(actionPlaying == null) return;

        actionPlaying.DataReset();
    }

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

    public double GetTimelineSpeed()
    {
        return director.playableGraph.GetRootPlayable(0).GetSpeed();
    }

    #endregion


    #region Timeline事件

    private GenericEventManager<PlayableDirector> _timelineEventManager = new GenericEventManager<PlayableDirector>();

    public void RegisterForTimelineEvent(object registrant, Action<PlayableDirector> callback)
    {
        _timelineEventManager.Subscribe(registrant, callback);
    }

    public void UnregisterFromTimelineEvent(object registrant)
    {
        _timelineEventManager.Unsubscribe(registrant);
    }

    void RaiseTimelineEvent(PlayableDirector playabledirector)
    {
        _timelineEventManager.Publish(playabledirector);
    }

    #endregion

}