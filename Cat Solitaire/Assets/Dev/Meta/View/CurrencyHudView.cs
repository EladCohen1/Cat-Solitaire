using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays one currency balance. The same prefab serves every currency —
/// drop it twice and point one at Coins and one at Stars.
/// </summary>
public class CurrencyHudView : MonoBehaviour
{
    [SerializeField] CurrencyDef _currency;
    [SerializeField] TMP_Text _amountLabel;
    [SerializeField] Image _iconImage;

    Wallet _wallet;

    void Start()
    {
        if (_currency == null)
        {
            Debug.LogError($"{name}: no CurrencyDef assigned.", this);
            enabled = false;
            return;
        }

        _wallet = GameBootstrap.Instance.Wallet;
        _wallet.Changed += OnCurrencyChanged;

        if (_iconImage != null && _currency.Icon != null)
            _iconImage.sprite = _currency.Icon;

        Refresh();
    }

    void OnDestroy()
    {
        if (_wallet != null) _wallet.Changed -= OnCurrencyChanged;
    }

    void OnCurrencyChanged(CurrencyDef changed)
    {
        if (changed == _currency) Refresh();
    }

    void Refresh()
    {
        if (_amountLabel != null)
            _amountLabel.text = _wallet.Get(_currency).ToString();
    }
}
