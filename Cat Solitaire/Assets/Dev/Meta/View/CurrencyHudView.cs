using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays one currency balance. The same prefab serves every currency —
/// drop it twice and point one at Coins and one at Stars.
///
/// It also sits in the level scene, which can be opened and played on its own with
/// no game running behind it. That is not a broken scene, so it leaves the authored
/// number where it is and says so once, rather than throwing on the way in.
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

        if (GameBootstrap.Instance == null)
        {
            Debug.Log($"{name}: no game running, so the {_currency.name} counter is " +
                      "showing whatever it was drawn with.", this);
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
        // Grouped, because a six figure balance is unreadable without it and the
        // counter is drawn wide enough to expect it.
        if (_amountLabel != null)
            _amountLabel.text = _wallet.Get(_currency).ToString("N0");
    }
}
