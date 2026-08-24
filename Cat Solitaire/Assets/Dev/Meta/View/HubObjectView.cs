using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// One buyable prop in the hub. Owns no rules: it reads state from
/// <see cref="ProgressionService"/> and forwards taps back to it.
/// Needs a Collider2D and a Physics2DRaycaster on the camera to receive clicks.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class HubObjectView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] HubObjectDef _definition;
    [SerializeField] SpriteRenderer _renderer;

    [Header("Optional")]
    [SerializeField] TMP_Text _costLabel;
    [SerializeField] GameObject _affordableHint;   // glow / arrow shown when buyable

    Wallet _wallet;
    ProgressionService _progression;

    void Reset() => _renderer = GetComponent<SpriteRenderer>();

    void Start()
    {
        if (_definition == null)
        {
            Debug.LogError($"{name}: no HubObjectDef assigned.", this);
            enabled = false;
            return;
        }

        // Start(), not Awake() — GameBootstrap builds the services in its own Awake,
        // and Awake order between objects is undefined.
        _wallet = GameBootstrap.Instance.Wallet;
        _progression = GameBootstrap.Instance.Progression;

        _progression.ObjectUnlocked += OnObjectUnlocked;
        _wallet.Changed += OnCurrencyChanged;

        Refresh();
    }

    void OnDestroy()
    {
        if (_progression != null) _progression.ObjectUnlocked -= OnObjectUnlocked;
        if (_wallet != null) _wallet.Changed -= OnCurrencyChanged;
    }

    public void OnPointerClick(PointerEventData eventData) => TryPurchase();

    /// <summary>Also hook this to a UI Button's OnClick if you prefer buttons over world taps.</summary>
    public void TryPurchase()
    {
        if (_progression == null || _progression.IsUnlocked(_definition)) return;

        if (!_progression.TryPurchase(_definition))
            Debug.Log($"Not enough currency for {_definition.DisplayName}.");
    }

    void OnObjectUnlocked(HubObjectDef unlocked)
    {
        if (unlocked == _definition) Refresh();
    }

    void OnCurrencyChanged(CurrencyDef _) => Refresh();

    void Refresh()
    {
        var unlocked = _progression.IsUnlocked(_definition);

        if (_renderer != null)
            _renderer.sprite = unlocked ? _definition.UnlockedSprite : _definition.LockedSprite;

        if (_costLabel != null)
        {
            _costLabel.gameObject.SetActive(!unlocked);
            if (!unlocked) _costLabel.text = FormatCost();
        }

        if (_affordableHint != null)
            _affordableHint.SetActive(!unlocked && _wallet.CanAfford(_definition.Cost));
    }

    string FormatCost()
    {
        var costs = _definition.Cost?.Costs;
        if (costs == null || costs.Length == 0) return "Free";

        var text = "";
        foreach (var cost in costs)
            text += (text.Length > 0 ? "  " : "") + cost.Amount;
        return text;
    }
}
