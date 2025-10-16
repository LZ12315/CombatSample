using CombatSample.Consts;
using System;
using UnityEngine;
using UnityEngine.Playables;

public class ActionPlayableDirector : MonoBehaviour
{
    public PlayableDirector playableDirector;
    public ActorActionSetting actionSetting;
   
    ActionAsset actionPlaying;

    private void Awake()
    {
        playableDirector.extrapolationMode = DirectorWrapMode.None;
        playableDirector.stopped += OnDirectorStopped;
    }

    private void Start()
    {
        PlayAction(actionSetting.idle);
    }

    private void OnDestroy()
    {
        _timelineEventManager.ClearAllSubscriptions();
    }

    public void PlayAction(ActionAsset action)
    {
        if (action == null || playableDirector == null) return;

        //播放事件
        RaiseTransitionEvent(playableDirector);

        playableDirector.Stop();
        playableDirector.playableAsset = action.TimelineAsset;
        playableDirector.time = 0.0;
        playableDirector.Play();

        actionPlaying = action;
    }

    private void OnDirectorStopped(PlayableDirector director)
    {
        if(actionPlaying == null) return;

        if (actionPlaying.isLoop)
            PlayAction(actionPlaying);
        else if (actionPlaying.nextAction != null)
            PlayAction(actionPlaying.nextAction);
        else
        {
            if (actionPlaying.actionType == Enums.ActorActionType.Normal)
                PlayAction(actionSetting.idle);
            else
                PlayAction(actionSetting.fight_Idle);
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