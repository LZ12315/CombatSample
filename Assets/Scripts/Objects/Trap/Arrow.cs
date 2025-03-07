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

        // �󶨵�����
        transform.parent = other.transform;

        // ��¼���λ��
        localPos = transform.localPosition;
        localRot = transform.localRotation;

        // ��������
        Destroy(GetComponent<Rigidbody>());

        bingded = true;
    }

    void LateUpdate()
    {
        if (bingded)
        {
            // �������λ��
            transform.localPosition = localPos;
            transform.localRotation = localRot;
        }
    }
}
