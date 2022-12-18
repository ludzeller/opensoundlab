// Copyright © 2020-2023 GPLv3 Ludwig Zeller OpenSoundLab
// 
// This file is part of OpenSoundLab, which is based on SoundStage VR.
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
// Copyright © 2020 Apache 2.0 Maximilian Maroe SoundStage VR
// Copyright © 2019-2020 Apache 2.0 James Surine SoundStage VR
// Copyright © 2017 Apache 2.0 Google LLC SoundStage VR
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

﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class drumSignalGenerator : signalGenerator
{

    bool newSignal = false;
    bool signalOn = false;

    int hitdur = 100;
    int counter = 0;

    [DllImport("SoundStageNative", EntryPoint = "DrumSignalGenerator")]
    public static extern int DrumSignalGenerator(float[] buffer, int length, int channels, bool signalOn, int counter);

    void Start()
    {
      hitdur = AudioSettings.outputSampleRate / 10;      
    }

    public void setKeyActive(bool on, int ID)
    {
        if (on)
        {
            newSignal = true;
            signalOn = true;
            counter = hitdur;
        }
    }

    public override void processBuffer(float[] buffer, double dspTime, int channels)
    {
        counter = DrumSignalGenerator(buffer, buffer.Length, channels, signalOn, counter);
        if (counter < 0)
            signalOn = false;
        if (newSignal)
        {
            buffer[0] = buffer[1] = -1;
            newSignal = false;
        }
    }
}