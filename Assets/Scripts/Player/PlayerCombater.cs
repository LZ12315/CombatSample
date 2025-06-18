using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombater : CharacterCombater
{
    [field: Header("追踪体设置")]
    [SerializeField] private float AcqFadeSpeed = 5;
    [field: SerializeField] public float FullAcq { get; set; } = 100;

    [field: Header("追踪体信息")]
    [field: SerializeField] public bool isAcquisted { get; set; }
    [SerializeField] private float acquisition = 0;
    public float Acquisition
    {
        get => acquisition;
        set
        {
            if(value < 0)
                acquisition = 0;
            if(value > FullAcq)
                acquisition = FullAcq;
        }
    }
    [field: SerializeField] public float Quality { get; set; }

    protected override void Start()
    {
        base.Start();
        Acquisition = 0;
    }

    private void Update()
    {
        if(!isAcquisted)
            Acquisition -= AcqFadeSpeed * Time.deltaTime;
    }

}
