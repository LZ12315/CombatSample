using System.Collections;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

public class Animancer_Test : MonoBehaviour
{
    public AnimancerComponent animancer;
    public List<TransitionAsset> animationClips = new ();

    private void Start()
    {
        StartCoroutine(PlayAnimations());
    }

    IEnumerator PlayAnimations()
    {
        foreach (var anim in animationClips)
        {
            animancer.Play(anim);
            Debug.Log(anim.FadeDuration);
            yield return new WaitForSeconds(1.5f);
        }
    }

}
