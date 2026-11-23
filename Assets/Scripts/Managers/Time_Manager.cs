using System;
using System.Collections;
using UnityEngine;

public class Time_Manager : MonoBehaviour
{
    // Skyboxes
    [SerializeField] private Texture2D skyboxNight;
    [SerializeField] private Texture2D skyboxSunrise;
    [SerializeField] private Texture2D skyboxDay;
    [SerializeField] private Texture2D skyboxSunset;

    // Gradients for light change
    [SerializeField] private Gradient gradientNightToSunrise;
    [SerializeField] private Gradient gradientSunriseToDay;
    [SerializeField] private Gradient gradientDayToSunset;
    [SerializeField] private Gradient gradientSunsetToNight;

    [SerializeField, Min(0f)] private float fogNightDensity = 0.06f;
    [SerializeField, Min(0f)] private float fogTwilightDensity = 0.02f;
    [SerializeField, Min(0f)] private float fogDayDensity = 0.005f;

    [SerializeField] private Light directionalLight;
    private Material originalSkybox;
    private Material runtimeSkybox;
    [SerializeField] private int minutes;
    public int Minutes
    {
        get { return minutes; }
        set { minutes = value; OnMinuteChange(value); }
    }

    [SerializeField] private int hours;
    public int Hours
    {
        get { return hours; }
        set { hours = value; OnHoursChange(value); }
    }

    [SerializeField] private int days;
    public int Days
    {
        get { return days; }
        set { days = value; }
    }

    private float tempSecond;

    private void OnEnable()
    {
        originalSkybox = RenderSettings.skybox;
        if (originalSkybox != null)
        {
            runtimeSkybox = new Material(originalSkybox);
            RenderSettings.skybox = runtimeSkybox;
        }
    }

    private void OnDisable()
    {
        if (runtimeSkybox == null)
        {
            return;
        }

        if (RenderSettings.skybox == runtimeSkybox)
        {
            RenderSettings.skybox = originalSkybox;
        }

        Destroy(runtimeSkybox);
        runtimeSkybox = null;
    }

    private void Start()
    {
        if (hours >= 20 || hours < 6)
        {
            RenderSettings.fogDensity = fogNightDensity;
        }
        else if ((hours >= 6 && hours < 8) || (hours >= 18 && hours < 20))
        {
            RenderSettings.fogDensity = fogTwilightDensity;
        }
        else
        {
            RenderSettings.fogDensity = fogDayDensity;
        }
    }

    public void Update()
    {
        tempSecond += Time.deltaTime;
        if(tempSecond >= 1f)
        {
            tempSecond = 0f;
            Minutes = minutes + 1;
        }
    }

    private void OnMinuteChange(int value)
    {
        if (directionalLight != null)
        {
            directionalLight.transform.Rotate(Vector3.up, (1f / 1440f) * 360f, Space.World);
        }
        if (value >= 60)
        {
            minutes = 0;
            Hours = hours + 1;
            if (hours >= 24)
            {
                Hours = 0;
                Days++;
            }
        }
    }

    private void OnHoursChange(int value)
    {
        // Change skybox and light based on the hour of the day
        // 6 AM - Sunrise
        if (value == 6)
        {
            StartCoroutine(LerpSkyBox(skyboxNight, skyboxSunrise, 90f));
            StartCoroutine(LerpLight(gradientNightToSunrise, fogTwilightDensity, 90f));
        } // 8 AM - Day
        else if (value == 8)
        {
            StartCoroutine(LerpSkyBox(skyboxSunrise, skyboxDay, 90f));
            StartCoroutine(LerpLight(gradientSunriseToDay, fogDayDensity, 90f));
        } // 6 PM - Sunset
        else if (value == 18)
        {
            StartCoroutine(LerpSkyBox(skyboxDay, skyboxSunset, 90f));
            StartCoroutine(LerpLight(gradientDayToSunset, fogTwilightDensity, 90f));
        } // 8 PM - Night
        else if (value == 20)
        {
            StartCoroutine(LerpSkyBox(skyboxSunset, skyboxNight, 90f));
            StartCoroutine(LerpLight(gradientSunsetToNight, fogNightDensity, 90f));
        }
    }

    private IEnumerator LerpSkyBox(Texture2D a, Texture2D b, float duration)
    {
        runtimeSkybox.SetTexture("_Texture1", a);
        runtimeSkybox.SetTexture("_Texture2", b);
        runtimeSkybox.SetFloat("_Blend", 0f);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            runtimeSkybox.SetFloat("_Blend", t / duration);
            yield return null;
        }
        runtimeSkybox.SetTexture("_Texture1", b);
        runtimeSkybox.SetTexture("_Texture2", b);
        runtimeSkybox.SetFloat("_Blend", 0f);
    }

    private IEnumerator LerpLight(Gradient lightGradient, float targetFogDensity, float duration)
    {
        float startingFogDensity = RenderSettings.fogDensity;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float blend = t / duration;
            Color lightColor = lightGradient.Evaluate(blend);
            if (directionalLight != null)
            {
                directionalLight.color = lightColor;
            }
            RenderSettings.fogColor = lightColor;
            RenderSettings.fogDensity = Mathf.Lerp(startingFogDensity, targetFogDensity, blend);
            yield return null;
        }
        Color finalLightColor = lightGradient.Evaluate(1f);
        if (directionalLight != null)
        {
            directionalLight.color = finalLightColor;
        }
        RenderSettings.fogColor = finalLightColor;
        RenderSettings.fogDensity = targetFogDensity;
    }
}
