using System;
using UnityEngine;

public class Example : MonoBehaviour
{
    public Actor actor;
    public ActionAsset asset;
    public ActionInstance assetInstance;

    private void OnEnable()
    {
        assetInstance = asset.CreateActionInstance();
        assetInstance.EnableTransitions(actor);
    }

    private void Update()
    {
        if(assetInstance == null) return;

        ActionAsset nextAction = assetInstance.CheckTransitions();
        if (nextAction != null)
        {
            Debug.Log("Transition");
            assetInstance.DisableTransitions();
            assetInstance = asset.CreateActionInstance();
            assetInstance.EnableTransitions(actor);
        }
    }

}