using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using System.IO;
using UnityEngine;

public class NetworkPlayerTape : NetworkBehaviour
{
    public GameObject handParent;
    [SyncVar(hook = nameof(OnSetHandSamplePath))]
    public string inHandSamplePath;
    [SyncVar(hook = nameof(OnChangeOffset))]
    private Vector3 offset;
    [SyncVar(hook = nameof(OnChangeRotationOffset))]
    private Quaternion rotationOffset;
    private tape tapeInHand;

    public GameObject tapePrefab;

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
        if (tapeInHand != null && !isLocalPlayer)
        {
            tapeInHand.transform.localPosition = newValue;
        }
    }
    public void OnChangeRotationOffset(Quaternion old, Quaternion newValue)
    {
        if (tapeInHand != null && !isLocalPlayer)
        {
            tapeInHand.transform.localRotation = newValue;
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
        inHandSamplePath = path;
        SetSamplePath(position, rotation);
    }

    private void SetSamplePath(Vector3 position, Quaternion rotation)
    {
        if (tapeInHand != null)
        {
            //destroy current tape
            if (isLocalPlayer)
            {
                tapeInHand.setGrab(false, null);
                Debug.Log($"release grabed tape of {handParent}");
            }
            if (!isLocalPlayer)
            {
                Destroy(tapeInHand.gameObject);
                Debug.Log($"Destroy grabed tape of {handParent}");
            }
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
            tapeInHand = g.GetComponent<tape>();
            tapeInHand.Setup(sampleManager.GetFileName(inHandSamplePath), sampleManager.CorrectPathSeparators(inHandSamplePath));
            tapeInHand.TargetNetworkPlayerTape = this;
        }
    }



    public void PassToOtherHand()
    {
        Debug.Log($"{gameObject.name} passed to other hand:");
        if (isServer)
        {
            inHandSamplePath = "";
            tapeInHand = null;
        }
        else
        {
            tapeInHand = null;
            CmdPassToOtherPlayer();
        }
    }

    [Command]
    public void CmdPassToOtherPlayer()
    {
        inHandSamplePath = "";
        if (tapeInHand != null)
        {
            Destroy(tapeInHand.gameObject);
        }
    }
}
