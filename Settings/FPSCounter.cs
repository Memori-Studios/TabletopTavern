using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Memori.Settings
{
[RequireComponent(typeof(TMP_Text))]
public class FPSCounter : MonoBehaviour
{
    private TMP_Text fpsCounterText;
    private float deltaTime, updateFrameRate = 0.25f;
    float timer;
    private void Awake() {
        fpsCounterText = GetComponent<TMP_Text>();
    }
    private void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        float fps = 1.0f / deltaTime;

        timer += Time.unscaledDeltaTime;
        if(timer < updateFrameRate)
            return;

        timer = 0;
        fpsCounterText.text = $"{Mathf.RoundToInt(fps)} FPS";
    }
}
}