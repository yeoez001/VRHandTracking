﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using OculusSampleFramework;
using Photon.Pun;
public class CustomGrabber : OVRGrabber
{
    public GameObject laserPointerObj;

    private OVRHand m_hand;
    private GestureDetector gesture;
    private PhotonView photonView;    
    private CustomLaserPointer laserPointer;

    private Vector3 previousPosition;
    private Quaternion lastRot;
    private Vector3 linearVelocity;
    private Vector3 angularVelocity;
    
    protected override void Start()
    {
        base.Start();
        m_hand = gameObject.GetComponent<OVRHand>();
        photonView = gameObject.GetComponent<PhotonView>();
        gesture = GetComponentInChildren<GestureDetector>();

        if (laserPointerObj)
        {
            laserPointer = laserPointerObj.GetComponent<CustomLaserPointer>();
            laserPointer.ShowLaser(false);
        }
        previousPosition = transform.position;
    }
        
    override public void Update()
    {
        base.Update();
        CheckGesture();

        if (m_grabbedObj && Application.platform == RuntimePlatform.Android)
        {
            m_grabbedObj.GetComponent<PhotonView>().RPC("ChangeGrabbableColour", RpcTarget.AllBuffered, "green", 0f, 0f, 0f);
        }
        previousPosition = transform.position;
        lastRot = transform.rotation;                   
    }

    void CheckGesture()
    {
        if (!m_hand.IsSystemGestureInProgress)
        {
            if (!m_grabbedObj && (gesture.isIndexPinching() || gesture.isGrabbing()) && m_grabCandidates.Count > 0 && !gesture.isPointing())
            {
                GrabBegin();
            }

            if (!gesture.isAnyPinching() && !gesture.isGrabbing() && m_grabbedObj)
            {
                m_grabbedObj.GetComponent<CustomGrabbable>().ResetColour();
                GrabEnd();
            }

            if (laserPointerObj)
            {
                if (gesture.isPointing())
                {
                    laserPointer.ShowLaser(true);
                }
                else
                {
                    laserPointer.ShowLaser(false);
                }
            }
        }
    }

    protected override void GrabBegin()
    {
        float closestMagSq = float.MaxValue;
        OVRGrabbable closestGrabbable = null;
        Collider closestGrabbableCollider = null;

        // Iterate grab candidates and find the closest grabbable candidate
        foreach (OVRGrabbable grabbable in m_grabCandidates.Keys)
        {
            bool canGrab = !(grabbable.isGrabbed && !grabbable.allowOffhandGrab);
            if (!canGrab)
            {
                continue;
            }

            for (int j = 0; j < grabbable.grabPoints.Length; ++j)
            {
                Collider grabbableCollider = grabbable.grabPoints[j];
                // Store the closest grabbable
                Vector3 closestPointOnBounds = grabbableCollider.ClosestPointOnBounds(m_gripTransform.position);
                float grabbableMagSq = (m_gripTransform.position - closestPointOnBounds).sqrMagnitude;
                if (grabbableMagSq < closestMagSq)
                {
                    closestMagSq = grabbableMagSq;
                    closestGrabbable = grabbable;
                    closestGrabbableCollider = grabbableCollider;
                }
            }
        }

        // Disable grab volumes to prevent overlaps
        GrabVolumeEnable(false);

        if (closestGrabbable != null)
        {
            if (closestGrabbable.isGrabbed)
            {
                CustomGrabber previousGrabber = (CustomGrabber) closestGrabbable.grabbedBy;
                previousGrabber.OffhandGrabbed(closestGrabbable);

                previousGrabber.GrabVolumeEnable(true);
            }

            m_grabbedObj = closestGrabbable;

            // Added to transfer ownership of children of the grabbed object to the grabbing client
            m_grabbedObj.GetComponent<PhotonView>().TransferOwnership(photonView.Owner);
            PhotonView[] pvs;
            NodeSelector node = m_grabbedObj.GetComponent<NodeSelector>();
            if (node && node.NodeChildPhotonView())
            {
                node.NodeChildPhotonView().gameObject.transform.SetParent(m_grabbedObj.transform);
                node.NodeChildPhotonView().TransferOwnership(photonView.Owner);
                pvs = node.NodeChildPhotonView().gameObject.GetComponentsInChildren<PhotonView>();
                
            } else
            {
                pvs = m_grabbedObj.GetComponentsInChildren<PhotonView>();
            }
            foreach (var pv in pvs)
            {
                pv.TransferOwnership(photonView.Owner);
            }

            m_grabbedObj.GrabBegin(this, closestGrabbableCollider);

            m_lastPos = transform.position;
            m_lastRot = transform.rotation;

            // Set up offsets for grabbed object desired position relative to hand.
            if (m_grabbedObj.snapPosition)
            {
                m_grabbedObjectPosOff = m_gripTransform.localPosition;
                if (m_grabbedObj.snapOffset)
                {
                    Vector3 snapOffset = m_grabbedObj.snapOffset.position;
                    if (m_controller == OVRInput.Controller.LTouch) snapOffset.x = -snapOffset.x;
                    m_grabbedObjectPosOff += snapOffset;
                }
            }
            else
            {
                Vector3 relPos = m_grabbedObj.transform.position - transform.position;
                relPos = Quaternion.Inverse(transform.rotation) * relPos;
                m_grabbedObjectPosOff = relPos;
            }

            if (m_grabbedObj.snapOrientation)
            {
                m_grabbedObjectRotOff = m_gripTransform.localRotation;
                if (m_grabbedObj.snapOffset)
                {
                    m_grabbedObjectRotOff = m_grabbedObj.snapOffset.rotation * m_grabbedObjectRotOff;
                }
            }
            else
            {
                Quaternion relOri = Quaternion.Inverse(transform.rotation) * m_grabbedObj.transform.rotation;
                m_grabbedObjectRotOff = relOri;
            }

            // NOTE: force teleport on grab, to avoid high-speed travel to dest which hits a lot of other objects at high
            // speed and sends them flying. The grabbed object may still teleport inside of other objects, but fixing that
            // is beyond the scope of this demo.
            MoveGrabbedObject(m_lastPos, m_lastRot, true);

            // NOTE: This is to get around having to setup collision layers, but in your own project you might
            // choose to remove this line in favor of your own collision layer setup.
            SetPlayerIgnoreCollision(m_grabbedObj.gameObject, true);

            if (m_parentHeldObject)
            {
                m_grabbedObj.transform.parent = transform;
            }
        }
    }
    public override void GrabEnd()
    {   
        linearVelocity = (transform.position - previousPosition) / Time.fixedDeltaTime;            
        Quaternion dif = lastRot * Quaternion.Inverse(transform.rotation);
        float angle;
        Vector3 axis;
        dif.ToAngleAxis(out angle, out axis);
        angularVelocity = axis * angle / Time.deltaTime;
        linearVelocity.Scale(new Vector3(0.7f, 0.7f, 0.7f));

        // Velocity of hand when release object is slow. Considered placing the object rather than throwing.
        if (linearVelocity.x < 0.15f && linearVelocity.y < 0.15f && linearVelocity.z < 0.15f)
        {
            linearVelocity = Vector3.zero;
        } 
        angularVelocity = Vector3.zero;
                               
        GrabbableRelease(linearVelocity, angularVelocity);
        
        GrabVolumeEnable(true); 
    }    
}