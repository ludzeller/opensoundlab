﻿// Copyright © 2017, 2020-2022 Logan Olson, Google LLC, James Surine, Ludwig Zeller, Hannes Barfuss
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

public class SARSCov2DeviceInterface : deviceInterface
{

  SARSCov2Data data;

  public override void Awake()
  {
    base.Awake();
  }

  void Start()
  {

  }


  void Update()
  {

  }

  public override InstrumentData GetData()
  {
    SARSCov2Data data = new SARSCov2Data();
    data.deviceType = menuItem.deviceType.SARSCov2;
    GetTransformData(data);
    
    return data;
  }

  public override void Load(InstrumentData d)
  {
    SARSCov2Data data = d as SARSCov2Data;

    transform.localPosition = data.position;
    transform.localRotation = data.rotation;
    transform.localScale = data.scale;

  }
}

public class SARSCov2Data : InstrumentData
{

}