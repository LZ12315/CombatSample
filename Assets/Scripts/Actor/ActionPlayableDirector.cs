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

    private void OnDestroy()
    {
        _timelineEventManager.ClearAllSubscriptions();
    }

    public void PlayAction(ActionAsset action)
    {
        if (action == null || director == null) return;

        //启动ActionTimeline切换事件
        RaiseTransitionEvent(director);

        director.Stop();
        director.playableAsset = action.TimelineAsset;
        director.time = 0.0;
        director.Play();

        actor.blackboard.SetVariableValue("currentAction", action);
        actionPlaying = action;
    }

    private void OnActionStopped(PlayableDirector director)
    {
        if(actionPlaying == null) return;

        actionPlaying.DataReset();
    }
    private void Update()
    {
        if (actionPlaying != null)
        {
            double timelineProgress = director.time / director.duration;
            actionPlaying.actionAssetData.nomalizedTime = timelineProgress;
        }
    }

    #region Timeline切换事件

    private GenericEventManager<PlayableDirector> _timelineEventManager = new GenericEventManager<PlayableDirector>();

    public void RegisterForTimelineEvent(object registrant, Action<PlayableDirector> callback)
    {
        _timelineEventManager.Subscribe(registrant, callback);
    }

    public void UnregisterFromTimelineEvent(object registrant)
    {
        _timelineEventManager.Unsubscribe(registrant);
    }

    void RaiseTransitionEvent(PlayableDirector playabledirector)
    {
        _timelineEventManager.Publish(playabledirector);
    }

    #endregion

}