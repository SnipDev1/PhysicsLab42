using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Shared uGUI factory matching the main menu's translucent white card style and LiberationSans typography.
/// </summary>
public static class MainMenuUiFactory
{
    public static readonly Color ScrimColor = new Color(0f, 0f, 0f, 0.55f);
    public static readonly Color CardColor = new Color(1f, 1f, 1f, 0.88f);
    public static readonly Color ModalCardColor = new Color(1f, 1f, 1f, 0.94f);
    public static readonly Color BorderColor = new Color(0f, 0f, 0f, 0.16f);
    public static readonly Color DividerColor = new Color(0f, 0f, 0f, 0.14f);
    public static readonly Color TextPrimary = new Color(0.05f, 0.05f, 0.07f, 1f);
    public static readonly Color TextSecondary = new Color(0.26f, 0.28f, 0.34f, 1f);
    public static readonly Color AccentColor = new Color(0.18f, 0.25f, 0.65f, 1f);
    public static readonly Color SuccessColor = new Color(0.12f, 0.50f, 0.24f, 1f);
    public static readonly Color DangerColor = new Color(0.80f, 0.16f, 0.16f, 1f);
    public static readonly Color BtnNormalTransparent = new Color(1f, 1f, 1f, 0f);
    public static readonly Color BtnNormalSubtle = new Color(0f, 0f, 0f, 0.06f);
    public static readonly Color BtnHighlighted = new Color(0f, 0f, 0f, 0.10f);
    public static readonly Color BtnPressed = new Color(0f, 0f, 0f, 0.18f);

    private static TMP_FontAsset _cachedFont;

    public static TMP_FontAsset GetFont()
    {
        if (_cachedFont == null)
            _cachedFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF") ?? TMP_Settings.defaultFontAsset;
        return _cachedFont;
    }

    public static Canvas FindOrCreateCanvas()
    {
        GameObject byName = GameObject.Find("Canvas");
        Canvas canvas = byName != null ? byName.GetComponent<Canvas>() : null;
        if (canvas == null)
            canvas = Object.FindAnyObjectByType<Canvas>();

        if (canvas == null)
        {
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        if (!canvas.TryGetComponent<CanvasScaler>(out var scaler))
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (!canvas.TryGetComponent<GraphicRaycaster>(out _))
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        return canvas;
    }

    public static RectTransform CreatePanel(Transform parent, string name, Color bg, bool addBorder = true, bool raycast = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = bg;
        img.raycastTarget = raycast;

        if (addBorder)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = BorderColor;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        return rt;
    }

    public static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string text,
        float fontSize,
        FontStyles style,
        Color color,
        TextAlignmentOptions align,
        bool raycast = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = GetFont();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = raycast;
        return tmp;
    }

    public static Button CreateButton(
        Transform parent,
        string name,
        string label,
        float fontSize,
        UnityAction onClick,
        bool subtleBg = true,
        Color? textColor = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        // Button ColorBlock multiplies against Image.color, so Image.color must stay white
        var img = go.GetComponent<Image>();
        img.color = Color.white;
        img.raycastTarget = true;

        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        Color normal = subtleBg ? BtnNormalSubtle : BtnNormalTransparent;
        btn.colors = new ColorBlock
        {
            normalColor = normal,
            highlightedColor = BtnHighlighted,
            pressedColor = BtnPressed,
            selectedColor = normal,
            disabledColor = new Color(0f, 0f, 0f, 0.03f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };
        if (onClick != null)
            btn.onClick.AddListener(onClick);

        var tmp = CreateText(rt, "Text (TMP)", label, fontSize, FontStyles.Bold, textColor ?? TextPrimary, TextAlignmentOptions.Center, false);
        var tmpRt = tmp.rectTransform;
        tmpRt.anchorMin = Vector2.zero;
        tmpRt.anchorMax = Vector2.one;
        tmpRt.offsetMin = new Vector2(12f, 4f);
        tmpRt.offsetMax = new Vector2(-12f, -4f);

        return btn;
    }

    public static RectTransform CreateDivider(Transform parent, float height = 2f)
    {
        var rt = CreatePanel(parent, "Divider", DividerColor, false, false);
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
        return rt;
    }
}
