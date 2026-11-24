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

    public static Slider CreateSlider(
        Transform parent,
        string name,
        float minValue,
        float maxValue,
        float initialValue,
        UnityAction<float> onValueChanged)
    {
        var sliderGo = new GameObject(name, typeof(RectTransform), typeof(Slider));
        var sliderRt = sliderGo.GetComponent<RectTransform>();
        sliderRt.SetParent(parent, false);

        var slider = sliderGo.GetComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.wholeNumbers = false;

        var bgRt = CreatePanel(sliderRt, "Background", new Color(0f, 0f, 0f, 0.12f), false, true);
        bgRt.anchorMin = new Vector2(0f, 0.35f);
        bgRt.anchorMax = new Vector2(1f, 0.65f);
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
        var fillAreaRt = fillAreaGo.GetComponent<RectTransform>();
        fillAreaRt.SetParent(sliderRt, false);
        fillAreaRt.anchorMin = new Vector2(0f, 0.35f);
        fillAreaRt.anchorMax = new Vector2(1f, 0.65f);
        fillAreaRt.offsetMin = Vector2.zero;
        fillAreaRt.offsetMax = Vector2.zero;

        var fillRt = CreatePanel(fillAreaRt, "Fill", AccentColor, false, false);
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;

        var handleAreaGo = new GameObject("Handle Slide Area", typeof(RectTransform));
        var handleAreaRt = handleAreaGo.GetComponent<RectTransform>();
        handleAreaRt.SetParent(sliderRt, false);
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.offsetMin = new Vector2(12f, 0f);
        handleAreaRt.offsetMax = new Vector2(-12f, 0f);

        var handleRt = CreatePanel(handleAreaRt, "Handle", Color.white, true, true);
        handleRt.sizeDelta = new Vector2(24f, 24f);

        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleRt.GetComponent<Image>();
        slider.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(0.92f, 0.94f, 1f),
            pressedColor = new Color(0.78f, 0.85f, 1f),
            selectedColor = Color.white,
            disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.5f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };

        slider.value = initialValue;
        if (onValueChanged != null)
            slider.onValueChanged.AddListener(onValueChanged);

        return slider;
    }

    public static (Slider slider, TextMeshProUGUI valueText) CreateSliderRow(
        Transform parent,
        string name,
        string label,
        float minValue,
        float maxValue,
        float step,
        float initialValue,
        string valueFormat,
        UnityAction<float> onValueChanged)
    {
        var rowGo = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
        var rowRt = rowGo.GetComponent<RectTransform>();
        rowRt.SetParent(parent, false);

        var vl = rowGo.GetComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(0, 0, 0, 0);
        vl.spacing = 6f;
        vl.childControlWidth = true;
        vl.childControlHeight = true;
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;

        var topGo = new GameObject("TopLine", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var topRt = topGo.GetComponent<RectTransform>();
        topRt.SetParent(rowRt, false);
        var hlTop = topGo.GetComponent<HorizontalLayoutGroup>();
        hlTop.childControlWidth = true;
        hlTop.childControlHeight = true;
        hlTop.childForceExpandWidth = false;
        hlTop.childForceExpandHeight = false;

        var lbl = CreateText(topRt, "Label", label, 19f, FontStyles.Bold, TextPrimary, TextAlignmentOptions.MidlineLeft);
        var lblLe = lbl.gameObject.AddComponent<LayoutElement>();
        lblLe.flexibleWidth = 1f;

        var valTmp = CreateText(topRt, "Value", string.Format(valueFormat, initialValue), 19f, FontStyles.Bold, AccentColor, TextAlignmentOptions.MidlineRight);
        var valLe = valTmp.gameObject.AddComponent<LayoutElement>();
        valLe.preferredWidth = 100f;

        var controlsGo = new GameObject("ControlsLine", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var controlsRt = controlsGo.GetComponent<RectTransform>();
        controlsRt.SetParent(rowRt, false);
        var hlControls = controlsGo.GetComponent<HorizontalLayoutGroup>();
        hlControls.spacing = 10f;
        hlControls.childAlignment = TextAnchor.MiddleCenter;
        hlControls.childControlWidth = true;
        hlControls.childControlHeight = true;
        hlControls.childForceExpandWidth = false;
        hlControls.childForceExpandHeight = false;

        var rowLe = rowGo.AddComponent<LayoutElement>();
        rowLe.preferredHeight = 72f;

        Slider slider = null;

        Button minusBtn = CreateButton(controlsRt, "MinusBtn", "−", 20f, null, true, TextPrimary);
        var minusLe = minusBtn.gameObject.AddComponent<LayoutElement>();
        minusLe.preferredWidth = 44f;
        minusLe.preferredHeight = 34f;

        var sliderContainer = new GameObject("SliderContainer", typeof(RectTransform));
        var scRt = sliderContainer.GetComponent<RectTransform>();
        scRt.SetParent(controlsRt, false);
        var scLe = sliderContainer.AddComponent<LayoutElement>();
        scLe.flexibleWidth = 1f;
        scLe.preferredHeight = 34f;

        slider = CreateSlider(scRt, "Slider", minValue, maxValue, initialValue, (val) =>
        {
            valTmp.text = string.Format(valueFormat, val);
            onValueChanged?.Invoke(val);
        });
        var sRt = slider.GetComponent<RectTransform>();
        sRt.anchorMin = Vector2.zero;
        sRt.anchorMax = Vector2.one;
        sRt.offsetMin = Vector2.zero;
        sRt.offsetMax = Vector2.zero;

        Button plusBtn = CreateButton(controlsRt, "PlusBtn", "+", 20f, null, true, TextPrimary);
        var plusLe = plusBtn.gameObject.AddComponent<LayoutElement>();
        plusLe.preferredWidth = 44f;
        plusLe.preferredHeight = 34f;

        minusBtn.onClick.AddListener(() =>
        {
            slider.value = Mathf.Clamp(slider.value - step, minValue, maxValue);
        });

        plusBtn.onClick.AddListener(() =>
        {
            slider.value = Mathf.Clamp(slider.value + step, minValue, maxValue);
        });

        return (slider, valTmp);
    }
}
