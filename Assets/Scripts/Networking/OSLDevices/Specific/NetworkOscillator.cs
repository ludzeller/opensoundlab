using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class NetworkOscillator : NetworkSyncListener
{
    protected signalGenerator signalGenerator;
    protected oscillatorDeviceInterface oscillatorDeviceInterface;
    protected virtual void Awake()
    {
        signalGenerator = GetComponent<signalGenerator>();
        oscillatorDeviceInterface = GetComponent<oscillatorDeviceInterface>();
        oscillatorDeviceInterface.LfoChange += OnLfoChange;
    }
    #region Mirror
    private void OnLfoChange(bool lfo)
    {
        if (lfo)
        {
            OnSync();
        }
    }


    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!isServer)
        {
            CmdRequestSync();
        }
    }

    protected override void OnSync()
    {
        base.OnSync();
        if (isServer)
        {
            RpcUpdatePhase(signalGenerator._phase);
        }
        else
        {
            CmdRequestSync();
        }
    }

    protected override void OnIntervalSync()
    {
        base.OnIntervalSync();
        if (isServer)
        {
            RpcUpdatePhase(signalGenerator._phase);
        }
    }

    [Command(requiresAuthority = false)]
    protected virtual void CmdRequestSync()
    {
        Debug.Log($"{gameObject.name} CmdRequestSync");
        RpcUpdatePhase(signalGenerator._phase);
    }

    [ClientRpc]
    protected virtual void RpcUpdatePhase(double phase)
    {
        if (isClient)
        {
            Debug.Log($"{gameObject.name} old phase: {signalGenerator._phase}, new phase {phase}");
            signalGenerator._phase = phase;
        }
    }
    #endregion
}
