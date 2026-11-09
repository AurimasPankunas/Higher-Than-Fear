using System;
using UnityEngine;

/// <summary>
/// Tuning values for the stamina system, edited in the Inspector on <see cref="PlayerStamina"/>.
/// </summary>
[Serializable]
public sealed class StaminaSettings
{
    [Header("Capacity")]
    [Tooltip("Maximum (and starting) stamina.")]
    [SerializeField, Min(1f)] float m_MaxStamina = 100f;

    [Header("Drain")]
    [Tooltip("Stamina lost per second while at least one hand holds a ladder.")]
    [SerializeField, Min(0f)] float m_PassiveDrainPerSecond = 1f;

    [Tooltip("Stamina lost each time a hand grabs a rung.")]
    [SerializeField, Min(0f)] float m_GrabCost = 5f;

    [Tooltip("Seconds before re-grabbing the same rung costs stamina again. Prevents grip spamming on one rung.")]
    [SerializeField, Min(0f)] float m_SameRungCostCooldown = 2f;

    [Header("Regeneration")]
    [Tooltip("Stamina gained per second while no hand holds a ladder.")]
    [SerializeField, Min(0f)] float m_RegenPerSecond = 3f;

    [Tooltip("Seconds after letting go (or spending stamina) before regeneration starts.")]
    [SerializeField, Min(0f)] float m_RegenDelay = 0f;

    [Header("Exhaustion")]
    [Tooltip("After stamina hits zero, ladders cannot be grabbed until stamina recovers to this value.")]
    [SerializeField, Min(0f)] float m_MinStaminaToGrabAfterExhaustion = 10f;

    public float MaxStamina => m_MaxStamina;
    public float PassiveDrainPerSecond => m_PassiveDrainPerSecond;
    public float GrabCost => m_GrabCost;
    public float SameRungCostCooldown => m_SameRungCostCooldown;
    public float RegenPerSecond => m_RegenPerSecond;
    public float RegenDelay => m_RegenDelay;
    public float MinStaminaToGrabAfterExhaustion => m_MinStaminaToGrabAfterExhaustion;

    /// <summary>Keeps values consistent; called from the owner's OnValidate.</summary>
    public void Validate()
    {
        // The recovery threshold must be reachable.
        m_MinStaminaToGrabAfterExhaustion = Mathf.Min(m_MinStaminaToGrabAfterExhaustion, m_MaxStamina);
    }
}
