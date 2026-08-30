using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the placeholder board out of plain UI primitives, so the level is
/// playable long before any card art exists. Everything here is meant to be
/// replaced by prefabs once the real art lands — nothing else depends on it.
/// </summary>
public static class SolitaireUi
{
    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");

        // Set every anchor explicitly: a RectTransform built in code does not come
        // out of the box the way one added through the GameObject menu does.
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, worldPositionStays: false);
        rect.localScale = Vector3.one;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(100f, 100f);
        return rect;
    }

    /// <summary>A rect that fills its parent, for board and HUD roots.</summary>
    public static RectTransform Stretch(string name, Transform parent)
    {
        var rect = Rect(name, parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static Image Panel(string name, Transform parent, Vector2 size, Color color)
    {
        var rect = Rect(name, parent);
        rect.sizeDelta = size;

        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static TMP_Text Label(string name, Transform parent, string text, float fontSize, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var rect = Rect(name, parent);

        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    public static Button Button(string name, Transform parent, string text, Vector2 size)
    {
        var image = Panel(name, parent, size, new Color(0.16f, 0.16f, 0.22f, 0.9f));

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var label = Label("Label", image.transform, text, 36f, Color.white);
        var rect = (RectTransform)label.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return button;
    }
}
