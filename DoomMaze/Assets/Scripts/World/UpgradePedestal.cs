using UnityEngine;

/// <summary>
/// Interactable pedestal used by upgrade rooms. The room controller assigns the
/// upgrade; this component owns local visuals, hum audio, and modal opening.
/// </summary>
public class UpgradePedestal : MonoBehaviour
{
    private const string SpawnPointName = "Upgrade Spawn Point";

    [SerializeField] private Transform _upgradeSpawnPoint;
    [SerializeField] private Vector3 _visualScale = Vector3.one * 0.85f;
    [SerializeField] private float _bobStrength = 0.18f;
    [SerializeField] private float _bobSpeed = 2.2f;
    [SerializeField] private float _spinStrength = 1f;
    [SerializeField] private float _spinSpeed = 90f;
    [SerializeField] private float _pulseSpeed = 3.4f;
    [SerializeField] private float _emissionMinIntensity = 1.2f;
    [SerializeField] private float _emissionMaxIntensity = 3.2f;
    [SerializeField] private AudioClip _humLoop;
    [SerializeField] [Range(0f, 1f)] private float _humVolume = 0.55f;
    [SerializeField] private float _humMinDistance = 1.5f;
    [SerializeField] private float _humMaxDistance = 12f;
    [SerializeField] private Vector3 _fallbackInteractionColliderSize = new Vector3(2.5f, 3f, 2.5f);
    [SerializeField] private Vector3 _fallbackInteractionColliderCenter = new Vector3(0f, 1.25f, 0f);

    private UpgradeRoomController _roomController;
    private UpgradeData _upgradeData;
    private GameObject _visualObject;
    private Renderer _visualRenderer;
    private Material _visualMaterial;
    private UpgradePedestalInteractTarget _interactTarget;
    private AudioSource _humSource;
    private Collider _interactionCollider;
    private Vector3 _visualBaseLocalPosition;
    private Color _upgradeColor = new Color(1f, 0.56f, 0.16f, 1f);
    private bool _isChosen;
    private bool _isConfigured;

    public bool CanInteract => !_isChosen && _upgradeData != null && _roomController != null;
    public UpgradeData UpgradeData => _upgradeData;

    private void Awake()
    {
        EnsureSetup();
    }

    private void OnEnable()
    {
        EnsureSetup();
        UpdateVisualState();
        UpdateHumState();
    }

    private void OnDisable()
    {
        if (_humSource != null)
            _humSource.Stop();
    }

    private void Update()
    {
        if (_visualObject == null || !_visualObject.activeSelf)
            return;

        float bob = Mathf.Sin(Time.time * Mathf.Max(0.01f, _bobSpeed)) * Mathf.Max(0f, _bobStrength);
        _visualObject.transform.localPosition = _visualBaseLocalPosition + Vector3.up * bob;
        _visualObject.transform.Rotate(Vector3.up, _spinSpeed * _spinStrength * Time.deltaTime, Space.Self);

        if (_visualMaterial == null)
            return;

        float pulse = (Mathf.Sin(Time.time * Mathf.Max(0.01f, _pulseSpeed)) + 1f) * 0.5f;
        float intensity = Mathf.Lerp(Mathf.Max(0f, _emissionMinIntensity), Mathf.Max(_emissionMinIntensity, _emissionMaxIntensity), pulse);
        Color color = _isChosen ? new Color(0.35f, 1f, 0.42f, 1f) : _upgradeColor;
        ApplyVisualMaterialColor(color, intensity);
    }

    public void Configure(UpgradeData upgradeData, UpgradeRoomController roomController)
    {
        EnsureSetup();
        _upgradeData = upgradeData;
        _roomController = roomController;
        _isChosen = false;
        _upgradeColor = GetColorForUpgrade(upgradeData);
        UpdateVisualState();
        UpdateHumState();
    }

    public void MarkUnavailable()
    {
        _upgradeData = null;
        _isChosen = true;
        UpdateVisualState();
        UpdateHumState();
    }

    public void MarkChosen()
    {
        _isChosen = true;
        UpdateVisualState();
        UpdateHumState();
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract)
            return;

        UpgradeChoiceModal.Show(this);
    }

    public void ChooseUpgrade()
    {
        if (!CanInteract)
            return;

        _roomController.ChoosePedestal(this);
    }

    private void EnsureSetup()
    {
        if (_isConfigured)
            return;

        if (_upgradeSpawnPoint == null)
            _upgradeSpawnPoint = FindSpawnPoint(transform);

        if (_upgradeSpawnPoint == null)
            _upgradeSpawnPoint = transform;

        EnsureInteractionCollider();
        EnsureVisual();
        EnsureHumSource();
        _isConfigured = true;
    }

    private static Transform FindSpawnPoint(Transform root)
    {
        if (root == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == SpawnPointName)
                return children[i];
        }

        return null;
    }

    private void EnsureInteractionCollider()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                _interactionCollider = colliders[i];
                return;
            }
        }

        BoxCollider collider = gameObject.AddComponent<BoxCollider>();
        collider.isTrigger = false;
        collider.size = _fallbackInteractionColliderSize;
        collider.center = _fallbackInteractionColliderCenter;
        _interactionCollider = collider;
    }

    private void EnsureVisual()
    {
        if (_visualObject != null)
            return;

        _visualObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _visualObject.name = "UpgradePlaceholderCube";
        _visualObject.transform.SetParent(_upgradeSpawnPoint, false);
        _visualObject.transform.localPosition = Vector3.zero;
        _visualObject.transform.localRotation = Quaternion.identity;
        _visualObject.transform.localScale = _visualScale;
        _visualBaseLocalPosition = _visualObject.transform.localPosition;

        Collider visualCollider = _visualObject.GetComponent<Collider>();
        if (visualCollider == null)
            visualCollider = _visualObject.AddComponent<BoxCollider>();
        visualCollider.isTrigger = false;

        _interactTarget = _visualObject.GetComponent<UpgradePedestalInteractTarget>();
        if (_interactTarget == null)
            _interactTarget = _visualObject.AddComponent<UpgradePedestalInteractTarget>();
        _interactTarget.Configure(this);

        _visualRenderer = _visualObject.GetComponent<Renderer>();
        if (_visualRenderer != null)
        {
            _visualMaterial = new Material(FindVisualShader());
            _visualMaterial.name = "RuntimeUpgradeCubeMaterial";
            _visualRenderer.material = _visualMaterial;
            ApplyVisualMaterialColor(_upgradeColor, _emissionMinIntensity);
        }
    }

    private void EnsureHumSource()
    {
        if (_humSource != null)
            return;

        _humSource = gameObject.GetComponent<AudioSource>();
        if (_humSource == null)
            _humSource = gameObject.AddComponent<AudioSource>();

        _humSource.playOnAwake = false;
        _humSource.loop = true;
        _humSource.spatialBlend = 1f;
        _humSource.rolloffMode = AudioRolloffMode.Linear;
        _humSource.minDistance = Mathf.Max(0.1f, _humMinDistance);
        _humSource.maxDistance = Mathf.Max(_humSource.minDistance, _humMaxDistance);
    }

    private void UpdateVisualState()
    {
        if (_visualObject == null)
            return;

        bool visible = _upgradeData != null;
        _visualObject.SetActive(visible);

        if (_visualMaterial != null && visible)
        {
            Color color = _isChosen ? new Color(0.35f, 1f, 0.42f, 1f) : _upgradeColor;
            ApplyVisualMaterialColor(color, _emissionMaxIntensity);
        }
    }

    private void UpdateHumState()
    {
        if (_humSource == null)
            return;

        _humSource.clip = _humLoop;
        _humSource.volume = Mathf.Clamp01(_humVolume);

        if (_humLoop != null && _upgradeData != null && !_isChosen && isActiveAndEnabled)
        {
            if (!_humSource.isPlaying)
                _humSource.Play();
        }
        else
        {
            _humSource.Stop();
        }
    }

    private void ApplyVisualMaterialColor(Color color, float emissionIntensity)
    {
        if (_visualMaterial == null)
            return;

        if (_visualMaterial.HasProperty("_BaseColor"))
            _visualMaterial.SetColor("_BaseColor", color);
        else if (_visualMaterial.HasProperty("_Color"))
            _visualMaterial.SetColor("_Color", color);

        Color emissionColor = color * Mathf.Max(0f, emissionIntensity);
        if (_visualMaterial.HasProperty("_EmissionColor"))
        {
            _visualMaterial.EnableKeyword("_EMISSION");
            _visualMaterial.SetColor("_EmissionColor", emissionColor);
        }
    }

    private static Shader FindVisualShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        return shader;
    }

    private static Color GetColorForUpgrade(UpgradeData upgradeData)
    {
        string seed = upgradeData != null && !string.IsNullOrWhiteSpace(upgradeData.UpgradeId)
            ? upgradeData.UpgradeId
            : "upgrade";

        int hash = seed.GetHashCode();
        float hue = Mathf.Abs(hash % 1000) / 1000f;
        return Color.HSVToRGB(hue, 0.78f, 1f);
    }
}

public class UpgradePedestalInteractTarget : MonoBehaviour, IInteractable
{
    private UpgradePedestal _pedestal;

    public bool CanInteract => _pedestal != null && _pedestal.CanInteract;

    public void Configure(UpgradePedestal pedestal)
    {
        _pedestal = pedestal;
    }

    public void Interact(GameObject interactor)
    {
        _pedestal?.Interact(interactor);
    }
}
