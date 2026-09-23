using System;
using UnityEngine;

[Serializable]
public class ActionHitBoxConfig
{
    [HideInInspector] public ActionHitBoxShape shape = ActionHitBoxShape.Box;
    public Vector3 center = Vector3.zero;
    public Quaternion rotation = Quaternion.identity;
    [HideInInspector] public Vector3 size = new Vector3(0.5f, 0.5f, 0.5f);
    public float height = 0.5f;
    public float radius = 0.1f;
}

public enum ActionHitBoxShape
{
    Capsule = 0,
    Box = 1,
    Sphere = 2,
}
