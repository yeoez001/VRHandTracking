﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Photon.Pun;

public class CustomGrabbable : OVRGrabbable
{ 
    private Color originalColour;
    private MeshRenderer mesh;
    private PhotonView photonView;
    private CustomGrabber collidedHand;

    protected override void Start() 
    {
        base.Start();
        mesh = GetComponent<MeshRenderer>();
        photonView = this.gameObject.GetComponent<PhotonView>();    
        photonView.OwnershipTransfer = OwnershipOption.Takeover;

        if (mesh.material.color != null)
        {
            originalColour = mesh.material.color;
        };
    }

    private void Update()
    {
       if (collidedHand)
        {
            if (collidedHand.grabbedObject != null && !collidedHand.grabbedObject.Equals(this))
            {
                ResetColour();
                collidedHand = null;
            }
        }
    }

    private void OnTriggerStay(Collider collision)
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            // Comparison done with name as tags are not detected on the hands for unknown reasons
            if (collision.name == "HandHighlight_L" || collision.name == "HandHighlight_R")
            {
                CustomGrabber hand = collision.gameObject.GetComponentInParent<CustomGrabber>();
                if (!hand.grabbedObject && mesh.material.color != Color.green)
                {
                    photonView.RPC("ChangeGrabbableColour", RpcTarget.AllBuffered, "blue", 0f, 0f, 0f);                    
                    collidedHand = hand;
                }                
            }

            else if (collision.name == "Laser_L" || collision.name == "Laser_R")
            {
                photonView.RPC("ChangeGrabbableColour", RpcTarget.AllBuffered, "yellow", 0f, 0f, 0f);
            }
        }   
    }

    private void OnTriggerExit(Collider collision)
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            if (collision.name == "HandHighlight_L" || collision.name == "HandHighlight_R" || collision.name == "Laser_L" || collision.name == "Laser_R")
            {
                ResetColour();
                collidedHand = null;
            }
        }
    }

    public void ResetColour()
    {
        photonView.RPC("ChangeGrabbableColour", RpcTarget.AllBuffered, "null", originalColour.r, originalColour.g, originalColour.b);
    }

    [PunRPC]
    void ChangeGrabbableColour(string colour, float r, float g, float b)
    {
        mesh = GetComponent<MeshRenderer>();
        if (colour.Equals("blue"))
        {
            mesh.material.color = Color.blue;
        }
        else if (colour.Equals("yellow"))
        {
            mesh.material.color = Color.yellow;
        }
        else if (colour.Equals("green"))
        {
            mesh.material.color = Color.green;
        }
        else if (colour.Equals("null"))
        {
            mesh.material.color = new Color(r, g, b);
        }        
    }
}
