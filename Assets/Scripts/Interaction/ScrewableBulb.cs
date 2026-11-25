using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable))]
public sealed class ScrewableBulb : MonoBehaviour
{
    private enum ThreadDirection
    {
        Clockwise = 1,
        CounterClockwise = -1
    }

    [SerializeField] private XRGrabInteractable grabInteractable;
    [SerializeField] private Transform bulbAxis;
    [SerializeField] private Transform socketAxis;
    [SerializeField, Min(0.01f)] private float insertionRadius = 0.15f;
    [SerializeField, Min(0.01f)] private float resetDistance = 0.35f;
    [SerializeField, Range(1f, 45f)] private float alignmentTolerance = 45f;
    [SerializeField, Min(0.25f)] private float turnsRequired = 0.25f;
    [SerializeField, Range(1f, 180f)] private float maxDegreesPerFrame = 60f;
    [SerializeField] private ThreadDirection threadDirection = ThreadDirection.Clockwise;

    private float twistProgressDegrees;
    private Quaternion previousRotation;
    private bool isInstalled;
    private bool positionLocked;
    private bool rotationConstrained;
    private bool hasSavedGrabSettings;
    private bool originalTrackPosition;
    private bool originalTrackRotation;
    private bool originalThrowOnDetach;
    private Vector3 lockedBulbAxisPosition;
    private IXRSelectInteractor rotationInteractor;
    private Quaternion previousInteractorRotation;

    public bool HasCompletedTwist => twistProgressDegrees >= turnsRequired * 360f;
    public bool CanBeSocketed => HasCompletedTwist && IsWithinInsertionZone();
    public bool CanBeStaged => IsWithinInsertionZone();
    public bool IsInstalled => isInstalled;

    private void Awake()
    {
        if (grabInteractable == null)
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
        }

        if (grabInteractable == null || bulbAxis == null || socketAxis == null)
        {
            Debug.LogError("ScrewableBulb requires an XRGrabInteractable, bulb axis, and socket axis.", this);
            enabled = false;
            return;
        }

        previousRotation = transform.rotation;
    }

    private void OnEnable()
    {
        if (grabInteractable == null)
        {
            return;
        }

        grabInteractable.selectEntered.AddListener(OnGrabSelectEntered);
        grabInteractable.selectExited.AddListener(OnGrabSelectExited);
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnGrabSelectExited);
        }

        RestoreGrabSettings();
    }

    private void LateUpdate()
    {
        if (!rotationConstrained && grabInteractable.isSelected && (isInstalled || CanBeSocketed))
        {
            foreach (IXRSelectInteractor interactor in grabInteractable.interactorsSelecting)
            {
                if (!(interactor is XRSocketInteractor))
                {
                    BeginInstalledGrab(interactor);
                    break;
                }
            }
        }

        bool isSelected = grabInteractable.isSelected;
        float signedTwistDegrees = 0f;
        if (rotationConstrained && isSelected && rotationInteractor != null)
        {
            signedTwistDegrees = ApplyAxialRotation();
        }

        if (positionLocked && isSelected)
        {
            transform.position += lockedBulbAxisPosition - bulbAxis.position;
        }

        Quaternion currentRotation = transform.rotation;
        if (!isSelected)
        {
            previousRotation = currentRotation;
            return;
        }

        float distance = Vector3.Distance(bulbAxis.position, socketAxis.position);
        if (distance > resetDistance && !positionLocked)
        {
            twistProgressDegrees = 0f;
            isInstalled = false;
            previousRotation = currentRotation;
            return;
        }

        if (rotationConstrained)
        {
            if (Mathf.Abs(signedTwistDegrees) > 0f && IsWithinInsertionZone())
            {
                AccumulateTwist(signedTwistDegrees);
            }
        }
        else if (IsWithinInsertionZone())
        {
            AccumulateTwist(currentRotation);
        }

        if (positionLocked && twistProgressDegrees <= 0f)
        {
            AllowRemovalMovement();
        }

        previousRotation = transform.rotation;
    }

    public void MarkInstalled()
    {
        if (HasCompletedTwist)
        {
            isInstalled = true;
        }
    }

    public void ResetProgress()
    {
        twistProgressDegrees = 0f;
        isInstalled = false;
        RestoreGrabSettings();
        previousRotation = transform.rotation;
    }

    private void OnGrabSelectEntered(SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor || (!isInstalled && !CanBeSocketed))
        {
            return;
        }

        BeginInstalledGrab(args.interactorObject);
    }

    private void BeginInstalledGrab(IXRSelectInteractor interactor)
    {
        if (rotationConstrained || interactor is XRSocketInteractor)
        {
            return;
        }

        isInstalled = true;
        lockedBulbAxisPosition = bulbAxis.position;
        originalTrackPosition = grabInteractable.trackPosition;
        originalTrackRotation = grabInteractable.trackRotation;
        originalThrowOnDetach = grabInteractable.throwOnDetach;
        hasSavedGrabSettings = true;
        positionLocked = true;
        rotationConstrained = true;
        rotationInteractor = interactor;
        previousInteractorRotation = rotationInteractor.transform.rotation;
        grabInteractable.trackPosition = false;
        grabInteractable.trackRotation = false;
        grabInteractable.throwOnDetach = false;
        previousRotation = transform.rotation;
    }

    private void OnGrabSelectExited(SelectExitEventArgs args)
    {
        if (!(args.interactorObject is XRSocketInteractor))
        {
            RestoreGrabSettings();
        }
    }

    private float ApplyAxialRotation()
    {
        Quaternion currentInteractorRotation = rotationInteractor.transform.rotation;
        Quaternion delta = currentInteractorRotation * Quaternion.Inverse(previousInteractorRotation);
        previousInteractorRotation = currentInteractorRotation;

        float signedTwistDegrees = GetSignedTwistDegrees(delta, socketAxis.forward);
        if (Mathf.Abs(signedTwistDegrees) > maxDegreesPerFrame)
        {
            return 0f;
        }

        if (Mathf.Abs(signedTwistDegrees) > 0f)
        {
            transform.rotation = Quaternion.AngleAxis(signedTwistDegrees, socketAxis.forward) * transform.rotation;
        }

        return signedTwistDegrees;
    }

    private static float GetSignedTwistDegrees(Quaternion delta, Vector3 axis)
    {
        Vector3 projectedVector = Vector3.Project(new Vector3(delta.x, delta.y, delta.z), axis);
        if (projectedVector.sqrMagnitude + delta.w * delta.w < 0.000001f)
        {
            return 0f;
        }

        float radians = 2f * Mathf.Atan2(Vector3.Dot(projectedVector, axis), delta.w);
        return Mathf.DeltaAngle(0f, radians * Mathf.Rad2Deg);
    }

    private void AllowRemovalMovement()
    {
        isInstalled = false;
        positionLocked = false;

        if (hasSavedGrabSettings && grabInteractable != null)
        {
            grabInteractable.trackPosition = originalTrackPosition;
            grabInteractable.throwOnDetach = originalThrowOnDetach;
        }
    }

    private void RestoreGrabSettings()
    {
        positionLocked = false;
        rotationConstrained = false;
        rotationInteractor = null;
        if (!hasSavedGrabSettings || grabInteractable == null)
        {
            return;
        }

        grabInteractable.trackPosition = originalTrackPosition;
        grabInteractable.trackRotation = originalTrackRotation;
        grabInteractable.throwOnDetach = originalThrowOnDetach;
        hasSavedGrabSettings = false;
    }

    private bool IsWithinInsertionZone()
    {
        if (Vector3.Distance(bulbAxis.position, socketAxis.position) > insertionRadius)
        {
            return false;
        }

        return Vector3.Angle(bulbAxis.forward, socketAxis.forward) <= alignmentTolerance;
    }

    private void AccumulateTwist(Quaternion currentRotation)
    {
        Quaternion delta = currentRotation * Quaternion.Inverse(previousRotation);
        delta.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f)
        {
            angle = 360f - angle;
            axis = -axis;
        }

        float alignment = Vector3.Dot(axis, socketAxis.forward);
        if (Mathf.Abs(alignment) < 0.5f || angle > maxDegreesPerFrame)
        {
            return;
        }

        AccumulateTwist(angle * Mathf.Sign(alignment));
    }

    private void AccumulateTwist(float signedTwistDegrees)
    {
        float direction = Mathf.Sign(signedTwistDegrees) * (float)threadDirection;
        twistProgressDegrees = Mathf.Clamp(
            twistProgressDegrees + Mathf.Abs(signedTwistDegrees) * direction,
            0f,
            turnsRequired * 360f);
    }
}
