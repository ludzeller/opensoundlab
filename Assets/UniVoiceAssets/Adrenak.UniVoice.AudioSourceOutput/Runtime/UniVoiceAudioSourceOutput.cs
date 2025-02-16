﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Adrenak.UniVoice.AudioSourceOutput
{
    /// <summary>
    /// This class feeds incoming mono audio segments directly to a stereo AudioSource via OnAudioFilterRead.
    /// It manages playback synchronization, handles missing segments, and implements smooth crossfades during catchup.
    /// </summary>

    public class UniVoiceAudioSourceOutput : MonoBehaviour, IAudioOutput
    {
        private const string TAG = "UniVoiceAudioSourceOutput";

        /// <summary>
        /// Thread-safe collection to store incoming audio segments.
        /// Key: Absolute segment index.
        /// Value: Mono audio samples.
        /// </summary>
        private ConcurrentDictionary<int, float[]> segments = new ConcurrentDictionary<int, float[]>();

        /// <summary>
        /// Queue to manage the order of segments for playback.
        /// </summary>
        private Queue<float[]> playbackQueue = new Queue<float[]>();

        /// <summary>
        /// Current segment playback tracking.
        /// </summary>
        private int playbackSegmentSampleIndex = 0;

        /// <summary>
        /// Next expected segment index.
        /// </summary>
        private int nextSegmentIndex = 0;

        /// <summary>
        /// Audio settings.
        /// </summary>
        private int frequency;
        private int channelCount;
        private int segmentLengthInSamples;

        /// <summary>
        /// Buffer thresholds.
        /// </summary>
        public int MinSegCount { get; set; }
        public int MaxSegCount { get; set; }

        /// <summary>
        /// Synchronization lock for thread safety.
        /// </summary>
        private object playLock = new object();

        /// <summary>
        /// AudioSource component.
        /// </summary>
        public AudioSource audioSource;

        /// <summary>
        /// Identifier for the audio source.
        /// </summary>
        public string ID
        {
            get => audioSource.name;
            set
            {
                gameObject.name = "UniVoice Peer #" + value;
                audioSource.name = "UniVoice Peer #" + value;
            }
        }

        /// <summary>
        /// Thread-safe queue for actions to be executed on the main thread.
        /// </summary>
        private ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

        private string outputMixer = "MIXER";
        private string outputMixerGroup = "univoice";

        /// <summary>
        /// Initializes the UniVoiceAudioSourceOutput.
        /// </summary>
        /// <param name="frequency">Sampling rate of the audio.</param>
        /// <param name="channelCount">Number of audio channels (fixed to 2 for stereo).</param>
        /// <param name="segmentLengthInSamples">Number of samples per mono segment.</param>
        /// <param name="minSegCount">Minimum buffer segments required to start playback.</param>
        /// <param name="maxSegCount">Maximum buffer segments before initiating catchup.</param>
        public void Initialize(int frequency, int channelCount, int segmentLengthInSamples, int minSegCount = 0, int maxSegCount = 20)
        {
            this.frequency = frequency;
            this.channelCount = channelCount;
            this.segmentLengthInSamples = segmentLengthInSamples;

            MinSegCount = Mathf.Clamp(minSegCount, 0, maxSegCount);
            MaxSegCount = Mathf.Clamp(maxSegCount, MinSegCount + 1, maxSegCount);

            // Ensure AudioSource is present
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            AudioMixer audioMixer = Resources.Load<AudioMixer>(outputMixer);
            AudioMixerGroup[] mixerGroups = audioMixer.FindMatchingGroups(string.Empty);

            foreach (AudioMixerGroup group in mixerGroups)
            {
                if (group.name == outputMixerGroup)
                {
                    audioSource.outputAudioMixerGroup = group;
                    break;
                }
            }

            // Configure AudioSource settings
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialize = true; // Please note that the AudioSource must be after this script in order for the spatialization to work
            audioSource.spatialBlend = 1f;
            audioSource.mute = false;

            Debug.unityLogger.Log(TAG, $"Initialized with frequency: {frequency}, channels: {this.channelCount}, segmentLength: {segmentLengthInSamples}, MinSegCount: {MinSegCount}, MaxSegCount: {MaxSegCount}");
        }

        /// <summary>
        /// Feeds incoming mono audio into the playback system.
        /// </summary>
        /// <param name="index">Absolute index of the segment.</param>
        /// <param name="frequency">Sampling rate of the audio.</param>
        /// <param name="channelCount">Number of audio channels (expected to be 1 for mono).</param>
        /// <param name="audioSamples">Mono audio samples being fed.</param>
        public void Feed(int index, int frequency, int channelCount, float[] audioSamples)
        {
            if (audioSamples.Length != segmentLengthInSamples)
            {
                Debug.unityLogger.LogWarning(TAG, $"Incorrect segment length: {audioSamples.Length}. Expected: {segmentLengthInSamples}");
                return;
            }

            // Ensure audio settings match
            if (this.frequency != frequency)
            {
                Debug.unityLogger.LogError(TAG, $"Frequency mismatch. Expected: {this.frequency}, Received: {frequency}");
                return;
            }

            // Expecting mono segments
            if (channelCount != 1)
            {
                Debug.unityLogger.LogWarning(TAG, $"Channel count for segments is {channelCount}. Expected: 1 (mono). Proceeding by treating as mono.");
            }

            if (index < nextSegmentIndex)
            {
                Debug.unityLogger.Log(TAG, $"Discarding old segment {index} as it is older than the acceptable threshold.");
                return;
            }

            // Avoid overwriting existing segments
            if (segments.TryAdd(index, audioSamples))
            {
                // Segment added successfully
            }
            else
            {
                Debug.unityLogger.LogWarning(TAG, $"Segment {index} already exists. Ignoring duplicate.");
            }
        }

        /// <summary>
        /// Feeds an incoming ChatroomAudioSegment into the audio buffer.
        /// </summary>
        /// <param name="segment">The audio segment to feed.</param>
        public void Feed(ChatroomAudioSegment segment) =>
            Feed(segment.segmentIndex, segment.frequency, segment.channelCount, segment.samples);

        // 16khz to 48khz
        public int UpsampleFactor = 3;
        public int readyCount = 0;
        public bool fillingUp = false;

        // bringing all local variables of OnAudioFilterRead to class scope in order to avoid garbage collection
        int samplesPerChannel;
        List<int> keysToRemove;
        int targetCount;
        int segmentsToRemove;
        int totalAvailableSamples;
        float[] currentSegment;
        int minKey;
        int outputBufferSampleIndex;

        private void OnAudioFilterRead(float[] outputBuffer, int channels)
        {
            lock (playLock)
            {
                samplesPerChannel = outputBuffer.Length / channels;

                // Determine buffer state
                readyCount = segments.Count;

                if (readyCount <= 1)
                {
                    // Insufficient buffer, log and wait until enough segments are ready
                    EnqueueMainThreadAction(() =>
                    {
                        Debug.unityLogger.Log(TAG, "Insufficient segments available: " + readyCount);
                    });

                    fillingUp = true;
                    return;
                }

                if (fillingUp)
                {
                    if (readyCount >= MinSegCount)
                    {
                        fillingUp = false; // enough segments gathered
                    }
                    else
                    {
                        return; // wait longer
                    }
                }

                // Remove segments that are too old
                keysToRemove = new List<int>();
                foreach (var k in segments.Keys)
                {
                    if (k < nextSegmentIndex)
                        keysToRemove.Add(k);
                }
                keysToRemove.Sort();

                foreach (int key in keysToRemove)
                {
                    if (segments.TryRemove(key, out _))
                    {
                        Debug.unityLogger.Log(TAG, $"Disposed old segment {key} to maintain latency constraints.");
                    }
                    else
                    {
                        Debug.unityLogger.LogWarning(TAG, $"Failed to dispose old segment {key}.");
                    }
                }

                // Dispose segments so that segments count is going down to MinSegCount + (MaxSegCount - MinSegCount) / 2
                if (readyCount > MaxSegCount)
                {
                    // Calculate the target number of segments
                    targetCount = MinSegCount + (MaxSegCount - MinSegCount) / 2;
                    segmentsToRemove = readyCount - targetCount;

                    if (segmentsToRemove > 0)
                    {
                        var allKeys = new List<int>(segments.Keys);
                        allKeys.Sort();
                        keysToRemove = allKeys.GetRange(0, segmentsToRemove);

                        foreach (var key in keysToRemove)
                        {
                            if (segments.TryRemove(key, out _))
                            {
                                Debug.unityLogger.Log(TAG, $"Disposed segment {key} to reduce buffer.");
                            }
                            else
                            {
                                Debug.unityLogger.LogWarning(TAG, $"Failed to dispose segment {key}.");
                            }
                        }

                        // Optionally, log the new segment count
                        EnqueueMainThreadAction(() =>
                        {
                            Debug.unityLogger.Log(TAG, $"Disposed {segmentsToRemove} segments. New segment count: {segments.Count}");
                        });
                    }
                }

                // Compute total available samples in playbackQueue
                totalAvailableSamples = 0;

                if (playbackQueue.Count > 0)
                {
                    currentSegment = playbackQueue.Peek();
                    totalAvailableSamples += (currentSegment.Length - playbackSegmentSampleIndex) * UpsampleFactor;
                }

                if (playbackQueue.Count > 1)
                {
                    // We need to sum samples of all segments except the first one already accounted above
                    // Convert playbackQueue to array to access beyond the first element
                    float[][] queueArray = playbackQueue.ToArray();
                    for (int i = 1; i < queueArray.Length; i++)
                    {
                        totalAvailableSamples += queueArray[i].Length * UpsampleFactor;
                    }
                }

                // Fill playbackQueue with enough segments to fill data
                while (totalAvailableSamples < samplesPerChannel && !segments.IsEmpty)
                {
                    // Find the smallest available segment index
                    minKey = int.MaxValue;
                    foreach (var k in segments.Keys)
                    {
                        if (k < minKey)
                            minKey = k;
                    }

                    if (minKey == int.MaxValue) // no segments
                        break;

                    if (segments.TryRemove(minKey, out float[] segment))
                    {
                        playbackQueue.Enqueue(segment);
                        totalAvailableSamples += segment.Length * UpsampleFactor;

                        // Update nextSegmentIndex
                        nextSegmentIndex = Math.Max(nextSegmentIndex, minKey + 1);
                    }
                    else
                    {
                        // If removal failed, possibly due to race conditions, break the loop
                        break;
                    }
                }

                // Initialize output sample index
                outputBufferSampleIndex = 0;

                // Finish upsample run if not finished during last buffer run
                if (up != 0)
                {
                    // currentSample and nextSample stay the same from the last buffer run
                    UpsampleSegmentLinear(currentSample, nextSample, UpsampleFactor, outputBuffer, ref outputBufferSampleIndex, samplesPerChannel, channels);
                }

                // fill up the 
                while (outputBufferSampleIndex < samplesPerChannel && playbackQueue.Count > 0)
                {
                    float[] currentSegment = playbackQueue.Peek();

                    if (playbackSegmentSampleIndex >= currentSegment.Length)
                    {
                        // Current segment exhausted, dequeue it
                        playbackQueue.Dequeue();
                        playbackSegmentSampleIndex = 0;
                        continue;
                    }

                    // Get current sample
                    currentSample = currentSegment[playbackSegmentSampleIndex];

                    // Get next sample
                    nextSample = currentSample; // Default to current sample

                    if (playbackSegmentSampleIndex + 1 < currentSegment.Length)
                    {
                        nextSample = currentSegment[playbackSegmentSampleIndex + 1];
                    }
                    else
                    {
                        // At the end of current segment, try to get the first sample of the next segment
                        if (playbackQueue.Count > 1)
                        {
                            float[][] queueArray = playbackQueue.ToArray();
                            if (queueArray.Length > 1 && queueArray[1].Length > 0)
                            {
                                nextSample = queueArray[1][0];
                            }
                        }
                    }

                    // Use the UpsampleSegmentLinear function
                    UpsampleSegmentLinear(currentSample, nextSample, UpsampleFactor, outputBuffer, ref outputBufferSampleIndex, samplesPerChannel, channels);

                    playbackSegmentSampleIndex++;
                }
            }
        }

        int up = 0;
        float currentSample;
        float nextSample;

        private void UpsampleSegmentLinear(float currentSample, float nextSample, int upsampleFactor, float[] outputData, ref int outputSampleIndex, int samplesPerChannel, int channels)
        {
            for (; up < upsampleFactor && outputSampleIndex < samplesPerChannel; up++)
            {
                float t = (float)up / upsampleFactor;
                float interpolatedSample = Mathf.Lerp(currentSample, nextSample, t);

                for (int ch = 0; ch < channels; ch++)
                {
                    outputData[outputSampleIndex * channels + ch] = interpolatedSample;
                }
                outputSampleIndex++;
            }
            if (up == upsampleFactor) up = 0; // the loop was able to do a full upsample run, prepare for next run by resetting to 0
        }



        /// <summary>
        /// Disposes the instance by deleting the GameObject of the component.
        /// </summary>
        public void Dispose()
        {
            // Ensure disposal is done on the main thread
            EnqueueMainThreadAction(() => Destroy(gameObject));
        }

        /// <summary>
        /// Factory class for creating UniVoiceAudioSourceOutput instances.
        /// </summary>
        public class Factory : IAudioOutputFactory
        {
            public int MinSegCount { get; private set; }
            public int MaxSegCount { get; private set; }

            public Factory() : this(5, 15) { }

            public Factory(int minSegCount, int maxSegCount)
            {

                MinSegCount = minSegCount;
                MaxSegCount = maxSegCount;
            }

            public IAudioOutput Create(int samplingRate, int channelCount, int segmentLengthInSamples)
            {
                var go = new GameObject($"UniVoiceAudioSourceOutput");
                var output = go.AddComponent<UniVoiceAudioSourceOutput>();
                output.Initialize(samplingRate, channelCount, segmentLengthInSamples, MinSegCount, MaxSegCount);
                return output;
            }
        }

        /// <summary>
        /// Enqueues an action to be executed on the main thread.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        private void EnqueueMainThreadAction(Action action)
        {
            mainThreadActions.Enqueue(action);
        }

        /// <summary>
        /// Executes all enqueued actions on the main thread.
        /// </summary>
        private void Update()
        {
            while (mainThreadActions.TryDequeue(out Action action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.unityLogger.LogError(TAG, $"Error executing main thread action: {ex}");
                }
            }
        }
    }
}
