using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using Samples;
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

    private void Start()
    {
        
    }

    public void OnSetHandSamplePath(string old, string newString)
    {
        if (old != newString)
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
            tapeInHand.transform.Rotate(-90, 0, 0, Space.Self);
        }
    }


    public void SetHandSamplePath(string path, Vector3 position, Quaternion rotation)
    {
        Debug.Log($"{gameObject.name} Set hand tape: {path}");
        if (isServer)
        {
            offset = position;
            rotationOffset = rotation;
            inHandSamplePath = path;
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
        SetSamplePath(position, rotation);
    }

    private void SetSamplePath(Vector3 position, Quaternion rotation)
    {
        if (isLocalPlayer)
        {
            return;
        }
        if (tapeInHand != null)
        {
            //destroy current tape
            Destroy(tapeInHand.gameObject);
        }
        if (inHandSamplePath.Length > 0)
        {
            //create new instance

            if (!File.Exists(sampleManager.instance.parseFilename(sampleManager.CorrectPathSeparators(inHandSamplePath))))
            {
                Debug.Log("File does't exist");
                return;
            }

            GameObject g = Instantiate(tapePrefab, position, rotation, handParent.transform);
            g.transform.Rotate(-90, 0, 0, Space.Self);
            tapeInHand = g.GetComponent<tape>();
            tapeInHand.Setup(sampleManager.GetFileName(inHandSamplePath), sampleManager.CorrectPathSeparators(inHandSamplePath));
            tapeInHand.TargetNetworkPlayerTape = this;
        }
    }
}
