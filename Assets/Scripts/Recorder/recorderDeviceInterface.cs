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

public class recorderDeviceInterface : deviceInterface {
  public omniJack input, output, recordTrigger, playTrigger, backTrigger;
  public sliderNotched durSlider;
  waveTranscribeRecorder transcriber;
  public basicSwitch normalizeSwitch;
  public button[] buttons;
  public AudioSource babySpeakerRim;

  int[] durations = new int[] { 300, 150, 60, 30, 10 };//{ 10,30,60,150,300 };

  public override void Awake() {
    base.Awake();
    transcriber = GetComponent<waveTranscribeRecorder>();
    //durSlider = GetComponentInChildren<sliderNotched>();
  }

  void Update() {
    if (input.signal != transcriber.incoming) transcriber.incoming = input.signal;
    if (durations[durSlider.switchVal] != transcriber.duration) {
      transcriber.updateDuration(durations[durSlider.switchVal]);
    }
  }

  public override void hit(bool on, int ID = -1) {
    if (ID == 2 && on) transcriber.Back();
    if (ID == 3 && on) transcriber.Save();
    if (ID == 4 && on) transcriber.Flush();

    if (ID == 0) {
      if (on) buttons[1].keyHit(true);
      transcriber.recording = on;
    }
    if (ID == 1) {
      if (!on) buttons[0].keyHit(false);
      transcriber.playing = on;
    }
    if(ID == 5){
      babySpeakerRim.mute = on;
    }
  }

  public override InstrumentData GetData() {
    RecorderData data = new RecorderData();
    data.deviceType = DeviceType.Recorder;
    GetTransformData(data);

    data.jackInID = input.transform.GetInstanceID();
    data.jackOutID = output.transform.GetInstanceID();
    data.recordTriggerID = recordTrigger.transform.GetInstanceID();
    data.playTriggerID = playTrigger.transform.GetInstanceID();
    data.backTriggerID = backTrigger.transform.GetInstanceID();
    data.dur = durSlider.switchVal;
    data.normalize = normalizeSwitch.switchVal;
    data.mutedState = buttons[5].isHit;
    return data;
  }

  public override void Load(InstrumentData d, bool copyMode) {
    RecorderData data = d as RecorderData;
    base.Load(data, copyMode);
    input.SetID(data.jackInID, copyMode);
    output.SetID(data.jackOutID, copyMode);
    recordTrigger.SetID(data.recordTriggerID, copyMode);
    playTrigger.SetID(data.playTriggerID, copyMode);
    backTrigger.SetID(data.backTriggerID, copyMode);
    durSlider.setVal(data.dur);
    normalizeSwitch.setSwitch(data.normalize, true);
    buttons[5].setOnAtStart(data.mutedState);
  }
}

public class RecorderData : InstrumentData {
  public int jackOutID;
  public int jackInID;
  public int recordTriggerID;
  public int playTriggerID;
  public int backTriggerID;
  public int dur;
  public bool normalize;
  public bool mutedState;
  public string audioFilename;
}