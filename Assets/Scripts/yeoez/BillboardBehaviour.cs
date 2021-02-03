﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
public class BillboardBehaviour : MonoBehaviour {

    private GameObject hmdCamera;
    private NetworkHead[] heads;

    // Use this for initialization
    void Start()
    {
        heads = transform.root.GetComponentsInChildren<NetworkHead>();
        foreach (var child in heads)
        {
            if (child.GetComponent<PhotonView>().IsMine)
            {
                hmdCamera = child.gameObject;
            }
        }

        if (!hmdCamera)
        {
            hmdCamera = GameObject.Find("CenterEyeAnchor");
        }
    }

    // Update is called once per frame
    void Update () {
        
        Vector3 v = transform.position - hmdCamera.transform.position;
        Quaternion q = Quaternion.LookRotation(v);
        transform.rotation = q;
	}
}
