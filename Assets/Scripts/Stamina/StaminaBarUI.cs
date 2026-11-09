using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays <see cref="PlayerStamina"/> as a horizontal bar by resizing a fill rectangle.
/// Resizing the rect (instead of using Image.fillAmount) means no sprite is required.
/// </summary>
public sealed class StaminaBarUI : MonoBehaviour
{
    [SerializeField] PlayerStamina m_Stamina;

    [Tooltip("Rect that is stretched across its parent and shrunk horizontally to show the stamina level.")]
    [SerializeField] RectTransform m_Fill;

    [Tooltip("Optional. Tinted by the colors below.")]
    [SerializeField] Image m_FillImage;

    [SerializeField] Color m_NormalColor = new Color(0.25f, 0.8f, 0.3f);
    [SerializeField] Color m_ExhaustedColor = new Color(0.85f, 0.2f, 0.2f);

    void Awake()
    {
        if (m_Stamina == null || m_Fill == null)
        {
            Debug.LogError("StaminaBarUI needs a PlayerStamina and a fill RectTransform.", this);
            enabled = false;
        }
    }

    void OnEnable()
    {
        m_Stamina.StaminaChanged += OnStaminaChanged;
        m_Stamina.Exhausted += RefreshColor;
        m_Stamina.Recovered += RefreshColor;

        OnStaminaChanged(m_Stamina.Current, m_Stamina.Max);
        RefreshColor();
    }

    void OnDisable()
    {
        // Awake can disable this component before OnEnable ever ran.
        if (m_Stamina == null)
            return;

        m_Stamina.StaminaChanged -= OnStaminaChanged;
        m_Stamina.Exhausted -= RefreshColor;
        m_Stamina.Recovered -= RefreshColor;
    }

    void OnStaminaChanged(float current, float max)
    {
        float normalized = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        m_Fill.anchorMin = Vector2.zero;
        m_Fill.anchorMax = new Vector2(normalized, 1f);
    }

    void RefreshColor()
    {
        if (m_FillImage != null)
            m_FillImage.color = m_Stamina.IsExhausted ? m_ExhaustedColor : m_NormalColor;
    }
}
