using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSocketInteractor))]
public sealed class BulbSocket : MonoBehaviour, IXRSelectFilter
{
    [SerializeField] private XRSocketInteractor socketInteractor;
    [SerializeField] private Light bulbLight;
    [SerializeField] private Renderer[] bulbEmitters;
    [SerializeField] private Color emissionColor = new Color(1f, 0.75f, 0.4f);
    [SerializeField, Min(0f)] private float emissionIntensity = 2f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock propertyBlock;
    private ScrewableBulb trackedBulb;

    public bool canProcess => isActiveAndEnabled;

    private void Awake()
    {
        if (socketInteractor == null)
        {
            socketInteractor = GetComponent<XRSocketInteractor>();
        }

        if (socketInteractor == null || bulbLight == null)
        {
            Debug.LogError("BulbSocket requires an XRSocketInteractor and a Light.", this);
            enabled = false;
            return;
        }

        propertyBlock = new MaterialPropertyBlock();
        SetLighting(false);
    }

    private void Update()
    {
        if (trackedBulb != null && !trackedBulb.IsInstalled)
        {
            trackedBulb = null;
            SetLighting(false);
        }
    }

    private void OnEnable()
    {
        if (socketInteractor == null)
        {
            return;
        }

        socketInteractor.selectFilters.Add(this);

        socketInteractor.selectEntered.AddListener(OnSocketSelectEntered);
        socketInteractor.selectExited.AddListener(OnSocketSelectExited);
    }

    private void OnDisable()
    {
        if (socketInteractor != null)
        {
            socketInteractor.selectFilters.Remove(this);
            socketInteractor.selectEntered.RemoveListener(OnSocketSelectEntered);
            socketInteractor.selectExited.RemoveListener(OnSocketSelectExited);
        }

        SetLighting(false);
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        if (!ReferenceEquals(interactor, socketInteractor))
        {
            return true;
        }

        ScrewableBulb bulb = interactable.transform.GetComponentInParent<ScrewableBulb>();
        return bulb != null && bulb.CanBeStaged;
    }

    private void OnSocketSelectEntered(SelectEnterEventArgs args)
    {
        ScrewableBulb bulb = args.interactableObject.transform.GetComponentInParent<ScrewableBulb>();
        trackedBulb = bulb;
        if (bulb != null && bulb.CanBeSocketed)
        {
            bulb.MarkInstalled();
        }

        SetLighting(bulb != null && bulb.CanBeSocketed);
    }

    private void OnSocketSelectExited(SelectExitEventArgs args)
    {
        ScrewableBulb bulb = args.interactableObject.transform.GetComponentInParent<ScrewableBulb>();
        if (bulb == null || !bulb.IsInstalled)
        {
            trackedBulb = null;
            SetLighting(false);
        }
    }

    private void SetLighting(bool isOn)
    {
        if (bulbLight != null)
        {
            bulbLight.enabled = isOn;
        }

        if (bulbEmitters == null || propertyBlock == null)
        {
            return;
        }

        Color color = isOn ? emissionColor * emissionIntensity : Color.black;
        foreach (Renderer bulbEmitter in bulbEmitters)
        {
            if (bulbEmitter == null)
            {
                continue;
            }

            bulbEmitter.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorId, color);
            bulbEmitter.SetPropertyBlock(propertyBlock);
        }
    }
}
