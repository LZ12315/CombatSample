using CombatSample.Consts;
using System;
using UnityEngine;
using UnityEngine.Playables;

public class ActionPlayableDirector : MonoBehaviour
{
    public PlayableDirector playableDirector;
    public ActorActionSetting actionSetting;
    ActionTimelineAsset actionPlaying;

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
        //_timelineEventManager.ClearAllSubscriptions();
    }

    public void PlayAction(ActionTimelineAsset action)
    {
        if (action == null || playableDirector == null) return;

        //RaiseTransitionEvent(playableDirector);

        //停止当前正在播放的任何Timeline
        playableDirector.Stop(); // 这会立即停止播放并重置所有内部状态

        playableDirector.playableAsset = action.TimelineAsset;

        //将播放时间显式重置为0
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

    //#region Timeline切换事件

    //private GenericEventManager<PlayableDirector> _timelineEventManager = new GenericEventManager<PlayableDirector>();

    //public void RegisterForTimelineEvent(object registrant, Action<PlayableDirector> callback)
    //{
    //    _timelineEventManager.Subscribe(registrant, callback);
    //}

    //public void UnregisterFromTimelineEvent(object registrant)
    //{
    //    _timelineEventManager.Unsubscribe(registrant);
    //}

    //void RaiseTransitionEvent(PlayableDirector playabledirector)
    //{
    //   _timelineEventManager.Publish(playabledirector);
    //}

    //#endregion

}

public partial class Enums
{
    public enum ActionTimelineEvent
    {
        TransToNextAction
    }
}