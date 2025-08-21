using System.Collections;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

public class Animancer_Test : MonoBehaviour
{
    public AnimancerComponent animancer;
    public List<ClipTransition> animationClips = new ();

    private void Start()
    {
        StartCoroutine(PlayAnimations());
    }

    IEnumerator PlayAnimations()
    {
        foreach (var anim in animationClips)
        {
            animancer.Play(anim);
            yield return new WaitForSeconds(1.5f);
        }
    }

}
