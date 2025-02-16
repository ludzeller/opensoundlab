// This file is part of OpenSoundLab, which is based on SoundStage VR.
//
// Copyright � 2020-2024 OSLLv1 Spherical Labs OpenSoundLab
// 
// OpenSoundLab is licensed under the OpenSoundLab License Agreement (OSLLv1).
// You may obtain a copy of the License at 
// https://github.com/SphericalLabs/OpenSoundLab/LICENSE-OSLLv1.md
// 
// By using, modifying, or distributing this software, you agree to be bound by the terms of the license.
// 
//
// Copyright � 2020 Apache 2.0 Maximilian Maroe SoundStage VR
// Copyright � 2019-2020 Apache 2.0 James Surine SoundStage VR
// Copyright � 2017 Apache 2.0 Google LLC SoundStage VR
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;
using System;

public class freeverbSignalGenerator : signalGenerator {

    public enum Param : int
    {
        P_ROOMSIZE,
        P_DAMPING,
        P_DRY,
        P_WET,
        P_WIDTH,
        P_FREEZE,
        P_N
    };

    /* These mirror some of the Freeverb parameters, therefore the C++ style naming. 
     If you change these, please also make the corresponding change in tuning.h of freeverb sources.
    Otherwise behavior is undefined.
     */
    const float kRoomSizeMin = 0.7f;
    const float kRoomSizeMax = kRoomSizeMin + 0.299f; //HB edit

    const float MIN_WET = 0;
    const float MAX_WET = 1;
    const float MIN_DRY = 0;
    const float MAX_DRY = 1;

    public signalGenerator sigIn, sigModSize, sigModFreeze, sigModMix;

    private float[] modSizeBuffer = new float[0];
    private float[] modFreezeBuffer = new float[0];
    private float[] modMixBuffer = new float[0];

    private IntPtr x;
    private float[] p = new float[(int)Param.P_N];
    [DllImport("OSLNative")]
    private static extern void Freeverb_Process(float[] buffer, int length, int channels, IntPtr x);
    [DllImport("OSLNative")]
    private static extern IntPtr Freeverb_New(int sampleRate);
    [DllImport("OSLNative")]
    private static extern void Freeverb_Free(IntPtr x);
    [DllImport("OSLNative")]
    private static extern void Freeverb_SetParam(int param, float value, IntPtr x);
    [DllImport("OSLNative")]
    private static extern float Freeverb_GetParam(int param, IntPtr x);
    [DllImport("OSLNative")]
    public static extern void SetArrayToSingleValue(float[] a, int length, float val);

    public override void Awake() {
        base.Awake();
        x = Freeverb_New(AudioSettings.outputSampleRate);
    }

    private void OnDestroy()
    {
        Freeverb_Free(x);
    }

    void Update() {
    
    }

    public void SetParam(float value, int param)
    {
        switch (param)
        {
            case (int)Param.P_ROOMSIZE:
                p[param] = Utils.map(value, 0, 1, kRoomSizeMin, kRoomSizeMax);
                //Debug.Log("Set param " + param + " to value " + p[param]);
                break;
            case (int)Param.P_DAMPING:
                p[param] = value;
                break;
            case (int)Param.P_WET:
                p[param] = Utils.map(value, 0, 1, MIN_WET, MAX_WET, 0.3f);
                break;
            case (int)Param.P_DRY:
                p[param] = Utils.map(value, 0, 1, MIN_DRY, MAX_DRY, 0.3f);
                //Debug.Log("Set param " + param + " to value " + p[param]);
                break;
            case (int)Param.P_WIDTH:
                p[param] = value;
                break;
            case (int)Param.P_FREEZE:
                p[param] = value == 0 ? 1 : 0; //in this case, we want "on" to be "off" and vice versa :)
                break;
        }
        //Debug.Log("Set param " + param + " to value " + p[param]);
    }

    public override void processBufferImpl(float[] buffer, double dspTime, int channels) {
        if (!recursionCheckPre()) return; // checks and avoids fatal recursions

        if (modSizeBuffer.Length != buffer.Length)
          System.Array.Resize(ref modSizeBuffer, buffer.Length);
        if (modFreezeBuffer.Length != buffer.Length)
          System.Array.Resize(ref modFreezeBuffer, buffer.Length);
        if (modMixBuffer.Length != buffer.Length)
          System.Array.Resize(ref modMixBuffer, buffer.Length);

        SetArrayToSingleValue(modSizeBuffer, modSizeBuffer.Length, 0f);
        SetArrayToSingleValue(modFreezeBuffer, modFreezeBuffer.Length, 0f);
        SetArrayToSingleValue(modMixBuffer, modMixBuffer.Length, 0f);

        //Process mod inputs
        if (sigModSize != null)
        {
            if (modSizeBuffer == null)
                modSizeBuffer = new float[buffer.Length];
            
            sigModSize.processBuffer(modSizeBuffer, dspTime, channels);
            p[(int)Param.P_ROOMSIZE] += modSizeBuffer[0]; //Add mod value to dial value - mod value is in range [-1...1]
            p[(int)Param.P_ROOMSIZE] = Mathf.Clamp01(p[(int)Param.P_ROOMSIZE]);
        }

        if (sigModFreeze != null)
        {
            if (modFreezeBuffer == null)
                modFreezeBuffer = new float[buffer.Length];
            
            sigModFreeze.processBuffer(modFreezeBuffer, dspTime, channels);
            p[(int)Param.P_FREEZE] = modFreezeBuffer[0] > 0 ? 1 : 0;
        }

        if (sigModMix != null)
        {
            if (modMixBuffer == null)
                modMixBuffer = new float[buffer.Length];
            
            sigModMix.processBuffer(modMixBuffer, dspTime, channels);
            float tmp = Mathf.Pow(p[(int)Param.P_WET], 2) + modMixBuffer[0]; //Add mod value to dial value - mod value is in range [-1...1]
            tmp = Mathf.Clamp01(tmp);
            //TODO: not sure if sqrt crossfade is the best solution here; the signals obviously do have SOME correlation...
            p[(int)Param.P_WET] = Utils.equalPowerCrossfadeGain(tmp);
            p[(int)Param.P_DRY] = Utils.equalPowerCrossfadeGain(1 - tmp);
        }

        //Set new parameters
        for (int i = 0; i < (int)Param.P_N; i++)
        {
            Freeverb_SetParam(i, p[i], x);
        }

        if (sigIn != null)
            sigIn.processBuffer(buffer, dspTime, channels);
        //We process the reverb even if there is no input available, to make sure reverb tails decay to the end, and to enable spacy freeze pads.
        Freeverb_Process(buffer, buffer.Length, channels, x);
        recursionCheckPost();
    }
}
