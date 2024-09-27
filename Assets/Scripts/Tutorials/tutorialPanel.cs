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

public class tutorialPanel : manipObject {

  public TextMesh label;

  public Renderer panelRenderer;

  public Material normalMat, selectedMat, activeMat;

  public tutorialsDeviceInterface tutorialsManager;

  public bool isActive = false;
  public string videoString = "";
  
  public override void Awake() {
    base.Awake();    
    if (transform.parent.parent) tutorialsManager = transform.parent.parent.GetComponent<tutorialsDeviceInterface>();
  }

  public void setLabel(string labelString){
    label.text = labelString;
  }

  public void setVideo(string str)
  {
    videoString = str;
  }

  public void setActivated(bool active, bool notifyManager = true){
    if (active && !isActive && notifyManager){ // freshly activated, tell the manager once
      tutorialsManager.triggerOpenTutorial(this);
    }
    isActive = active;
    panelRenderer.sharedMaterial = isActive ? activeMat : normalMat;
  }


  public override void setState(manipState state) {
    if(state == manipState.none && !isActive){
      panelRenderer.sharedMaterial = normalMat;
    } else if(state == manipState.selected && !isActive){
      panelRenderer.sharedMaterial = selectedMat;
    } else if(state == manipState.grabbed){
      setActivated(true);
      manipulatorObjScript.hapticPulse(700);
    }

  }


  public override void onTouch(bool on, manipulator m)
  {
    if (m != null)
    {
      if (m.emptyGrab)
      {
        setActivated(true);
        m.hapticPulse(2000);
      }
    }
  }
}
