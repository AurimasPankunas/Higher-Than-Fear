using UnityEngine;
using UnityEngine.InputSystem;
public class AnimateHandOnInput : MonoBehaviour
{
    public InputActionProperty triggerValue;
    public InputActionProperty gripValue;

    public Animator handAnimator;
    // Lets a forced slip show an open hand even while the controller grip remains held.
    public bool forceOpen;
    // Use the physical grip action to decide when the slipped hand may re-grab.
    public bool IsGripPressed => gripValue.action != null && gripValue.action.ReadValue<float>() > 0.1f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float trigger = triggerValue.action.ReadValue<float>();
        // Override only the visual animation; this does not change the controller's actual input.
        float grip = forceOpen ? 0f : gripValue.action.ReadValue<float>();

        Debug.Log($"Trigger value: {trigger}");
        handAnimator.SetFloat("Trigger", trigger);
        Debug.Log($"Grip value: {grip}");
        handAnimator.SetFloat("Grip", grip);
    }
}
