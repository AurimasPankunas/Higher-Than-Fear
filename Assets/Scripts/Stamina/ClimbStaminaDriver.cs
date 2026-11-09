using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing;

/// <summary>
/// Connects climbing to stamina: reports whether any hand holds a ladder (passive drain),
/// charges stamina for grabbing rungs, forces both hands off when exhausted,
/// and blocks ladder grabs until the player has recovered.
/// </summary>
public sealed class ClimbStaminaDriver : MonoBehaviour, IXRSelectFilter
{
    [SerializeField] PlayerStamina m_Stamina;

    [Tooltip("The left and right hand interactors that can grab ladders.")]
    [SerializeField] XRBaseInteractor[] m_HandInteractors;

    // Per hand state, indexed like m_HandInteractors.
    AnimateHandOnInput[] m_HandAnimations;
    bool[] m_HandForcedOpen;
    Object[] m_HeldRungKeys;

    // Last time each rung was grabbed or released, used to avoid charging for grip spamming.
    readonly Dictionary<Object, float> m_RungLastTouchedTime = new Dictionary<Object, float>();
    readonly List<IXRSelectInteractable> m_ReleaseBuffer = new List<IXRSelectInteractable>();

    bool m_ReleaseRequested;

    /// <summary>Allows XRI to use this filter only while the component is active.</summary>
    public bool canProcess => isActiveAndEnabled;

    void Awake()
    {
        if (m_Stamina == null || m_HandInteractors == null || m_HandInteractors.Length == 0)
        {
            Debug.LogError("ClimbStaminaDriver needs a PlayerStamina and at least one hand interactor.", this);
            enabled = false;
            return;
        }

        int handCount = m_HandInteractors.Length;
        m_HandAnimations = new AnimateHandOnInput[handCount];
        m_HandForcedOpen = new bool[handCount];
        m_HeldRungKeys = new Object[handCount];

        for (int i = 0; i < handCount; i++)
        {
            if (m_HandInteractors[i] == null)
            {
                Debug.LogError($"ClimbStaminaDriver hand interactor {i} is not assigned.", this);
                enabled = false;
                return;
            }

            m_HandAnimations[i] = m_HandInteractors[i].GetComponentInChildren<AnimateHandOnInput>(true);
        }
    }

    void OnEnable()
    {
        m_Stamina.Exhausted += OnExhausted;

        foreach (XRBaseInteractor interactor in m_HandInteractors)
        {
            interactor.selectEntered.AddListener(OnSelectEntered);
            interactor.selectExited.AddListener(OnSelectExited);
            interactor.selectFilters.Add(this);
        }
    }

    void OnDisable()
    {
        // Awake can disable this component before the references were validated.
        if (m_HandForcedOpen == null)
            return;

        m_Stamina.Exhausted -= OnExhausted;
        m_Stamina.SetDraining(false);

        for (int i = 0; i < m_HandInteractors.Length; i++)
        {
            XRBaseInteractor interactor = m_HandInteractors[i];
            interactor.selectEntered.RemoveListener(OnSelectEntered);
            interactor.selectExited.RemoveListener(OnSelectExited);
            interactor.selectFilters.Remove(this);

            SetHandForcedOpen(i, false);
        }
    }

    /// <summary>Blocks ladder grabs while the player is exhausted. Other interactables are unaffected.</summary>
    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        return !(interactable is ClimbInteractable) || !m_Stamina.IsExhausted;
    }

    void Update()
    {
        m_Stamina.SetDraining(IsAnyHandClimbing());

        if (m_ReleaseRequested)
        {
            m_ReleaseRequested = false;
            ReleaseAllClimbs();
        }

        // Keep the hand visually open until the real grip returns to neutral, matching WindSlipHand.
        for (int i = 0; i < m_HandForcedOpen.Length; i++)
        {
            if (m_HandForcedOpen[i] && (m_HandAnimations[i] == null || !m_HandAnimations[i].IsGripPressed))
                SetHandForcedOpen(i, false);
        }
    }

    // Raised from inside stamina updates, so the actual release is deferred to Update.
    void OnExhausted()
    {
        m_ReleaseRequested = true;
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (!(args.interactableObject is ClimbInteractable climbInteractable))
            return;

        int handIndex = IndexOfHand(args.interactorObject);
        if (handIndex < 0)
            return;

        Collider rung = FindNearestRungCollider(climbInteractable, m_HandInteractors[handIndex]);
        // Fall back to the interactable itself when it exposes no usable collider.
        Object rungKey = rung != null ? (Object)rung : climbInteractable;
        m_HeldRungKeys[handIndex] = rungKey;

        if (ShouldChargeForGrab(handIndex, rungKey))
            m_Stamina.Spend(m_Stamina.Settings.GrabCost);

        m_RungLastTouchedTime[rungKey] = Time.time;
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        if (!(args.interactableObject is ClimbInteractable))
            return;

        int handIndex = IndexOfHand(args.interactorObject);
        if (handIndex < 0 || m_HeldRungKeys[handIndex] == null)
            return;

        m_RungLastTouchedTime[m_HeldRungKeys[handIndex]] = Time.time;
        m_HeldRungKeys[handIndex] = null;
    }

    int IndexOfHand(IXRInteractor interactor)
    {
        for (int i = 0; i < m_HandInteractors.Length; i++)
        {
            if (ReferenceEquals(m_HandInteractors[i], interactor))
                return i;
        }

        return -1;
    }

    bool ShouldChargeForGrab(int grabbingHandIndex, Object rungKey)
    {
        // Swapping hands on a rung another hand is still holding is free.
        for (int i = 0; i < m_HeldRungKeys.Length; i++)
        {
            if (i != grabbingHandIndex && m_HeldRungKeys[i] == rungKey)
                return false;
        }

        // Re-grabbing the same rung shortly after touching it is free; falling and retrying later is not.
        return !m_RungLastTouchedTime.TryGetValue(rungKey, out float lastTouched)
               || Time.time - lastTouched >= m_Stamina.Settings.SameRungCostCooldown;
    }

    bool IsAnyHandClimbing()
    {
        foreach (XRBaseInteractor interactor in m_HandInteractors)
        {
            foreach (IXRSelectInteractable selected in interactor.interactablesSelected)
            {
                if (selected is ClimbInteractable)
                    return true;
            }
        }

        return false;
    }

    void ReleaseAllClimbs()
    {
        for (int i = 0; i < m_HandInteractors.Length; i++)
        {
            XRBaseInteractor interactor = m_HandInteractors[i];

            m_ReleaseBuffer.Clear();
            foreach (IXRSelectInteractable selected in interactor.interactablesSelected)
            {
                if (selected is ClimbInteractable)
                    m_ReleaseBuffer.Add(selected);
            }

            if (m_ReleaseBuffer.Count == 0)
                continue;

            SetHandForcedOpen(i, true);
            foreach (IXRSelectInteractable climbable in m_ReleaseBuffer)
                interactor.interactionManager.SelectExit(interactor, climbable);
        }
    }

    void SetHandForcedOpen(int handIndex, bool forcedOpen)
    {
        m_HandForcedOpen[handIndex] = forcedOpen;
        if (m_HandAnimations[handIndex] != null)
            m_HandAnimations[handIndex].forceOpen = forcedOpen;
    }

    // A ClimbInteractable covers many rungs, so the rung under the hand identifies what was grabbed.
    static Collider FindNearestRungCollider(ClimbInteractable climbInteractable, XRBaseInteractor interactor)
    {
        IReadOnlyList<Collider> colliders = climbInteractable.colliders;
        IReadOnlyList<Collider> candidates = colliders.Count > 0
            ? colliders
            : (IReadOnlyList<Collider>)climbInteractable.GetComponentsInChildren<Collider>();

        Vector3 handPosition = interactor.GetAttachTransform(climbInteractable).position;
        Collider nearest = null;
        float nearestDistanceSquared = float.PositiveInfinity;

        foreach (Collider candidate in candidates)
        {
            if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy)
                continue;

            float distanceSquared = (candidate.ClosestPoint(handPosition) - handPosition).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearest = candidate;
            }
        }

        return nearest;
    }
}
