using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The bottom-right "play" button in the hub. Shows the entry cost and disables
/// itself when the player cannot pay it. Hook the Button's OnClick to <see cref="Play"/>.
/// </summary>
public class PlayLevelButton : MonoBehaviour
{
    [SerializeField] LevelDef _level;
    [SerializeField] Button _button;

    [Header("Optional")]
    [SerializeField] TMP_Text _costLabel;

    Wallet _wallet;

    void Start()
    {
        if (_level == null)
        {
            Debug.LogError($"{name}: no LevelDef assigned.", this);
            enabled = false;
            return;
        }

        _wallet = GameBootstrap.Instance.Wallet;
        _wallet.Changed += OnCurrencyChanged;
        Refresh();
    }

    void OnDestroy()
    {
        if (_wallet != null) _wallet.Changed -= OnCurrencyChanged;
    }

    public void Play() => GameFlow.Instance.PlayLevel(_level);

    void OnCurrencyChanged(CurrencyDef _) => Refresh();

    void Refresh()
    {
        if (_button != null) _button.interactable = _wallet.CanAfford(_level.EntryCost);

        if (_costLabel != null)
        {
            var costs = _level.EntryCost?.Costs;
            _costLabel.text = costs == null || costs.Length == 0 ? "FREE" : costs[0].Amount.ToString();
        }
    }
}
