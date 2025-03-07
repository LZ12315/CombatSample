using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Arrow : MonoBehaviour
{
    Vector3 localPos;
    Quaternion localRot;
    bool bingded;

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer != 6)
            return;

        // 绑定到骨骼
        transform.parent = other.transform;

        // 记录相对位置
        localPos = transform.localPosition;
        localRot = transform.localRotation;

        // 禁用物理
        Destroy(GetComponent<Rigidbody>());

        bingded = true;
    }

    void LateUpdate()
    {
        if (bingded)
        {
            // 保持相对位置
            transform.localPosition = localPos;
            transform.localRotation = localRot;
        }
    }
}
