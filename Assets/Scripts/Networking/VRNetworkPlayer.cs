using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class VRNetworkPlayer : NetworkBehaviour
{
    [SerializeField]private Transform networkHead;
    [SerializeField] private Transform networkLeftHand;
    [SerializeField] private Transform networkRightHand;

    private Transform localPlayerHead;
    private Transform localPlayerRightHand;
    private Transform localPlayerLeftHand;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        var ovrCameraRig = FindAnyObjectByType<OVRCameraRig>();
        if (ovrCameraRig != null)
        {
            localPlayerHead = ovrCameraRig.centerEyeAnchor;
            localPlayerRightHand = ovrCameraRig.rightHandAnchor;
            localPlayerLeftHand = ovrCameraRig.leftHandAnchor;
        }

        if (NetworkMenuManager.Instance != null)
        {
            NetworkMenuManager.Instance.localPlayer = this;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (isLocalPlayer)
        {
            networkHead.position = localPlayerHead.position;
            networkHead.rotation = localPlayerHead.rotation;

            networkLeftHand.position = localPlayerLeftHand.position;
            networkLeftHand.rotation = localPlayerLeftHand.rotation;

            networkRightHand.position = localPlayerRightHand.position;
            networkRightHand.rotation = localPlayerRightHand.rotation;
        }
    }
}
