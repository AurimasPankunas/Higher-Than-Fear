using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Applies wind-driven visual movement to the hand while leaving its tracked interactor untouched.
/// </summary>
[RequireComponent(typeof(XRDirectInteractor))]
public class WindSlipHand : MonoBehaviour, IXRSelectFilter
{
    // These references are found from the hand hierarchy at startup if they are not assigned in the Inspector.
    [SerializeField] private WeatherManager weatherManager;
    [SerializeField] private Transform visualPivot;
    [SerializeField] private AnimateHandOnInput handAnimation;
    [SerializeField] private ClimbingHandVisualLatch visualLatch;

    // Converts the weather's wind speed into visual hand-slide speed.
    [SerializeField, Min(0f)] private float windSlipMultiplier = 0.02f;
    // A hand slips when its visual attach point is this far outside the saved rung collider.
    [SerializeField, Min(0f)] private float slipDistance = 0.12f;
    // How quickly the visual pivot returns to neutral after the hand stops holding a rung.
    [SerializeField, Min(0f)] private float returnSpeed = 0.5f;

    // The XR interactor stays tracked; wind affects only the visual latch or visual pivot.
    private XRDirectInteractor interactor;
    private Vector3 restPivotLocalPosition;
    private ClimbInteractable heldClimbable;
    private ClimbInteractable lockedClimbable;
    private Collider heldRungCollider;
    private Vector3 slideOffsetWorld;
    private bool slipDetected;
    private bool waitingForGripRelease;

    /// <summary>Allows XRI to use this filter only while the hand component is active.</summary>
    public bool canProcess => isActiveAndEnabled;

    private void Awake()
    {
        // Resolve scene references from the tracked hand and its visual-model hierarchy.
        interactor = GetComponent<XRDirectInteractor>();

        if (handAnimation == null)
        {
            handAnimation = GetComponentInChildren<AnimateHandOnInput>(true);
        }

        if (visualLatch == null)
        {
            visualLatch = GetComponent<ClimbingHandVisualLatch>();
        }

        if (visualPivot == null && handAnimation != null)
        {
            visualPivot = handAnimation.transform.parent;
        }

        if (weatherManager == null)
        {
            weatherManager = FindAnyObjectByType<WeatherManager>();
        }

        if ((visualPivot == null && visualLatch == null) ||
            handAnimation == null ||
            weatherManager == null)
        {
            Debug.LogError(
                "WindSlipHand requires a visual pivot or hand visual latch, hand animation, and WeatherManager.",
                this);
            enabled = false;
            return;
        }

        // Save the pivot's authored position for scenes that do not use a visual latch.
        if (visualPivot != null)
        {
            restPivotLocalPosition = visualPivot.localPosition;
        }
    }

    private void OnEnable()
    {
        // Register on this hand's interactor so the other hand remains unaffected.
        if (interactor != null)
        {
            interactor.selectFilters.Add(this);
        }
    }

    private void OnDisable()
    {
        // Remove the runtime filter and restore the visual state if this component is disabled.
        if (interactor != null)
        {
            interactor.selectFilters.Remove(this);
        }

        if (handAnimation != null)
        {
            handAnimation.forceOpen = false;
        }

        EndHold();
        ResetVisualOffset();
        waitingForGripRelease = false;
        lockedClimbable = null;
    }

    /// <summary>Blocks this hand from reselecting the slipped climbable until its grip is released.</summary>
    public bool Process(IXRSelectInteractor candidate, IXRSelectInteractable interactable)
    {
        if (!ReferenceEquals(candidate, interactor) || !waitingForGripRelease || !ReferenceEquals(interactable, lockedClimbable))
        {
            return true;
        }

        if (handAnimation == null || !handAnimation.IsGripPressed)
        {
            waitingForGripRelease = false;
            lockedClimbable = null;
            if (handAnimation != null)
            {
                handAnimation.forceOpen = false;
            }

            return true;
        }

        return false;
    }

    private void Update()
    {
        // End the re-grip lock only after the physical grip action returns to neutral.
        if (waitingForGripRelease && !handAnimation.IsGripPressed)
        {
            // Keep the visual open until the real grip input returns to neutral.
            waitingForGripRelease = false;
            lockedClimbable = null;
            handAnimation.forceOpen = false;
        }

        // Track selection changes independently for each controller hand.
        ClimbInteractable selectedClimbable = GetSelectedClimbable();
        if (selectedClimbable != heldClimbable)
        {
            EndHold();
            if (selectedClimbable != null)
            {
                BeginHold(selectedClimbable);
            }
        }

        if (heldClimbable != null && !slipDetected)
        {
            ApplyWindSlide();
        }
        else
        {
            ReturnVisualToRest();
        }
    }

    private ClimbInteractable GetSelectedClimbable()
    {
        // Only ladder/climb selections participate in wind slip.
        foreach (var selectedInteractable in interactor.interactablesSelected)
        {
            if (selectedInteractable is ClimbInteractable climbInteractable)
            {
                return climbInteractable;
            }
        }

        return null;
    }

    private void BeginHold(ClimbInteractable climbInteractable)
    {
        // Remember the rung under this hand so sliding onto another rung still counts as slipping.
        heldClimbable = climbInteractable;
        heldRungCollider = FindNearestRungCollider(climbInteractable);
        slideOffsetWorld = Vector3.zero;
        slipDetected = false;
        ApplyVisualOffset();

        if (heldRungCollider == null)
        {
            Debug.LogWarning($"No rung collider was found under {climbInteractable.name}.", climbInteractable);
        }
    }

    private Collider FindNearestRungCollider(ClimbInteractable climbInteractable)
    {
        // A ClimbInteractable may define colliders explicitly or use all colliders in its child hierarchy.
        var colliders = climbInteractable.colliders;
        Collider[] childColliders = colliders.Count == 0
            ? climbInteractable.GetComponentsInChildren<Collider>()
            : null;
        Vector3 handPosition = interactor.GetAttachTransform(climbInteractable).position;
        Collider nearestCollider = null;
        float nearestDistanceSquared = float.PositiveInfinity;
        int colliderCount = colliders.Count == 0 ? childColliders.Length : colliders.Count;

        for (int i = 0; i < colliderCount; i++)
        {
            Collider candidate = colliders.Count == 0 ? childColliders[i] : colliders[i];
            if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            float distanceSquared = (candidate.ClosestPoint(handPosition) - handPosition).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearestCollider = candidate;
            }
        }

        return nearestCollider;
    }

    private void ApplyWindSlide()
    {
        if (heldRungCollider == null)
        {
            return;
        }

        // Smoothly remove any existing displacement if the weather changes while holding.
        if (weatherManager == null || weatherManager.GetCurrentWeather().name != "Windy")
        {
            ReturnVisualToRest();
            return;
        }

        // Accumulate a world-space displacement so turning the player does not rotate the wind.
        slideOffsetWorld += weatherManager.GetWindVelocity() * windSlipMultiplier * Time.deltaTime;
        ApplyVisualOffset();

        // Compare the offset visual attach point against only the rung captured when this hand grabbed.
        Vector3 visualHandPosition = interactor.GetAttachTransform(heldClimbable).position + slideOffsetWorld;
        float distanceFromRung = Vector3.Distance(heldRungCollider.ClosestPoint(visualHandPosition), visualHandPosition);
        if (distanceFromRung > slipDistance)
        {
            ForceSlip();
        }
    }

    private void ForceSlip()
    {
        // Avoid sending repeated release requests after the threshold is crossed.
        if (heldClimbable == null || slipDetected)
        {
            return;
        }

        slipDetected = true;
        lockedClimbable = heldClimbable;
        waitingForGripRelease = true;
        if (handAnimation != null)
        {
            handAnimation.forceOpen = true;
        }

        // Exit only this hand so another interactor can remain on the shared ladder.
        interactor.interactionManager.SelectExit(
            (IXRSelectInteractor)interactor,
            (IXRSelectInteractable)heldClimbable);
        Debug.Log($"{name} has slipped off {heldRungCollider.name}.", this);
    }

    private void EndHold()
    {
        // Keep the offset for the return animation, but stop checking the released rung.
        heldClimbable = null;
        heldRungCollider = null;
        slipDetected = false;
    }

    private void ReturnVisualToRest()
    {
        // Move at a fixed rate so the hand returns smoothly regardless of frame rate.
        slideOffsetWorld = Vector3.MoveTowards(slideOffsetWorld, Vector3.zero, returnSpeed * Time.deltaTime);
        ApplyVisualOffset();
    }

    private void ApplyVisualOffset()
    {
        // The latch owns the final pose when present, so latched and wind poses compose.
        if (visualLatch != null)
        {
            visualLatch.SetVisualOffsetWorld(slideOffsetWorld);
            return;
        }

        // Preserve the original pivot-based behavior for scenes without a visual latch.
        if (visualPivot != null && visualPivot.parent != null)
            visualPivot.localPosition = restPivotLocalPosition + visualPivot.parent.InverseTransformVector(slideOffsetWorld);
    }

    private void ResetVisualOffset()
    {
        // Disabling the component should restore the model immediately rather than leave it offset.
        slideOffsetWorld = Vector3.zero;
        ApplyVisualOffset();
    }
}
