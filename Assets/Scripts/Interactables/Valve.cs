using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Lets the player grab and physically turn this valve.
/// After the configured number of counter-clockwise turns the valve locks
/// and can no longer be grabbed or turned.
///
/// The valve is kinematic and its rotation is driven by the player's hand
/// position around the wheel, so there is no physics/joint jitter.
/// The Rigidbody and XRGrabInteractable are set up at runtime, so the Valve
/// prefab does not need any manual component setup.
/// </summary>
public class Valve : MonoBehaviour
{
    [Header("Turning")]
    [Tooltip("Local axis the valve spins around. Leave at zero to auto-detect the disc axis from the valve collider.")]
    [SerializeField] private Vector3 spinAxis = Vector3.zero;

    [Tooltip("Number of full turns required to lock the valve.")]
    [SerializeField] private float turnsToLock = 2f;

    [Tooltip("If the valve only turns and locks when moved the wrong way in VR, toggle this.")]
    [SerializeField] private bool counterClockwise = true;

    public UnityEvent onLocked;

    private Rigidbody valveBody;
    private XRGrabInteractable grabInteractable;

    private IXRSelectInteractor activeInteractor;

    private Quaternion startLocalRotation;
    private Vector3 startLocalPosition;
    private Vector3 localPivot;
    private Vector3 axisInParentSpace;
    private Vector3 worldAxis;
    private Vector3 pivotWorld;
    private Vector3 referenceDirection;

    private float previousHandAngle;
    private float progressAngle;
    private bool hasHandAngle;
    private bool isLocked;

    private const float HandAngleDeadzone = 0.05f;
    private const float MinHandRadius = 0.05f;

    private void Awake()
    {
        spinAxis = ResolveSpinAxis();
        startLocalRotation = transform.localRotation;
        startLocalPosition = transform.localPosition;

        SetupRigidbody();
        SetupGrabInteractable();

        worldAxis = transform.TransformDirection(spinAxis).normalized;
        pivotWorld = GetSpinPivotWorld();
        referenceDirection = GetReferenceDirection(worldAxis);

        // The script rotates the valve around its wheel centre, which may not
        // be the transform's pivot point. Cache the pivot in parent space.
        Transform parent = transform.parent;
        localPivot = parent != null ? parent.InverseTransformPoint(pivotWorld) : pivotWorld;
        axisInParentSpace = parent != null ? parent.InverseTransformDirection(worldAxis) : worldAxis;
    }

    private void OnDestroy()
    {
        if (grabInteractable == null)
            return;

        grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
        grabInteractable.selectExited.RemoveListener(OnSelectExited);
    }

    /// <summary>
    /// Uses the configured axis, or falls back to the thinnest axis of the
    /// valve's box collider (the disc axis) when no axis is specified.
    /// </summary>
    private Vector3 ResolveSpinAxis()
    {
        if (spinAxis.sqrMagnitude > 0.0001f)
            return spinAxis.normalized;

        BoxCollider box = GetComponentInChildren<BoxCollider>();
        if (box == null)
            return Vector3.forward;

        Vector3 size = box.size;
        Vector3 colliderAxis;

        if (size.x <= size.y && size.x <= size.z)
            colliderAxis = Vector3.right;
        else if (size.y <= size.z)
            colliderAxis = Vector3.up;
        else
            colliderAxis = Vector3.forward;

        Vector3 boxWorldAxis = box.transform.TransformDirection(colliderAxis);
        return transform.InverseTransformDirection(boxWorldAxis).normalized;
    }

    private void SetupRigidbody()
    {
        valveBody = GetComponent<Rigidbody>();
        if (valveBody == null)
            valveBody = gameObject.AddComponent<Rigidbody>();

        // Kinematic: the valve should never be pushed around by physics. The
        // script moves it directly from the hand position instead.
        valveBody.isKinematic = true;
        valveBody.useGravity = false;
    }

    private void SetupGrabInteractable()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();

        // Make sure there is exactly one interactable on the valve. The scene
        // currently has an XRSimpleInteractable on it, which cannot be grabbed.
        foreach (XRBaseInteractable interactable in GetComponents<XRBaseInteractable>())
        {
            if (interactable == grabInteractable)
                continue;

            interactable.enabled = false;
            Destroy(interactable);
        }

        if (grabInteractable == null)
            grabInteractable = gameObject.AddComponent<XRGrabInteractable>();

        grabInteractable.enabled = true;
        grabInteractable.selectMode = InteractableSelectMode.Single;

        // The valve is rotated by this script, not by the grab transformer.
        grabInteractable.trackPosition = false;
        grabInteractable.trackRotation = false;
        grabInteractable.throwOnDetach = false;

        grabInteractable.selectEntered.AddListener(OnSelectEntered);
        grabInteractable.selectExited.AddListener(OnSelectExited);
    }

    private Vector3 GetSpinPivotWorld()
    {
        Collider valveCollider = GetComponentInChildren<Collider>();
        return valveCollider != null ? valveCollider.bounds.center : transform.position;
    }

    private static Vector3 GetReferenceDirection(Vector3 axis)
    {
        Vector3 reference = Vector3.ProjectOnPlane(Vector3.right, axis);

        if (reference.sqrMagnitude < 0.0001f)
            reference = Vector3.ProjectOnPlane(Vector3.forward, axis);

        return reference.normalized;
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (isLocked)
            return;

        activeInteractor = args.interactorObject;
        hasHandAngle = TryGetHandAngle(activeInteractor.transform.position, out previousHandAngle);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        if (args.interactorObject != activeInteractor)
            return;

        activeInteractor = null;
        hasHandAngle = false;
    }

    private void Update()
    {
        if (isLocked || activeInteractor == null)
            return;

        if (!TryGetHandAngle(activeInteractor.transform.position, out float handAngle))
        {
            hasHandAngle = false;
            return;
        }

        if (!hasHandAngle)
        {
            previousHandAngle = handAngle;
            hasHandAngle = true;
            return;
        }

        float handDelta = Mathf.DeltaAngle(previousHandAngle, handAngle);
        previousHandAngle = handAngle;

        float turnDelta = counterClockwise ? handDelta : -handDelta;

        // Ratchet: only the allowed direction moves the valve. This also
        // removes hand-tracking jitter from the rotation.
        if (turnDelta <= HandAngleDeadzone)
            return;

        float targetAngle = 360f * turnsToLock;
        float remainingAngle = targetAngle - progressAngle;
        if (remainingAngle <= 0f)
            return;

        progressAngle += Mathf.Min(turnDelta, remainingAngle);

        float visualAngle = counterClockwise ? progressAngle : -progressAngle;
        Quaternion rotationDelta = Quaternion.AngleAxis(visualAngle, axisInParentSpace);
        transform.localRotation = rotationDelta * startLocalRotation;
        transform.localPosition = localPivot + rotationDelta * (startLocalPosition - localPivot);

        if (progressAngle >= targetAngle - 0.001f)
            LockValve();
    }

    private bool TryGetHandAngle(Vector3 handPosition, out float angle)
    {
        Vector3 fromPivot = handPosition - pivotWorld;
        Vector3 planarDirection = Vector3.ProjectOnPlane(fromPivot, worldAxis);

        if (planarDirection.sqrMagnitude < MinHandRadius * MinHandRadius)
        {
            angle = 0f;
            return false;
        }

        angle = Vector3.SignedAngle(referenceDirection, planarDirection, worldAxis);
        return true;
    }

    private void LockValve()
    {
        isLocked = true;
        onLocked?.Invoke();
        progressAngle = 360f * turnsToLock;

        // Release the hand cleanly, then stop the valve being grabbable.
        if (activeInteractor != null && grabInteractable.interactionManager != null)
            grabInteractable.interactionManager.SelectExit(activeInteractor, grabInteractable);

        activeInteractor = null;
        hasHandAngle = false;

        // The player can no longer grab or turn the valve.
        if (grabInteractable != null)
            grabInteractable.enabled = false;
    }
}
