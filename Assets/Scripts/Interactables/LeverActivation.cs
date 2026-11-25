using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(HingeJoint))]
public class LeverActivation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HingeJoint hinge;
    [SerializeField] private Rigidbody leverBody;
    [SerializeField] private XRGrabInteractable grabInteractable;

    [Header("Locking")]
    [Tooltip("How close to the hinge's min limit the lever must get before it locks.")]
    [SerializeField] private float limitTolerance = 1f;

    [SerializeField]
    private bool isLocked;

    public UnityEvent onLocked;

    private void Awake()
    {
        if (hinge == null)
            hinge = GetComponent<HingeJoint>();

        if (leverBody == null)
            leverBody = GetComponent<Rigidbody>();

        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void FixedUpdate()
    {
        if (isLocked || hinge == null || leverBody == null)
            return;

        // Lock the lever as soon as it reaches the hinge's minimum angle.
        if (hinge.angle <= hinge.limits.min + limitTolerance)
            LockLever();
    }

    private void LockLever()
    {
        isLocked = true;
        onLocked?.Invoke();
        // Stop any residual physics motion.
        leverBody.linearVelocity = Vector3.zero;
        leverBody.angularVelocity = Vector3.zero;

        // Freeze the body so neither physics nor grabbing can move it.
        leverBody.constraints |= RigidbodyConstraints.FreezeAll;

        // Prevent the player from grabbing the lever again.
        if (grabInteractable != null)
            grabInteractable.enabled = false;
    }
}
