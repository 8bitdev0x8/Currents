using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Builds the runtime controls with crisp TextMeshPro labels.</summary>
public sealed class FluidCurrentOverlayUI : MonoBehaviour
{
    private sealed class SliderBinding
    {
        public Slider slider;
        public TMP_Text valueText;
        public Func<float> read;
        public Action<float> write;
        public bool logarithmic;
    }

    private readonly List<SliderBinding> bindings = new List<SliderBinding>();
    private FluidCurrentDemo demo;
    private GameObject canvasObject;
    private GameObject panel;
    private TMP_Text panelToggleText;
    private TMP_Text fpsText;
    private Toggle rainbowToggle;
    private TMP_FontAsset fallbackFont;
    private Font fallbackSystemFont;
    private bool open;
    private FontStyles labelFontStyle = FontStyles.Bold;

    public void Initialize(FluidCurrentDemo controller)
    {
        if (panel != null) return;
        demo = controller;
        Build();
    }

    public void RefreshValues()
    {
        for (int i = 0; i < bindings.Count; i++)
        {
            SliderBinding binding = bindings[i];
            float value = binding.read();
            binding.slider.SetValueWithoutNotify(binding.logarithmic ? Mathf.Log10(Mathf.Max(value, 0.0001f)) : value);
            binding.valueText.text = value.ToString("0.###");
        }
        if (rainbowToggle != null) rainbowToggle.SetIsOnWithoutNotify(demo.rainbowEnabled);
    }

    private void Update()
    {
        if (fpsText != null) fpsText.text = $"{demo.DisplayedFps:0} FPS";
    }

    private void Build()
    {
        EnsureEventSystem();
        canvasObject = new GameObject("Currents TMP Overlay", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.matchWidthOrHeight = 0.5f;

        Button openButton = MakeButton(canvas.transform, "Controls", new Vector2(12f, -12f),
            new Vector2(330f, 38f), new Color(0.035f, 0.025f, 0.055f, 0.97f), 13f, TextAnchor.MiddleLeft);
        panelToggleText = openButton.GetComponentInChildren<TMP_Text>();
        panelToggleText.text = "CURRENTS     /     CONTROLS     +";
        openButton.onClick.AddListener(TogglePanel);

        Image fpsCard = MakeImage(canvas.transform, "FPS Card", new Vector2(-12f, -12f),
            new Vector2(108f, 34f), new Color(0.035f, 0.025f, 0.055f, 0.97f),
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        fpsText = MakeText(fpsCard.transform, "FPS", "0 FPS", 13f,
            new Color(1f, 0.82f, 0.46f), TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(fpsText.rectTransform, 8f, 0f, -8f, 0f);

        panel = MakePanel(canvas.transform);
        panel.SetActive(false);
        BuildPanelContents();
    }

    private GameObject MakePanel(Transform parent)
    {
        Image image = MakeImage(parent, "Controls Panel", new Vector2(12f, -56f),
            new Vector2(330f, Mathf.Min(560f, Mathf.Max(150f, Screen.height - 76f))),
            new Color(0.035f, 0.025f, 0.055f, 0.97f),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(12f, -56f);
        return image.gameObject;
    }

    private void BuildPanelContents()
    {
        Button restore = MakeButton(panel.transform, "Restore Defaults", new Vector2(-10f, -10f),
            new Vector2(126f, 25f), new Color(1f, 0.69f, 0.26f), 9f, TextAnchor.MiddleCenter);
        RectTransform restoreRect = restore.GetComponent<RectTransform>();
        restoreRect.anchorMin = new Vector2(1f, 1f);
        restoreRect.anchorMax = new Vector2(1f, 1f);
        restoreRect.pivot = new Vector2(1f, 1f);
        restoreRect.anchoredPosition = new Vector2(-12f, -10f);
        restore.onClick.AddListener(() =>
        {
            demo.RestoreDefaultValues();
            RefreshValues();
        });
        TMP_Text restoreText = restore.GetComponentInChildren<TMP_Text>();
        restoreText.color = new Color(0.08f, 0.035f, 0.01f);

        TMP_Text title = MakeText(panel.transform, "Panel Title", "LIVE SIMULATION PARAMETERS",
            10f, new Color(1f, 0.69f, 0.26f), TextAlignmentOptions.Left, labelFontStyle);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.offsetMin = new Vector2(16f, -32f);
        titleRect.offsetMax = new Vector2(-150f, -10f);

        RectTransform viewport = CreateRect("Parameter Viewport", panel.transform);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(16f, 42f);
        viewport.offsetMax = new Vector2(-12f, -42f);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        ScrollRect scroll = panel.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        RectTransform content = CreateRect("Parameter Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, 100f);
        scroll.content = content;
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(2, 2, 4, 8);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        AddRainbowToggle(content);
        AddSection(content, "ADJUST SETTINGS");
        if (demo.showFlowVelocityInOverlay) AddSlider(content, "Flow velocity", () => demo.flowVelocity, v => demo.flowVelocity = v, 0.1f, 4f);
        if (demo.showViscosityInOverlay) AddSlider(content, "Kinematic viscosity", () => demo.kinematicViscosity, v => demo.kinematicViscosity = v, 0.001f, 0.15f);
        if (demo.showWakeStrengthInOverlay) AddSlider(content, "Wake strength", () => demo.wakeStrength, v => demo.wakeStrength = v, 0f, 2f);
        if (demo.showWakeViolenceInOverlay) AddSlider(content, "Wake violence", () => demo.wakeViolence, v => demo.wakeViolence = v, 0.5f, 5f);
        if (demo.showLineFrequencyInOverlay) AddSlider(content, "Line frequency", () => demo.lineFrequency, v => demo.lineFrequency = v, 1f, 12f);
        if (demo.showLineWidthInOverlay) AddSlider(content, "Line width", () => demo.lineWidth, v => demo.lineWidth = v, 0.005f, 0.2f);
        if (demo.showRedLineWidthInOverlay) AddSlider(content, "Red line width", () => demo.redLineWidth, v => demo.redLineWidth = v, 0.02f, 0.4f);
        if (demo.showBlackLineWidthInOverlay) AddSlider(content, "Black line width", () => demo.blackLineWidth, v => demo.blackLineWidth = v, 0f, 0.2f);
        if (demo.showEdgeFadeInOverlay) AddSlider(content, "Edge fade distance", () => demo.edgeFadeDistance, v => demo.edgeFadeDistance = v, 1f, 40f);
        if (demo.showRainbowFadeInOverlay) AddSlider(content, "Rainbow wake fade", () => demo.rainbowFadeDistance, v => demo.rainbowFadeDistance = v, 8f, 60f);
        if (demo.showRainbowCycleInOverlay) AddSlider(content, "Rainbow cycle length", () => demo.rainbowCycleLength, v => demo.rainbowCycleLength = v, 4f, 40f);
        if (demo.showSimulationRateInOverlay) AddSlider(content, "Simulation rate", () => demo.simulationRate, v => demo.simulationRate = v, 15f, 60f);
        if (demo.showFlowRecoveryInOverlay) AddSlider(content, "Flow recovery", () => demo.normalFlowRecovery, v => demo.normalFlowRecovery = v,
            Mathf.Log10(0.0001f), Mathf.Log10(2f), true);
        AddSection(content, "RIGHT-DRAG SPHERE   ·   LEFT-DRAG FLOW");
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        RefreshValues();
    }

    private void AddRainbowToggle(Transform parent)
    {
        RectTransform row = CreateRow(parent, 34f);
        TMP_Text label = MakeText(row, "Rainbow Label", "RAINBOW WAKE", 10f,
            new Color(0.79f, 0.72f, 0.85f), TextAlignmentOptions.Left, FontStyles.Bold);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(4f, 0f);
        labelRect.offsetMax = new Vector2(-60f, 0f);

        GameObject toggleObject = new GameObject("Rainbow Enabled", typeof(RectTransform), typeof(Image), typeof(Toggle));
        toggleObject.transform.SetParent(row, false);
        RectTransform toggleRect = toggleObject.GetComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(1f, 0.5f);
        toggleRect.anchorMax = new Vector2(1f, 0.5f);
        toggleRect.pivot = new Vector2(1f, 0.5f);
        toggleRect.sizeDelta = new Vector2(22f, 22f);
        Image toggleBackground = toggleObject.GetComponent<Image>();
        toggleBackground.color = new Color(0.18f, 0.12f, 0.23f);
        GameObject checkObject = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        checkObject.transform.SetParent(toggleObject.transform, false);
        RectTransform checkRect = checkObject.GetComponent<RectTransform>();
        checkRect.anchorMin = Vector2.zero;
        checkRect.anchorMax = Vector2.one;
        checkRect.offsetMin = new Vector2(4f, 4f);
        checkRect.offsetMax = new Vector2(-4f, -4f);
        Image checkImage = checkObject.GetComponent<Image>();
        checkImage.color = new Color(1f, 0.69f, 0.26f);
        rainbowToggle = toggleObject.GetComponent<Toggle>();
        rainbowToggle.targetGraphic = toggleBackground;
        rainbowToggle.graphic = checkImage;
        rainbowToggle.isOn = demo.rainbowEnabled;
        rainbowToggle.onValueChanged.AddListener(enabled => demo.rainbowEnabled = enabled);
    }

    private void AddSlider(Transform parent, string label, Func<float> read, Action<float> write,
        float minimum, float maximum, bool logarithmic = false)
    {
        RectTransform row = CreateRow(parent, 50f);
        TMP_Text labelText = MakeText(row, label + " Label", label.ToUpperInvariant(), 9f,
            new Color(0.79f, 0.72f, 0.85f), TextAlignmentOptions.Left, FontStyles.Bold);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0.52f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(4f, 0f);
        labelRect.offsetMax = new Vector2(-64f, 0f);

        TMP_Text valueText = MakeText(row, label + " Value", read().ToString("0.###"), 9f,
            new Color(0.98f, 0.93f, 1f), TextAlignmentOptions.Right, FontStyles.Bold);
        RectTransform valueRect = valueText.rectTransform;
        valueRect.anchorMin = new Vector2(1f, 0.52f);
        valueRect.anchorMax = Vector2.one;
        valueRect.pivot = Vector2.one;
        valueRect.sizeDelta = new Vector2(60f, 20f);
        valueRect.anchoredPosition = new Vector2(-2f, 0f);

        GameObject sliderObject = new GameObject(label + " Slider", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(row, false);
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 0.55f);
        sliderRect.offsetMin = new Vector2(4f, 2f);
        sliderRect.offsetMax = new Vector2(-4f, -1f);
        Image track = MakeImage(sliderObject.transform, "Track", Vector2.zero, Vector2.zero,
            new Color(0.18f, 0.12f, 0.23f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        Stretch(track.rectTransform, 0f, 3f, 0f, -3f);
        Image fill = MakeImage(sliderObject.transform, "Fill", Vector2.zero, Vector2.zero,
            new Color(0.64f, 0.38f, 0.82f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(0f, 3f);
        fillRect.offsetMax = new Vector2(0f, -3f);
        Image handle = MakeImage(sliderObject.transform, "Handle", Vector2.zero, new Vector2(14f, 14f),
            new Color(1f, 0.69f, 0.26f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.fillRect = fillRect;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.onValueChanged.AddListener(value =>
        {
            float actual = logarithmic ? Mathf.Pow(10f, value) : value;
            write(actual);
            valueText.text = actual.ToString("0.###");
        });

        bindings.Add(new SliderBinding { slider = slider, valueText = valueText, read = read, write = write, logarithmic = logarithmic });
    }

    private void AddSection(Transform parent, string text)
    {
        TMP_Text section = MakeText(parent, "Section", text, 9f,
            new Color(1f, 0.69f, 0.26f), TextAlignmentOptions.Left, FontStyles.Bold);
        LayoutElement layout = section.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 20f;
    }

    private RectTransform CreateRow(Transform parent, float height)
    {
        RectTransform row = CreateRect("Parameter Row", parent);
        LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.minHeight = height;
        return row;
    }

    private Button MakeButton(Transform parent, string name, Vector2 anchoredPosition,
        Vector2 size, Color color, float fontSize, TextAnchor alignment)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        TMP_Text text = MakeText(buttonObject.transform, name + " Text", name.ToUpperInvariant(), fontSize,
            new Color(0.95f, 0.82f, 1f), ToTMPAlignment(alignment), FontStyles.Bold);
        Stretch(text.rectTransform, 12f, 2f, -8f, -2f);
        if (alignment == TextAnchor.MiddleCenter) Stretch(text.rectTransform, 2f, 2f, -2f, -2f);
        return button;
    }

    private Image MakeImage(Transform parent, string name, Vector2 anchoredPosition, Vector2 size,
        Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private TMP_Text MakeText(Transform parent, string name, string value, float size,
        Color color, TextAlignmentOptions alignment, FontStyles style)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.raycastTarget = false;
        text.font = ResolveFont();
        return text;
    }

    private TMP_FontAsset ResolveFont()
    {
        // The static defaultFontAsset property dereferences TMP_Settings.instance
        // without checking whether the TMP Settings resource exists in this project.
        // Use the guarded accessor so the runtime built-in font fallback can be created.
        TMP_FontAsset configuredFont = TMP_Settings.GetFontAsset();
        if (configuredFont != null) return configuredFont;
        if (fallbackFont != null) return fallbackFont;
        fallbackSystemFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (fallbackSystemFont == null) return null;
        fallbackFont = TMP_FontAsset.CreateFontAsset(fallbackSystemFont);
        if (fallbackFont != null) fallbackFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        return fallbackFont;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
    }

    private void TogglePanel()
    {
        open = !open;
        panel.SetActive(open);
        panelToggleText.text = open ? "CURRENTS     /     CONTROLS     −" : "CURRENTS     /     CONTROLS     +";
    }

    private static TextAlignmentOptions ToTMPAlignment(TextAnchor alignment)
    {
        return alignment == TextAnchor.MiddleCenter ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void OnDestroy()
    {
        if (canvasObject != null)
        {
            if (Application.isPlaying) Destroy(canvasObject);
            else DestroyImmediate(canvasObject);
        }
        if (fallbackFont != null)
        {
            if (Application.isPlaying) Destroy(fallbackFont);
            else DestroyImmediate(fallbackFont);
        }
    }
}
