using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing;
using Weather = WeatherManager.Weather;

[RequireComponent(typeof(ClimbInteractable))]
public class ReactingHandsToWeather : MonoBehaviour
{
    [SerializeField] private WeatherManager weatherManager;
    [SerializeField] private float regrabCooldown = 1f;
    ClimbInteractable climbInteractable;
    bool wasHeld;
    bool isCoolingDown;
    float timeToFall = 5f; // Time in seconds before the player falls
    [SerializeField] private float fallTimer = 0f;
    private void Awake()
    {
        climbInteractable = GetComponent<ClimbInteractable>();
        climbInteractable.selectEntered.AddListener(OnSelectEntered);
    }

    private void OnDestroy()
    {
        climbInteractable.selectEntered.RemoveListener(OnSelectEntered);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        // Reset on every new hand grab, even if another hand still holds the ladder.
        fallTimer = 0f;
    }

    private void Update()
    {
        bool isHeld = climbInteractable.isSelected;

        if(isHeld && !wasHeld)
        {
            // The object has just been grabbed
            fallTimer = 0f;
            Debug.Log("Object grabbed");

        }
        else if (!isHeld && wasHeld)
        {
            // The object has just been released
            Debug.Log("Object released");
        }

        if (isHeld)
        {
            // The object is currently being held
            Weather currentWeather = weatherManager.GetCurrentWeather();

            // Windy movement is handled independently by WindSlipHand on each tracked hand.
            if(currentWeather.name == "Rainy")
            {
                // React to rainy weather
                fallTimer += Time.deltaTime;
                //Debug.Log("Reacting to Rainy Weather");
                if (fallTimer >= timeToFall)
                {
                    Debug.Log("Player is holding the object for too long in rainy weather");
                    LoseHold();
                }
            }
        }
        wasHeld = isHeld;
    }

    /// <summary>
    /// Forces the player to lose their hold on the climb interactable, triggering the hand animation to open and starting a cooldown before they can grab again.
    /// </summary>
    private void LoseHold()
    {
        if (isCoolingDown || !climbInteractable.isSelected)
        {
            return;
        }

        List<AnimateHandOnInput> handAnimations = new List<AnimateHandOnInput>();
        foreach (var interactor in climbInteractable.interactorsSelecting)
        {
            AnimateHandOnInput handAnimation = interactor.transform.GetComponentInChildren<AnimateHandOnInput>(true);
            if (handAnimation != null && !handAnimations.Contains(handAnimation))
            {
                handAnimation.forceOpen = true;
                handAnimations.Add(handAnimation);
            }
        }

        if (handAnimations.Count == 0)
        {
            Debug.LogWarning("No AnimateHandOnInput component was found on the selecting interactor or its children.");
        }

        climbInteractable.interactionManager.CancelInteractableSelection((IXRSelectInteractable)climbInteractable);
        isCoolingDown = true;
        StartCoroutine(WaitForRegrip(handAnimations));
    }

    /// <summary>
    /// Waits for the regrip cooldown and ensures that the player has released the grip before allowing them to grab again.
    /// </summary>
    /// <param name="handAnimations">The list of hand animations to wait for.</param>
    /// <returns></returns>
    private IEnumerator WaitForRegrip(List<AnimateHandOnInput> handAnimations)
    {
        climbInteractable.enabled = false;
        yield return new WaitForSeconds(regrabCooldown);

        while (handAnimations.Exists(handAnimation => handAnimation != null && handAnimation.IsGripPressed))
        {
            yield return null;
        }

        foreach (AnimateHandOnInput handAnimation in handAnimations)
        {
            if (handAnimation != null)
            {
                handAnimation.forceOpen = false;
            }
        }

        climbInteractable.enabled = true;
        isCoolingDown = false;
    }
}
