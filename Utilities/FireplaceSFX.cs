using UnityEngine;
using Memori.Audio;
using Memori.Core;

[RequireComponent(typeof(AudioSource))]
public class FireplaceSFX : MonoBehaviour
{
    AudioSource fireplaceAudioSource;
    private void Start()
    {
        fireplaceAudioSource = GetComponent<AudioSource>();
        IAudioRequester.Instance.effectsVolume.OnValueChanged += FireplaceSFXLevelChange;
        FireplaceSFXLevelChange(IAudioRequester.Instance.effectsVolume.GetValue());
    }
    private void OnDestroy() 
    {
        if (IAudioRequester.HasInstance)
            IAudioRequester.Instance.effectsVolume.OnValueChanged -= FireplaceSFXLevelChange;
    }
    private void FireplaceSFXLevelChange(float volume) 
    {
        fireplaceAudioSource.volume = 0.2f * volume;
    }
}
