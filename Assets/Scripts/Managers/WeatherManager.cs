using UnityEngine;
using UnityEngine.InputSystem;

public class WeatherManager : MonoBehaviour
{
    /// <summary>
    /// Cardinal direction the wind travels toward, using Unity's horizontal world axes.
    /// </summary>
    public enum WindDirection
    {
        North,
        East,
        South,
        West
    }

    /// <summary>
    /// Represents a weather condition with its properties.
    /// </summary>
    [System.Serializable]
    public class Weather
    {
        public string name;
        public float temperature;
        public float rainStrength;
        public float windSpeed;
        // The first enum value is North, so older scene data with no saved direction defaults north.
        public WindDirection windDirection;

        /// <summary>Creates a weather value; omitted wind directions default to North.</summary>
        public Weather(string name, float temperature, float rainStrength, float windSpeed, WindDirection windDirection = WindDirection.North)
        {
            this.name = name;
            this.temperature = temperature;
            this.rainStrength = rainStrength;
            this.windSpeed = windSpeed;
            this.windDirection = windDirection;
        }
    }

    /// <summary>
    /// The current weather condition.
    /// </summary>
    [SerializeField] private Weather currentWeather = new Weather("Sunny", 25f, 0f, 5f);


    /// <summary>
    /// Returns the current weather condition.
    /// </summary>
    /// <returns>The current weather condition.</returns>
    public Weather GetCurrentWeather()
    {
        return currentWeather;
    }

    /// <summary>
    /// Returns wind direction multiplied by wind speed in world units per second.
    /// </summary>
    public Vector3 GetWindVelocity()
    {
        // Convert the inspector-friendly compass setting to a unit vector in world space.
        Vector3 direction = currentWeather.windDirection switch
        {
            WindDirection.East => Vector3.right,
            WindDirection.South => Vector3.back,
            WindDirection.West => Vector3.left,
            _ => Vector3.forward
        };

        // Multiply the direction by its speed so callers can use this directly as velocity.
        return direction * currentWeather.windSpeed;
    }

    /// <summary>
    /// Sets the current weather condition.
    /// </summary>
    /// <param name="name">The name of the weather condition.</param>
    /// <param name="temperature">The temperature of the weather condition.</param>
    /// <param name="rainStrength">The rainStrength of the weather condition.</param>
    /// <param name="windSpeed">The wind speed of the weather condition.</param>
    /// <param name="windDirection">The compass direction the wind travels toward.</param>
    public void SetWeather(string name, float temperature, float rainStrength, float windSpeed, WindDirection windDirection = WindDirection.North)
    {
        currentWeather = new Weather(name, temperature, rainStrength, windSpeed, windDirection);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // Temporary keyboard controls let the weather reactions be tested before a weather system is added.
        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
        {
            currentWeather.name = "Windy";
            Debug.Log("Weather changed to Windy");
        }

        if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
        {
            currentWeather.name = "Rainy";
            Debug.Log("Weather changed to Rainy");
        }
    }
}
