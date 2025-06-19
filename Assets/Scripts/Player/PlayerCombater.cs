using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombater : CharacterCombater
{
    [field: Header("追踪体参数")]
    [SerializeField] private float AcqFadeSpeed = 5;
    [SerializeField] private float AcqContainsTime = 2;
    [SerializeField] private float fullAcq { get; set; } = 100;

    [field: Header("追踪体信息")]
    [field: SerializeField] public bool IsAcquised {  get; private set; } = false;
    [SerializeField] private float acquisition = 0;
    public float Acquisition
    {
        get => acquisition;
        set
        {
            acquisition = value;

            if(acquisition < 0)
                acquisition = 0;

            if (acquisition >= fullAcq)
            {
                acquisition = fullAcq;
                IsAcquised = true;
                acqCunter = AcqContainsTime;
            }

            if(value > 0)
            {
                isAcquisting = true;
                acqCunter = AcqContainsTime;
            }
        }
    }

    bool isAcquisting = false;
    float acqCunter = 0;

    protected override void Start()
    {
        base.Start();

        Acquisition = 0;
    }

    private void Update()
    {
        if (isAcquisting && acqCunter > 0)
            acqCunter -= Time.deltaTime;
        if(acqCunter <= 0)
            isAcquisting = false;

        if(!isAcquisting)
            Acquisition -= AcqFadeSpeed * Time.deltaTime;
    }

}
