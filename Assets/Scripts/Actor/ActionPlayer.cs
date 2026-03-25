using UnityEngine;
using UnityEngine.Playables;
using System;

[RequireComponent(typeof(PlayableDirector))]
public class ActionPlayer : MonoBehaviour
{
    private PlayableDirector _director;
    private double _playbackSpeed = 1.0;
    public double PlaybackSpeed => _playbackSpeed;

    public ActionInstance CurrentAction { get; private set; }

    // ??????????????????????????
    public event Action<ActionInstance> OnActionFinished;
    public event Action<ActionInstance> OnActionInterrupted;

    private void Awake()
    {
        _director = GetComponent<PlayableDirector>();
        _director.extrapolationMode = DirectorWrapMode.None;
        _director.playableAsset = null; // ???????????????? Timeline
    }

    private void OnEnable()
    {
        _director.stopped += HandleDirectorStopped;
    }

    private void OnDisable()
    {
        _director.stopped -= HandleDirectorStopped;
    }

    /// <summary>
    /// ??????????????
    /// </summary>
    /// <param name="actionAsset">????????????</param>
    public void Play(ActionAsset actionAsset)
    {
        if (actionAsset == null || actionAsset.TimelineAsset == null)
        {
            Debug.LogWarning("?????????????ActionAsset?????Timeline??ActionAsset??", this);
            return;
        }

        // ???????????????
        CurrentAction = actionAsset.CreateActionInstance();
        _director.playableAsset = CurrentAction.Config.TimelineAsset;
        _director.time = 0;
        _playbackSpeed = 1.0;
        _director.Play();
        // ? ???????????????????
        // ??? Timeline ???????????????????? 0 ?????
        // ???????? Tag ???????? Block.Move ???????????????????????
        _director.Evaluate();
    }

    /// <summary>
    /// ?????????
    /// </summary>
    public void Stop()
    {
        if (_director.state == PlayState.Playing)
        {
            _director.Stop();
        }
        _director.playableAsset = null;
        _playbackSpeed = 1.0;
        CurrentAction = null;
    }

    public void Pause()
    {
        if (_director.state == PlayState.Playing)
        {
            _director.Pause();
        }
    }

    public void Resume()
    {
        if (_director.state == PlayState.Paused)
        {
            _director.Resume();
        }
    }

    public void SetSpeed(double speed)
    {
        _playbackSpeed = speed;
        if (_director.playableGraph.IsValid())
        {
            _director.playableGraph.GetRootPlayable(0).SetSpeed(speed);
        }
    }

    private void Update()
    {
        // ???????????????????????
        if (CurrentAction != null && _director.state == PlayState.Playing)
        {
            double normalizedTime = _director.duration > 0 ? _director.time / _director.duration : 0;
            CurrentAction.UpdateNormalizedTime(normalizedTime);
        }
    }

    /// <summary>
    /// ??Director???????????????
    /// </summary>
    private void HandleDirectorStopped(PlayableDirector director)
    {
        if (CurrentAction != null)
        {
            // ????????????????????????????? CurrentAction ??????
            ActionInstance actionToNotify = CurrentAction;

            // ? ????????????????????? NormalizedTime (?? 0.05 ????????????)
            // ??????? ActionInstance ??????????????? NormalizedTime
            if (actionToNotify.RuntimeData.normalizedTime >= 0.95f)
            {
                // Natural finish: subscriber StopCurrentAction runs OnExit (SelfTag, etc.).
                OnActionFinished?.Invoke(actionToNotify);
                if (CurrentAction == actionToNotify)
                    CurrentAction = null;
            }
            else
            {
                CurrentAction = null;
                OnActionInterrupted?.Invoke(actionToNotify);
            }
        }
    }
}