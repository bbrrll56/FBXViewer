using System.Collections;
using System.IO;
using UnityEngine;

namespace FBXViewer.Viewer
{
    /// <summary>
    /// Loads and plays local WAV files.
    /// </summary>
    public class WAVPlayer : MonoBehaviour
    {
        private AudioSource audioSource;
        private Coroutine playbackCoroutine;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        public void PlayWAV(string filePath)
        {
            StopPlayback();

            if (audioSource == null)
            {
                Debug.LogError("[WAVPlayer] AudioSource was not found.");
                return;
            }

            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[WAVPlayer] WAV file was not found: {filePath}");
                return;
            }

            playbackCoroutine = StartCoroutine(LoadAndPlayWAV(filePath));
        }

        public IEnumerator PlayWAVAndWait(string filePath)
        {
            StopPlayback();

            if (audioSource == null)
            {
                Debug.LogError("[WAVPlayer] AudioSource was not found.");
                yield break;
            }

            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[WAVPlayer] WAV file was not found: {filePath}");
                yield break;
            }

            yield return LoadAndPlayWAV(filePath);

            while (audioSource != null && audioSource.isPlaying)
            {
                yield return null;
            }
        }

        public void StopPlayback()
        {
            if (playbackCoroutine != null)
            {
                StopCoroutine(playbackCoroutine);
                playbackCoroutine = null;
            }

            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
            }
        }

        private IEnumerator LoadAndPlayWAV(string filePath)
        {
            Debug.Log($"[WAVPlayer] Loading WAV: {filePath}");

            byte[] wavData = File.ReadAllBytes(filePath);
            AudioClip clip = DecodeWAVToAudioClip(wavData, Path.GetFileNameWithoutExtension(filePath));

            if (clip != null)
            {
                audioSource.clip = clip;
                audioSource.Play();
                Debug.Log("[WAVPlayer] Playback started.");
            }
            else
            {
                Debug.LogError("[WAVPlayer] Failed to decode WAV.");
            }

            playbackCoroutine = null;
            yield return null;
        }

        private AudioClip DecodeWAVToAudioClip(byte[] wavData, string clipName)
        {
            try
            {
                int numChannels = System.BitConverter.ToInt16(wavData, 22);
                int sampleRate = System.BitConverter.ToInt32(wavData, 24);
                int audioDataSize = System.BitConverter.ToInt32(wavData, 40);
                int audioDataOffset = 44;

                int numSamples = audioDataSize / (2 * numChannels);
                float[] floatData = new float[numSamples * numChannels];

                for (int i = 0; i < numSamples * numChannels; i++)
                {
                    short pcmSample = System.BitConverter.ToInt16(wavData, audioDataOffset + i * 2);
                    floatData[i] = pcmSample / 32768f;
                }

                AudioClip clip = AudioClip.Create(clipName, numSamples, numChannels, sampleRate, false);
                clip.SetData(floatData, 0);

                Debug.Log($"[WAVPlayer] AudioClip created: {clipName} ({sampleRate}Hz, {numChannels}ch)");
                return clip;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WAVPlayer] WAV decode error: {ex.Message}");
                return null;
            }
        }
    }
}
