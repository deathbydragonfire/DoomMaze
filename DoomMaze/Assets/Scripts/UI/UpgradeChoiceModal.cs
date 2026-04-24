using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Runtime modal for inspecting and choosing an upgrade pedestal.
/// </summary>
public class UpgradeChoiceModal : MonoBehaviour
{
    private const int SortingOrder = 6500;
    private static UpgradeChoiceModal _instance;

    private Canvas _canvas;
    private CanvasGroup _group;
    private Image _flashOverlay;
    private TextMeshProUGUI _titleLabel;
    private TextMeshProUGUI _descriptionLabel;
    private TextMeshProUGUI _rankLabel;
    private Button _backButton;
    private Button _chooseButton;
    private UpgradePedestal _activePedestal;
    private bool _previousCursorVisible;
    private CursorLockMode _previousCursorLockMode;
    private bool _playerControlsWereEnabled;
    private Coroutine _flashRoutine;

    public static void Show(UpgradePedestal pedestal)
    {
        if (pedestal == null || pedestal.UpgradeData == null)
            return;

        if (_instance == null)
            _instance = CreateInstance();

        _instance.Open(pedestal);
    }

    private static UpgradeChoiceModal CreateInstance()
    {
        GameObject root = new GameObject("UpgradeChoiceModal", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        DontDestroyOnLoad(root);

        UpgradeChoiceModal modal = root.AddComponent<UpgradeChoiceModal>();
        modal.Build();
        return modal;
    }

    private void Build()
    {
        _canvas = GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _group = GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.interactable = false;
        _group.blocksRaycasts = false;

        RectTransform rootRect = transform as RectTransform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        TMP_FontAsset font = MenuFontUtility.ResolveMenuFont(transform);

        GameObject dimObject = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dimObject.transform.SetParent(transform, false);
        Stretch(dimObject.transform as RectTransform);
        Image dim = dimObject.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.78f);

        GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(transform, false);
        RectTransform panelRect = panelObject.transform as RectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(980f, 560f);
        panelRect.anchoredPosition = Vector2.zero;
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.06f, 0.055f, 0.05f, 0.98f);

        _titleLabel = MenuFontUtility.CreateText("Title", panelObject.transform, "UPGRADE", font, 48f, TextAlignmentOptions.Center);
        RectTransform titleRect = _titleLabel.transform as RectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(48f, -124f);
        titleRect.offsetMax = new Vector2(-48f, -32f);
        _titleLabel.color = new Color(1f, 0.75f, 0.32f, 1f);

        _descriptionLabel = MenuFontUtility.CreateText("Description", panelObject.transform, "", font, 30f, TextAlignmentOptions.TopLeft);
        RectTransform descriptionRect = _descriptionLabel.transform as RectTransform;
        descriptionRect.anchorMin = new Vector2(0f, 0f);
        descriptionRect.anchorMax = new Vector2(1f, 1f);
        descriptionRect.offsetMin = new Vector2(72f, 160f);
        descriptionRect.offsetMax = new Vector2(-72f, -160f);
        _descriptionLabel.textWrappingMode = TextWrappingModes.Normal;
        _descriptionLabel.color = new Color(0.92f, 0.9f, 0.84f, 1f);

        _rankLabel = MenuFontUtility.CreateText("Rank", panelObject.transform, "", font, 28f, TextAlignmentOptions.Center);
        RectTransform rankRect = _rankLabel.transform as RectTransform;
        rankRect.anchorMin = new Vector2(0f, 0f);
        rankRect.anchorMax = new Vector2(1f, 0f);
        rankRect.pivot = new Vector2(0.5f, 0f);
        rankRect.offsetMin = new Vector2(72f, 104f);
        rankRect.offsetMax = new Vector2(-72f, 150f);
        _rankLabel.color = new Color(0.55f, 1f, 0.62f, 1f);

        GameObject buttonRow = new GameObject("ButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        buttonRow.transform.SetParent(panelObject.transform, false);
        RectTransform buttonRowRect = buttonRow.transform as RectTransform;
        buttonRowRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRowRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRowRect.pivot = new Vector2(0.5f, 0f);
        buttonRowRect.sizeDelta = new Vector2(620f, 76f);
        buttonRowRect.anchoredPosition = new Vector2(0f, 40f);

        HorizontalLayoutGroup layout = buttonRow.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 44f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        _backButton = MenuFontUtility.CreateTextButton("BackButton", buttonRow.transform, "BACK", font, new Vector2(260f, 72f));
        _chooseButton = MenuFontUtility.CreateTextButton("ChooseButton", buttonRow.transform, "CHOOSE", font, new Vector2(260f, 72f));
        _backButton.onClick.AddListener(Close);
        _chooseButton.onClick.AddListener(Choose);
        MenuButtonHoverEffect.AttachToButtons(buttonRow.transform);

        GameObject flashObject = new GameObject("GreenFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        flashObject.transform.SetParent(transform, false);
        Stretch(flashObject.transform as RectTransform);
        _flashOverlay = flashObject.GetComponent<Image>();
        _flashOverlay.color = new Color(0.15f, 1f, 0.22f, 0f);
        _flashOverlay.raycastTarget = false;

        gameObject.SetActive(false);
    }

    private void Open(UpgradePedestal pedestal)
    {
        _activePedestal = pedestal;
        UpgradeData upgrade = pedestal.UpgradeData;
        RunUpgradeManager manager = RunUpgradeManager.Instance;
        int currentRank = manager.GetRank(upgrade.UpgradeId);
        int nextRank = Mathf.Min(currentRank + 1, Mathf.Max(1, upgrade.MaxRank));

        _titleLabel.text = GetDisplayName(upgrade).ToUpperInvariant();
        _descriptionLabel.text = BuildDescription(upgrade);
        _rankLabel.text = $"RANK {currentRank}/{Mathf.Max(1, upgrade.MaxRank)}  >  {nextRank}/{Mathf.Max(1, upgrade.MaxRank)}";

        _previousCursorVisible = Cursor.visible;
        _previousCursorLockMode = Cursor.lockState;
        _playerControlsWereEnabled = InputManager.Instance != null && InputManager.Instance.Controls.Player.enabled;

        InputManager.Instance?.EnableUIControls();
        EnsureEventSystem();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        gameObject.SetActive(true);
        _group.alpha = 1f;
        _group.interactable = true;
        _group.blocksRaycasts = true;
        _chooseButton.Select();
    }

    private void Close()
    {
        Close(deactivate: true);
    }

    private void Close(bool deactivate)
    {
        _activePedestal = null;
        _group.alpha = 0f;
        _group.interactable = false;
        _group.blocksRaycasts = false;
        if (deactivate)
            gameObject.SetActive(false);
        RestoreInput();
    }

    private void Choose()
    {
        UpgradePedestal pedestal = _activePedestal;
        if (pedestal == null)
        {
            Close();
            return;
        }

        pedestal.ChooseUpgrade();
        Close(deactivate: false);
        StartGreenFlash();
    }

    private void RestoreInput()
    {
        if (_playerControlsWereEnabled)
            InputManager.Instance?.EnablePlayerControls();

        Cursor.lockState = _previousCursorLockMode;
        Cursor.visible = _previousCursorVisible;
    }

    private void StartGreenFlash()
    {
        gameObject.SetActive(true);
        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);

        _flashRoutine = StartCoroutine(GreenFlashRoutine());
    }

    private IEnumerator GreenFlashRoutine()
    {
        float duration = 0.28f;
        float peakAlpha = 0.45f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Color color = new Color(0.15f, 1f, 0.22f, Mathf.Lerp(peakAlpha, 0f, t));
            _flashOverlay.color = color;
            yield return null;
        }

        _flashOverlay.color = new Color(0.15f, 1f, 0.22f, 0f);
        _flashRoutine = null;
        if (_group.alpha <= 0f)
            gameObject.SetActive(false);
    }

    private static string BuildDescription(UpgradeData upgrade)
    {
        if (upgrade == null)
            return "";

        if (!string.IsNullOrWhiteSpace(upgrade.Description))
            return upgrade.Description;

        return upgrade.EffectType switch
        {
            UpgradeEffectType.PistolDamage => "Increases pistol damage by 20% per rank.",
            UpgradeEffectType.MachineGunDamage => "Increases machine gun damage by 15% per rank.",
            UpgradeEffectType.ReloadSpeed => "Reduces reload time by 15% per rank, down to 55% of base reload time.",
            UpgradeEffectType.FlamethrowerUse => "Reduces flamethrower heat gain by 15% per rank.",
            UpgradeEffectType.FlamethrowerCooldown => "Increases flamethrower heat cooldown by 25% per rank.",
            UpgradeEffectType.RocketExplosionRadius => "Increases rocket explosion radius by 20% per rank.",
            UpgradeEffectType.SpecialCharge => "Reduces the kills required to charge your special by 1 per rank.",
            UpgradeEffectType.ExtraJump => "Adds one extra air jump per rank.",
            UpgradeEffectType.ExtraWallJump => "Adds one extra wall jump per airborne sequence per rank.",
            UpgradeEffectType.MovementSpeed => "Increases walk and sprint speed by 10% per rank.",
            UpgradeEffectType.MeleeDamage => "Increases melee damage by 25% per rank.",
            UpgradeEffectType.PickupDropRate => "Adds 10 percentage points to enemy pickup drop chance per rank.",
            _ => "Improves your run until you start a new game."
        };
    }

    private static string GetDisplayName(UpgradeData upgrade)
    {
        if (upgrade == null)
            return "Upgrade";

        return !string.IsNullOrWhiteSpace(upgrade.DisplayName) ? upgrade.DisplayName : upgrade.UpgradeId;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        InputManager.Instance?.ConfigureUIInputModule();
    }
}
