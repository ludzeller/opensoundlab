using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using System.IO;
using UnityEngine;

public class NetworkPlayerTapeHand : NetworkBehaviour
{
    public GameObject handParent;
    [SyncVar(hook = nameof(OnSetHandSamplePath))]
    public string inHandSamplePath;
    [SyncVar(hook = nameof(OnChangeOffset))]
    private Vector3 offset;
    [SyncVar(hook = nameof(OnChangeRotationOffset))]
    private Quaternion rotationOffset;
    private tape networkedTapeInHand;
    private tape tapeInHand;

    public GameObject tapePrefab;

    public tape TapeInHand { get => tapeInHand; set => tapeInHand = value; }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!isServer && inHandSamplePath.Length > 0)
        {
            SetSamplePath(offset, rotationOffset);
        }
    }

    public void OnSetHandSamplePath(string old, string newString)
    {
        if (old != newString && !isServer)
        {
            Debug.Log($"{gameObject.name} On Set hand tape: {newString}");

            SetSamplePath(offset, rotationOffset);
        }
    }


    public void OnChangeOffset(Vector3 old, Vector3 newValue)
    {
        if (networkedTapeInHand != null && !isLocalPlayer)
        {
            networkedTapeInHand.transform.localPosition = newValue;
        }
    }
    public void OnChangeRotationOffset(Quaternion old, Quaternion newValue)
    {
        if (networkedTapeInHand != null && !isLocalPlayer)
        {
            networkedTapeInHand.transform.localRotation = newValue;
        }
    }


    public void SetHandSamplePath(string path, Vector3 position, Quaternion rotation)
    {
        Debug.Log($"{gameObject.name} Set hand tape: {path}, {position}, {rotation}");
        if (isServer)
        {
            offset = position;
            rotationOffset = rotation;
            inHandSamplePath = path;
            SetSamplePath(position, rotation);
        }
        else
        {
            CmdUpdateSamplePath(path, position, rotation);
        }
    }

    [Command]
    public void CmdUpdateSamplePath(string path, Vector3 position, Quaternion rotation)
    {
        inHandSamplePath = path;
        rotationOffset = rotation;
        SetSamplePath(position, rotation);
    }

    private void SetSamplePath(Vector3 position, Quaternion rotation)
    {
        if (networkedTapeInHand != null)
        {
            Destroy(networkedTapeInHand.gameObject);
            Debug.Log($"Destroy grabbed tape of {handParent}");
        }
        if (!isLocalPlayer && inHandSamplePath.Length > 0)
        {
            //create new instance

            if (!File.Exists(sampleManager.instance.parseFilename(sampleManager.CorrectPathSeparators(inHandSamplePath))))
            {
                Debug.Log("File doesn't exist");
                return;
            }

            Debug.Log($"Change sample path of {handParent} to {inHandSamplePath}, {position}, {rotation}");

            GameObject g = Instantiate(tapePrefab, handParent.transform);
            g.transform.localPosition = position;
            g.transform.localRotation = rotation;
            networkedTapeInHand = g.GetComponent<tape>();
            networkedTapeInHand.Setup(sampleManager.GetFileName(inHandSamplePath), sampleManager.CorrectPathSeparators(inHandSamplePath));
            networkedTapeInHand.TargetNetworkTapeHand = this;
            networkedTapeInHand.masterObj = null;
        }
    }



    public void PassToOtherHand()
    {
        Debug.Log($"{gameObject.name} passed to other hand");
        if (isServer)
        {
            inHandSamplePath = "";
            networkedTapeInHand = null;
            RpcDeleteGrabbedTapeInHand();

        }
        else
        {
            networkedTapeInHand = null;
            CmdPassToOtherPlayer();
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdPassToOtherPlayer()
    {
        Debug.Log($"Cmd {gameObject.name} passed to other hand");

        inHandSamplePath = "";
        if (networkedTapeInHand != null)
        {
            Destroy(networkedTapeInHand.gameObject);
        }
        RpcDeleteGrabbedTapeInHand();
    }

    [ClientRpc]
    public void RpcDeleteGrabbedTapeInHand()
    {
        Debug.Log($"RPC {gameObject.name} delet grabbed object");
        if (tapeInHand != null)
        {
            //tapeInHand.setGrab(false, handParent.transform);
            Destroy(tapeInHand.gameObject);
        }
    }
}
