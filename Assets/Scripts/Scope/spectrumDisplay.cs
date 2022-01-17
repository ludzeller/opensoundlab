// Copyright 2017 Google LLC
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
using System.Diagnostics;
using System;

public class spectrumDisplay : MonoBehaviour
{
  public AudioSource source;
  public FFTWindow fftWin = FFTWindow.BlackmanHarris;
  float[] spectrum;

  int width = 256 * 4;
  int height = 256 * 1;

  RenderTexture offlineTexture;
  Material offlineMaterial;

  Texture2D onlineTexture;
  public Material onlineMaterial;

  bool active = false;

  int drawY = 0;
  int drawX = 0;
  int lastDrawX = 0;
  int lastDrawY = 0;
  public int skip = 1;
  float bandWidth;
  float maxLog;

  void Start()
  {
    spectrum = new float[width];

    bandWidth = 24000f / width;
    maxLog = Mathf.Log10(24000);

    // create material for GL rendering, is this like an actual drawing material? 
    // But colors, etc. can be set below, too... 
    // code doesn't work without this though!
    offlineMaterial = new Material(Shader.Find("GUI/Text Shader"));
    offlineMaterial.hideFlags = HideFlags.HideAndDontSave;
    offlineMaterial.shader.hideFlags = HideFlags.HideAndDontSave;
    // get a RenderTexture and set it as target for offline rendering
    offlineTexture = RenderTexture.GetTemporary(width, height);
    RenderTexture.active = offlineTexture;

    // these are the ones that you will actually see
    onlineTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);
    onlineMaterial = Instantiate(onlineMaterial);
    GetComponent<Renderer>().material = onlineMaterial;

  }

  void Update()
  {

    if (!active) return;

    source.GetSpectrumData(spectrum, 0, fftWin);
    RenderGLToTexture(width, height, offlineMaterial);

  }

  // Via https://forum.unity.com/threads/rendering-gl-to-a-texture2d-immediately-in-unity4.158918/
  void RenderGLToTexture(int width, int height, Material material)
  {

    //Destroy(offlineTexture);

    // get a temporary RenderTexture //
    //offlineTexture = RenderTexture.GetTemporary(width, height);

    // always reset offline render here, in case another script had its finger on it in the meantime
    RenderTexture.active = offlineTexture;

    GL.Clear(false, true, Color.black);

    material.SetPass(0);
    GL.PushMatrix();
    GL.LoadPixelMatrix(0, width, height, 0);
    GL.Begin(GL.LINE_STRIP);
    GL.Color(new Color(1, 1, 1, 1f));

    drawY = 0;
    drawX = 0;
    lastDrawX = 0;
    lastDrawY = 0;

    for (int freqBand = 0; freqBand < spectrum.Length; freqBand++) // skip 0, because of log negative?
    {
      drawX = Mathf.RoundToInt(Utils.map(Mathf.Log10(freqBand * bandWidth), 1f, maxLog, 0f, width));
      if (drawX - lastDrawX <= skip) continue; // skip bands if too close together

      drawY = height - Mathf.RoundToInt(Mathf.Pow(spectrum[freqBand], 0.3f) * height);

      GL.Vertex3(drawX, drawY, 0);

      //p1 = p2;
      lastDrawX = drawX;
      lastDrawY = drawY;
    }

    GL.End();
    GL.PopMatrix();

    // blit/copy the offlineTexture into the onlineTexture (on GPU)
    Graphics.CopyTexture(offlineTexture, onlineTexture);

    // See https://docs.unity3d.com/ScriptReference/RenderTexture.GetTemporary.html
    //RenderTexture.active = null;
    //RenderTexture.ReleaseTemporary(offlineTexture); 
  }


  public void toggleActive(bool on)
  {
    if (active == on) return;
    active = on;
    onlineMaterial.SetTexture(Shader.PropertyToID("_MainTex"), on ? onlineTexture : Texture2D.blackTexture);
  }

}
