using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row of the object progress window: an icon, a name, and either a price to pay
/// or a mark that it is already bought. Owns no rules — it reads
/// <see cref="ProgressionService"/> and hands taps straight back to it.
///
/// The window builds these and refreshes them. A row deliberately subscribes to
/// nothing, so a chapter with fifty objects costs fifty rows and no listeners.
/// </summary>
public class HubObjectBarView : MonoBehaviour
{
    [SerializeField] TMP_Text _nameLabel;
    [SerializeField] TMP_Text _costLabel;
    [SerializeField] Button _upgradeButton;

    [Header("Optional")]
    [SerializeField] Image _icon;
    [SerializeField] Image _costCurrencyIcon;
    [SerializeField] GameObject _costGroup;       // switched off once the object is bought
    [SerializeField] GameObject _unlockedBadge;   // shown in its place

    HubObjectDef _definition;
    ProgressionService _progression;
    Wallet _wallet;

    public HubObjectDef Definition => _definition;

    public void Bind(HubObjectDef definition, ProgressionService progression, Wallet wallet)
    {
        _definition = definition;
        _progression = progression;
        _wallet = wallet;

        if (_upgradeButton != null)
        {
            // Remove first: rebinding a pooled row must not stack a second listener.
            _upgradeButton.onClick.RemoveListener(TryPurchase);
            _upgradeButton.onClick.AddListener(TryPurchase);
        }

        Refresh();
    }

    /// <summary>Public so the prefab's own button can call it directly if you prefer.</summary>
    public void TryPurchase()
    {
        if (_progression == null || _definition == null) return;
        if (_progression.IsUnlocked(_definition)) return;

        if (!_progression.TryPurchase(_definition))
            Debug.Log($"Not enough currency for {_definition.DisplayName}.");
    }

    public void Refresh()
    {
        if (_definition == null) return;

        var unlocked = _progression.IsUnlocked(_definition);

        if (_nameLabel != null) _nameLabel.text = _definition.DisplayName;

        if (_icon != null)
        {
            var sprite = RowSprite(unlocked);
            _icon.sprite = sprite;
            _icon.enabled = sprite != null;   // no art yet is a blank row, not a white box
        }

        if (_costGroup != null) _costGroup.SetActive(!unlocked);
        if (_unlockedBadge != null) _unlockedBadge.SetActive(unlocked);
        if (_costLabel != null) _costLabel.text = FormatCost();

        var currency = FirstCurrency();
        if (_costCurrencyIcon != null && currency != null && currency.Icon != null)
            _costCurrencyIcon.sprite = currency.Icon;

        // A bought row stays on the list as a record of what the player owns,
        // so the button goes quiet instead of the row disappearing.
        if (_upgradeButton != null)
            _upgradeButton.interactable = !unlocked && _wallet.CanAfford(_definition.Cost);
    }

    Sprite RowSprite(bool unlocked)
    {
        if (_definition.Icon != null) return _definition.Icon;

        // No list icon authored yet — borrow the world sprite so the row is not empty.
        return unlocked ? _definition.UnlockedSprite : _definition.LockedSprite;
    }

    CurrencyDef FirstCurrency()
    {
        var costs = _definition.Cost?.Costs;
        return costs != null && costs.Length > 0 ? costs[0].Currency : null;
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
