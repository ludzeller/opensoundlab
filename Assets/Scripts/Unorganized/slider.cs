// Copyright � 2017, 2020-2022 Logan Olson, Google LLC, James Surine, Ludwig Zeller, Hannes Barfuss
//
// This file is part of OpenSoundLab.
//
// OpenSoundLab is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// OpenSoundLab is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with OpenSoundLab.  If not, see <http://www.gnu.org/licenses/>.

using UnityEngine;
using System.Collections;

public class slider : manipObject {
  public float percent = 0f;
  public float xBound = 0.04f;

  public float sliderHue = .44f;

  public Color customColor;
  public Material onMat;
  public Renderer rend;
  Material offMat;
  Material glowMat;
  public bool labelsPresent = true;

  Material[] labels;

  public Color labelColor = new Color(0f, 0.8f, 1f);

  public override void Awake() {
    base.Awake();
    if (rend == null) rend = GetComponent<Renderer>();
    offMat = rend.material;
    customColor = labelColor;

    glowMat = new Material(onMat);
    glowMat.SetFloat("_EmissionGain", .0f);
    glowMat.SetColor("_TintColor", labelColor);

    //if (labelsPresent) {
    //  labels = new Material[3];
    //  labels[0] = transform.parent.Find("Label1").GetComponent<Renderer>().material;
    //  labels[1] = transform.parent.Find("Label2").GetComponent<Renderer>().material;
    //  labels[2] = transform.parent.Find("Label3").GetComponent<Renderer>().material;
      
    //  for (int i = 0; i < 3; i++) {
    //    labels[i].SetColor("_TintColor", labelColor);
    //  }
    //}
    setPercent(percent);
  }

  int lastNotch = -1;
  public override void grabUpdate(Transform t) {
    Vector3 p = transform.localPosition;
    p.x = Mathf.Clamp(transform.parent.InverseTransformPoint(manipulatorObj.position).x + offset, -xBound, xBound);
    transform.localPosition = p;
    updatePercent();

    if (Mathf.FloorToInt(percent / .05f) != lastNotch) {
      if (manipulatorObjScript != null) manipulatorObjScript.hapticPulse(500);
      lastNotch = Mathf.FloorToInt(percent / .05f);
    }
  }

  public void setPercent(float p) {
    Vector3 pos = transform.localPosition;
    pos.x = Mathf.Lerp(-xBound, xBound, p);
    transform.localPosition = pos;
    updatePercent();
  }

  void updatePercent() {
    percent = .5f + transform.localPosition.x / (2 * xBound);

    //if (labelsPresent) {
    //  if (percent <= 0.5f) {
    //    labels[0].SetColor("_TintColor", labelColor * (0.15f + 0.5f - percent) * 2);
    //    labels[1].SetColor("_TintColor", labelColor * (0.15f + percent) * 2);
    //    labels[2].SetColor("_TintColor", labelColor * 0.15f);
    //  } else {
    //    labels[0].SetColor("_TintColor", labelColor * 0.15f);
    //    labels[1].SetColor("_TintColor", labelColor * (1.15f - percent) * 2);
    //    labels[2].SetColor("_TintColor", labelColor * (percent - 0.35f) * 2);
    //  }
    //}
  }

  float offset = 0;

  public override void setState(manipState state) {
    curState = state;
    if (curState == manipState.none) {
      rend.material = offMat;
    } else if (curState == manipState.selected) {
      rend.material = glowMat;
      glowMat.SetFloat("_EmissionGain", .5f);
    } else if (curState == manipState.grabbed) {
      rend.material = glowMat;
      glowMat.SetFloat("_EmissionGain", .7f);
      offset = transform.localPosition.x - transform.parent.InverseTransformPoint(manipulatorObj.position).x;
    }
  }
}