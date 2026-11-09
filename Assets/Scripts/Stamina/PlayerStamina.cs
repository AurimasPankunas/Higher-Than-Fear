using System;
using UnityEngine;

/// <summary>
/// Owns the player's stamina value and its drain/regeneration rules.
/// Contains no XR code: other components report what the player is doing and react to the events.
/// </summary>
public sealed class PlayerStamina : MonoBehaviour
{
    [SerializeField] StaminaSettings m_Settings = new StaminaSettings();

    float m_Current;
    float m_TimeSinceLastUse;
    bool m_IsDraining;

    /// <summary>Raised when the value changes. Arguments: current, max.</summary>
    public event Action<float, float> StaminaChanged;

    /// <summary>Raised once when stamina reaches zero.</summary>
    public event Action Exhausted;

    /// <summary>Raised once when an exhausted player has regained enough stamina to grab again.</summary>
    public event Action Recovered;

    /// <summary>Tuning values. Changes made in the Inspector apply immediately, including in play mode.</summary>
    public StaminaSettings Settings => m_Settings;

    public float Current => m_Current;
    public float Max => m_Settings.MaxStamina;
    public float Normalized => Max > 0f ? m_Current / Max : 0f;

    /// <summary>True from reaching zero until stamina recovers to the settings' grab threshold.</summary>
    public bool IsExhausted { get; private set; }

    void Awake()
    {
        m_Current = m_Settings.MaxStamina;
        m_TimeSinceLastUse = m_Settings.RegenDelay;
    }

    void OnValidate()
    {
        m_Settings?.Validate();
    }

    void Start()
    {
        // Listeners subscribe during Awake/OnEnable, so publish the initial value once everything is ready.
        StaminaChanged?.Invoke(m_Current, Max);
    }

    void Update()
    {
        float deltaTime = Time.deltaTime;

        if (m_IsDraining)
        {
            m_TimeSinceLastUse = 0f;
            SetCurrent(m_Current - m_Settings.PassiveDrainPerSecond * deltaTime);
        }
        else
        {
            m_TimeSinceLastUse += deltaTime;
            if (m_TimeSinceLastUse >= m_Settings.RegenDelay)
                SetCurrent(m_Current + m_Settings.RegenPerSecond * deltaTime);
        }
    }

    /// <summary>Enables or disables the continuous drain. Regeneration only runs while this is false.</summary>
    public void SetDraining(bool isDraining)
    {
        m_IsDraining = isDraining;
    }

    /// <summary>Instantly removes stamina (clamped at zero) and restarts the regeneration delay.</summary>
    public void Spend(float amount)
    {
        if (!isActiveAndEnabled || amount <= 0f)
            return;

        m_TimeSinceLastUse = 0f;
        SetCurrent(m_Current - amount);
    }

    void SetCurrent(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, m_Settings.MaxStamina);
        if (!Mathf.Approximately(clamped, m_Current))
        {
            m_Current = clamped;
            StaminaChanged?.Invoke(m_Current, m_Settings.MaxStamina);
        }

        if (!IsExhausted && m_Current <= 0f)
        {
            IsExhausted = true;
            Exhausted?.Invoke();
        }
        else if (IsExhausted && m_Current >= m_Settings.MinStaminaToGrabAfterExhaustion)
        {
            IsExhausted = false;
            Recovered?.Invoke();
        }
    }
}
