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

public class DCDeviceInterface : deviceInterface {
  public omniJack input, output;
  //basicSwitch isBipolar;
  dial attenDial;
  //AudioSource speaker;
  public TextMesh valueDisplay;

  DCSignalGenerator signal;

  public override void Awake() {
    base.Awake();
    //isBipolar = GetComponentInChildren<basicSwitch>();
    attenDial = GetComponentInChildren<dial>();
    //speaker = GetComponentInChildren<AudioSource>();
    signal = GetComponent<DCSignalGenerator>();

    //speaker.volume = signal.incoming == null ? 0f : 1f;
    //attenDial.defaultPercent = isBipolar.switchVal ? 0.5f : 0f;
  }

  void Update() {
    //if(signal.isBipolar != isBipolar.switchVal)
    //{
    //  signal.isBipolar = isBipolar.switchVal;
    //  attenDial.defaultPercent = isBipolar.switchVal ? 0.5f : 0f;
    //}
    
    signal.attenVal = attenDial.percent * 2f - 1f;
    valueDisplay.text = signal.attenVal.ToString("F3");

    if (signal.incoming != input.signal)
    {
      signal.incoming = input.signal;
      //speaker.volume = signal.incoming == null ? 0f : 1f;
    }
  }

  public override InstrumentData GetData() {
    DCData data = new DCData();
    data.deviceType = DeviceType.DC;
    GetTransformData(data);

    //data.isBipolar = isBipolar.switchVal;
    data.dial = attenDial.percent;

    data.jackInID = input.transform.GetInstanceID();
    data.jackOutID = output.transform.GetInstanceID();

    return data;
  }

  public override void Load(InstrumentData d, bool copyMode) {
    DCData data = d as DCData;
    base.Load(data, true);

    input.SetID(data.jackInID, copyMode);
    output.SetID(data.jackOutID, copyMode);

    //isBipolar.setSwitch(data.isBipolar, true);
    attenDial.setPercent(data.dial);

  }
}

public class DCData : InstrumentData {
  //public bool isBipolar;
  public float dial;

  public int jackOutID;
  public int jackInID;
}
