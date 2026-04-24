using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class MenuFontUtility
{
    private const string MenuFontAssetPath = "Assets/Fonts/Unutterable_Font_1_07/TrueType (.ttf)/Unutterable-Regular SDF 1.asset";
    private static TMP_FontAsset _cachedMenuFont;

    public static TMP_FontAsset ResolveMenuFont(Transform root, TMP_FontAsset preferred = null)
    {
        if (preferred != null)
            return preferred;

        if (_cachedMenuFont != null)
            return _cachedMenuFont;

        if (root != null)
        {
            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_FontAsset font = texts[i] != null ? texts[i].font : null;
                if (font != null && font.name.Contains("Unutterable"))
                {
                    _cachedMenuFont = font;
                    return _cachedMenuFont;
                }
            }

            for (int i = 0; i < texts.Length; i++)
            {
                TMP_FontAsset font = texts[i] != null ? texts[i].font : null;
                if (font != null)
                {
                    _cachedMenuFont = font;
                    return _cachedMenuFont;
                }
            }
        }

#if UNITY_EDITOR
        _cachedMenuFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MenuFontAssetPath);
#endif

        return _cachedMenuFont;
    }

    public static void ApplyFont(Transform root, TMP_FontAsset font)
    {
        if (root == null || font == null)
            return;

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null)
                texts[i].font = font;
        }
    }

    public static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string text,
        TMP_FontAsset font,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = font;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    public static Button CreateTextButton(string name, Transform parent, string text, TMP_FontAsset font, Vector2 size)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.transform as RectTransform;
        rectTransform.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.08f, 0.08f, 0.08f, 0.88f);

        Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.5f, 0.05f, 0.05f, 0.95f);
        colors.pressedColor = new Color(0.75f, 0.08f, 0.08f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        TextMeshProUGUI label = CreateText("Label", buttonObject.transform, text, font, 28f, TextAlignmentOptions.Center);
        RectTransform labelRect = label.transform as RectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label.raycastTarget = true;

        return button;
    }
}
