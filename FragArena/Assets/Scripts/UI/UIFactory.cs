using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Builds the interface in code. Every screen in the game uses this, so no UI
/// has to be assembled by hand in the Unity editor.
/// </summary>
public static class UIFactory
{
    static Font cachedFont;

    public static Font DefaultFont
    {
        get
        {
            if (cachedFont == null)
                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cachedFont == null)
                cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return cachedFont;
        }
    }

    /// <summary>Buttons and sliders do nothing without an EventSystem in the scene.</summary>
    public static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    public static Canvas CreateCanvas(string name, int sortOrder)
    {
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static RectTransform Place(GameObject go, Vector2 anchor, Vector2 pivot,
                                      Vector2 position, Vector2 size)
    {
        var rect = go.GetComponent<RectTransform>();
        if (rect == null) rect = go.AddComponent<RectTransform>();

        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public static RectTransform Stretch(GameObject go)
    {
        var rect = go.GetComponent<RectTransform>();
        if (rect == null) rect = go.AddComponent<RectTransform>();

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static Image CreateImage(Transform parent, string name, Color color,
                                    Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.color = color;

        Place(go, anchor, pivot, position, size);
        return image;
    }

    public static Text CreateText(Transform parent, string name, string content, int fontSize,
                                  TextAnchor alignment, Color color,
                                  Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<Text>();
        text.text = content;
        text.font = DefaultFont;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        Place(go, anchor, pivot, position, size);
        return text;
    }

    public static Button CreateButton(Transform parent, string name, string label, int fontSize,
                                      Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size,
                                      UnityAction onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.color = new Color(0.16f, 0.45f, 0.75f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        if (onClick != null) button.onClick.AddListener(onClick);

        Place(go, anchor, pivot, position, size);

        var labelText = CreateText(go.transform, "Label", label, fontSize, TextAnchor.MiddleCenter,
                                   Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                   Vector2.zero, size);
        Stretch(labelText.gameObject);

        return button;
    }

    public static Slider CreateSlider(Transform parent, string name, float min, float max, float value,
                                      bool wholeNumbers, Vector2 anchor, Vector2 pivot,
                                      Vector2 position, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var slider = go.AddComponent<Slider>();
        Place(go, anchor, pivot, position, size);

        Image background = CreateImage(go.transform, "Background", new Color(0.18f, 0.18f, 0.20f),
                                       new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        Stretch(background.gameObject);

        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(go.transform, false);
        RectTransform fillAreaRect = Stretch(fillArea);
        fillAreaRect.offsetMin = new Vector2(5f, 0f);
        fillAreaRect.offsetMax = new Vector2(-5f, 0f);

        Image fill = CreateImage(fillArea.transform, "Fill", new Color(0.30f, 0.65f, 0.95f),
                                 new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        RectTransform fillRect = Stretch(fill.gameObject);

        var handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(go.transform, false);
        RectTransform handleAreaRect = Stretch(handleArea);
        handleAreaRect.offsetMin = new Vector2(10f, 0f);
        handleAreaRect.offsetMax = new Vector2(-10f, 0f);

        Image handle = CreateImage(handleArea.transform, "Handle", Color.white,
                                   new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                   Vector2.zero, new Vector2(24f, 0f));
        RectTransform handleRect = handle.rectTransform;
        handleRect.anchorMin = new Vector2(0f, 0f);
        handleRect.anchorMax = new Vector2(0f, 1f);
        handleRect.sizeDelta = new Vector2(24f, 0f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.wholeNumbers = wholeNumbers;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;

        return slider;
    }
}
