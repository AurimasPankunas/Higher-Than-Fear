using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing;

/// <summary>
/// Keeps the rendered hand model attached to a climbable while its interactor selects it
/// </summary>
public sealed class ClimbingHandVisualLatch : MonoBehaviour
{
    // Interactor whose grab and release events control the visual latch
    [SerializeField] XRBaseInteractor m_Interactor;

    // Transform of the rendered hand model
    [SerializeField] Transform m_VisualRoot;

    // Time in seconds for the visual hand to blend back to its tracked pose after release.
    [SerializeField, Min(0f)] float m_ReleaseBlendDuration = 0.2f;

    // The models default pose relative to its parent, restored after releasing a climbable
    Vector3 m_OriginalLocalPosition;
    Quaternion m_OriginalLocalRotation;

    // Climbable currently holding this visual hand in place
    ClimbInteractable m_ClimbInteractable;

    // The hand models latched pose relative to the climbables climb transform
    Vector3 m_LatchedLocalPosition;
    Quaternion m_LatchedLocalRotation;

    // True while the visual hand is being held at its saved climbable pose
    bool m_IsLatched;

    // Pose to blend from when the hand is released.
    Vector3 m_ReleaseStartPosition;
    Quaternion m_ReleaseStartRotation;

    // Time elapsed since the release blend began.
    float m_ReleaseBlendElapsed;

    // Whether the visual is currently blending back to controller tracking.
    bool m_IsBlendingToTrackedPose;

    /// <summary>
    /// Finds the interactor if needed and saves the hand models default local pose
    /// Disables this component if a required reference is missing
    /// </summary>
    void Awake()
    {
        if (m_Interactor == null)
            m_Interactor = GetComponent<XRBaseInteractor>();

        if (m_Interactor == null || m_VisualRoot == null)
        {
            Debug.LogError(
                "ClimbingHandVisualLatch needs an interactor and a hand visual root.",
                this);
            enabled = false;
            return;
        }

        m_OriginalLocalPosition = m_VisualRoot.localPosition;
        m_OriginalLocalRotation = m_VisualRoot.localRotation;
    }

    /// <summary>
    /// Subscribes to the interactors grab and release events
    /// </summary>
    void OnEnable()
    {
        if (m_Interactor == null)
            return;

        m_Interactor.selectEntered.AddListener(OnSelectEntered);
        m_Interactor.selectExited.AddListener(OnSelectExited);
    }

    /// <summary>
    /// Unsubscribes from interaction events and restores the tracked hand pose
    /// </summary>
    void OnDisable()
    {
        if (m_Interactor != null)
        {
            m_Interactor.selectEntered.RemoveListener(OnSelectEntered);
            m_Interactor.selectExited.RemoveListener(OnSelectExited);
        }

        RestoreTrackedPose();
    }

    /// <summary>
    /// Saves the visual hands pose relative to the selected climbable
    /// </summary>
    /// <param name="args">Details of the selection event, including the selected interactable</param>
    void OnSelectEntered(SelectEnterEventArgs args)
    {
        // Only latch the hand when the selected object supports XRI climbing.
        ClimbInteractable climbInteractable =
            args.interactableObject as ClimbInteractable;

        if (climbInteractable == null)
            return;

        m_ClimbInteractable = climbInteractable;
        m_IsBlendingToTrackedPose = false;

        // The climb transform is the reference space used to keep the hand fixed to the climbable
        Transform anchor = climbInteractable.climbTransform;
        m_LatchedLocalPosition = anchor.InverseTransformPoint(m_VisualRoot.position);
        m_LatchedLocalRotation =
            Quaternion.Inverse(anchor.rotation) * m_VisualRoot.rotation;

        m_IsLatched = true;
    }

    /// <summary>
    /// Applies the saved climbable pose after normal tracking updates each frame
    /// </summary>
    void LateUpdate()
    {
        if (m_IsLatched)
        {
            if (m_ClimbInteractable == null)
            {
                RestoreTrackedPose();
                return;
            }

            // Convert the saved local pose back to world space as the climbable moves
            Transform anchor = m_ClimbInteractable.climbTransform;
            m_VisualRoot.SetPositionAndRotation(
                anchor.TransformPoint(m_LatchedLocalPosition),
                anchor.rotation * m_LatchedLocalRotation);
            return;
        }

        if (!m_IsBlendingToTrackedPose)
            return;

        m_ReleaseBlendElapsed += Time.deltaTime;
        // Normalize elapsed time to the configured duration and ease the transition
        float blendProgress = m_ReleaseBlendDuration <= 0f
            ? 1f
            : Mathf.Clamp01(m_ReleaseBlendElapsed / m_ReleaseBlendDuration);
        float easedProgress = Mathf.SmoothStep(0f, 1f, blendProgress);

        // Recalculate the target every frame because the tracked controller keeps moving
        Transform visualParent = m_VisualRoot.parent;
        Vector3 trackedPosition = visualParent != null
            ? visualParent.TransformPoint(m_OriginalLocalPosition)
            : m_OriginalLocalPosition;
        Quaternion trackedRotation = visualParent != null
            ? visualParent.rotation * m_OriginalLocalRotation
            : m_OriginalLocalRotation;

        m_VisualRoot.SetPositionAndRotation(
            Vector3.Lerp(m_ReleaseStartPosition, trackedPosition, easedProgress),
            Quaternion.Slerp(m_ReleaseStartRotation, trackedRotation, easedProgress));

        if (blendProgress >= 1f)
            RestoreTrackedPose();
    }

    /// <summary>
    /// Restores normal tracking when this interactor releases the latched climbable
    /// </summary>
    /// <param name="args">Details of the release event, including the released interactable</param>
    void OnSelectExited(SelectExitEventArgs args)
    {
        if (m_IsLatched && args.interactableObject == m_ClimbInteractable)
        {
            BeginReleaseBlend();
        }
    }

    /// <summary>
    /// Starts a smooth transition from the latched pose back to the tracked hand.
    /// </summary>
    void BeginReleaseBlend()
    {
        m_IsLatched = false;
        m_ClimbInteractable = null;

        if (m_VisualRoot == null || m_ReleaseBlendDuration <= 0f)
        {
            RestoreTrackedPose();
            return;
        }

        m_ReleaseStartPosition = m_VisualRoot.position;
        m_ReleaseStartRotation = m_VisualRoot.rotation;
        m_ReleaseBlendElapsed = 0f;
        m_IsBlendingToTrackedPose = true;
    }

    /// <summary>
    /// Clears the latch and returns the visual hand to its saved local pose
    /// </summary>
    void RestoreTrackedPose()
    {
        m_IsLatched = false;
        m_IsBlendingToTrackedPose = false;
        m_ClimbInteractable = null;

        if (m_VisualRoot == null)
            return;

        m_VisualRoot.localPosition = m_OriginalLocalPosition;
        m_VisualRoot.localRotation = m_OriginalLocalRotation;
    }
}