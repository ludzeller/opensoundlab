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

public class scopeSignalGenerator : signalGenerator
{

  public signalGenerator incoming;
  float[] bufferCopy; // for FFT
  waveViz displayOsc; // for OSC
  spectrumDisplay displayFft;
  public bool isMuted = false;

  [DllImport("OSLNative")]
  public static extern void CopyArray(float[] a, float[] b, int length);
  [DllImport("OSLNative")]
  public static extern void SetArrayToSingleValue(float[] a, int length, float val);


  public override void Awake()
  {
    base.Awake();
    displayOsc = GetComponentInChildren<waveViz>();
    displayFft = GetComponentInChildren<spectrumDisplay>();

    bufferCopy = new float[MAX_BUFFER_LENGTH];
  }

  // needed for scope, feeds it in AudioSource on Scope for FFT
  private void OnAudioFilterRead(float[] buffer, int channels)
  {
    if (incoming == null || bufferCopy == null) return;
    CopyArray(bufferCopy, buffer, buffer.Length);
  }

  public override void processBufferImpl(float[] buffer, double dspTime, int channels)
  {
    if (!recursionCheckPre()) return; // checks and avoids fatal recursions
    if (bufferCopy.Length != buffer.Length)
      System.Array.Resize(ref bufferCopy, buffer.Length);

    // if silent, 0 out and return
    if (!incoming)
    {
      SetArrayToSingleValue(buffer, buffer.Length, 0.0f);
      SetArrayToSingleValue(bufferCopy, bufferCopy.Length, 0.0f);
      return;
    }

    if (incoming != null){
      incoming.processBuffer(buffer, dspTime, channels);
    }

    /*if(displayFft.gameObject.activeSelf) */CopyArray(buffer, bufferCopy, buffer.Length); // copy over for FFT
    /*if(displayOsc.gameObject.activeSelf) */displayOsc.storeBuffer(buffer, channels); // copy over for OSC
    
    if(isMuted){
      SetArrayToSingleValue(buffer, buffer.Length, 0.0f); // clear after usage before passing on. todo: this could be made more efficient and click-free
    }
    recursionCheckPost();
  }
}
