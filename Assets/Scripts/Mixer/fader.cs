// This file is part of OpenSoundLab, which is based on SoundStage VR.
//
// Copyright � 2020-2023 GPLv3 Ludwig Zeller OpenSoundLab
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
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

public class fader : signalGenerator {
  int ID = -1;
  float faderLength = 1;

  public Transform faderBody, faderSliderBar;
  public float mySliderFloat;
  public omniJack inputA, inputB;
  public signalGenerator incomingA, incomingB;
  public bool active = true;
  public slider fadeSlider;
  float lastpercent = 0f;

  float[] bufferB;

  [DllImport("OSLNative")] public static extern void SetArrayToSingleValue(float[] a, int length, float val);
  [DllImport("OSLNative")] public static extern void processFader(float[] buffer, int length, int channels, float[] bufferB, int lengthB, bool aSig, bool bSig, bool samePercent, float lastpercent, float sliderPercent);

  public override void Awake() {
    fadeSlider = GetComponentInChildren<slider>();
    bufferB = new float[MAX_BUFFER_LENGTH];
    SetArrayToSingleValue(bufferB, bufferB.Length, 0.0f);
  }

  void Update() {
    if (incomingA != inputA.signal) incomingA = inputA.signal;
    if (incomingB != inputB.signal) incomingB = inputB.signal;
  }

  public void updateFaderLength(float f) {
    faderLength = f;
    faderBody.localScale = new Vector3(1, 1, faderLength);
    faderSliderBar.localScale = new Vector3(1, 1, faderLength - .25f);

    Vector3 jackPos = new Vector3(.0005f, .001f, faderLength * .14f - .015f);
    inputA.transform.localPosition = jackPos;
    jackPos.z *= -1;
    inputB.transform.localPosition = jackPos;

    //change slider bounds
    fadeSlider.xBound = .095f * faderLength - .025f;

    //move slider
    if (fadeSlider.curState != manipObject.manipState.grabbed) fadeSlider.setPercent(fadeSlider.percent);
  }

  public override void processBuffer(float[] buffer, double dspTime, int channels) {
    if (!active || (incomingA == null && incomingB == null)) {
      SetArrayToSingleValue(buffer, buffer.Length, 0.0f);
      return;
    }

    float sliderPercent = fadeSlider.percent;
    float p = sliderPercent;
    bool samePercent = System.Math.Abs(lastpercent - sliderPercent) < 0.00001;

    if (incomingA != null && incomingB != null) {
      if (bufferB.Length != buffer.Length)
        System.Array.Resize(ref bufferB, buffer.Length);

      SetArrayToSingleValue(bufferB, bufferB.Length, 0.0f);

      incomingA.processBuffer(buffer, dspTime, channels);
      incomingB.processBuffer(bufferB, dspTime, channels);
    } else if (incomingA != null) incomingA.processBuffer(buffer, dspTime, channels);
    else incomingB.processBuffer(buffer, dspTime, channels);

    processFader(buffer, buffer.Length, channels, bufferB, bufferB.Length, incomingA != null, incomingB != null, samePercent, lastpercent, sliderPercent);

    lastpercent = sliderPercent;
  }
}

