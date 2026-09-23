using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The hub's play button. It knows one thing: which level comes next. Tapping it
/// opens the price window, and the window owns everything about what a run costs —
/// with a bet bar there is no single entry price until the player picks a rung, so
/// this button cannot answer that question and does not try.
///
/// With no window assigned it falls back to starting the level directly, at the
/// level's own entry cost. Hook the Button's OnClick to <see cref="Play"/>.
/// </summary>
public class PlayLevelButton : MonoBehaviour
{
    [SerializeField] Button _button;

    [Tooltip("The price window to open. Empty starts the level straight away at its own entry cost.")]
    [SerializeField] LevelPriceInfoWindow _infoWindow;

    [Header("Optional")]
    [Tooltip("Leave empty to play whatever comes next on the ladder. Set it to pin this button to one level, " +
             "which is useful for testing but means the button never advances.")]
    [SerializeField] LevelDef _level;
    [SerializeField] TMP_Text _levelNumberLabel;

    Wallet _wallet;
    LevelLadder _ladder;

    /// <summary>The pinned level if there is one, otherwise whatever is next on the ladder.</summary>
    LevelDef Level => _level != null ? _level : _ladder != null ? _ladder.Current : null;

    /// <summary>Only meaningful without a window: with one, the rung decides the price.</summary>
    bool GatesOnCost => _infoWindow == null;

    void Start()
    {
        _wallet = GameBootstrap.Instance.Wallet;
        _ladder = GameBootstrap.Instance.Ladder;

        if (Level == null)
        {
            Debug.LogError($"{name}: nothing to play — assign a LevelDef here, or a level sequence on GameBootstrap.", this);
            enabled = false;
            return;
        }

        if (GatesOnCost) _wallet.Changed += OnCurrencyChanged;
        if (_ladder != null) _ladder.Changed += Refresh;

        Refresh();
    }

    void OnDestroy()
    {
        if (_wallet != null) _wallet.Changed -= OnCurrencyChanged;
        if (_ladder != null) _ladder.Changed -= Refresh;
    }

    public void Play()
    {
        var level = Level;
        if (level == null) return;

        if (_infoWindow != null) _infoWindow.Open(level);
        else GameFlow.Instance.PlayLevel(level);
    }

    void OnCurrencyChanged(CurrencyDef _) => Refresh();

    void Refresh()
    {
        var level = Level;
        if (level == null) return;

        if (_levelNumberLabel != null && _ladder != null)
            _levelNumberLabel.text = $"Level {_ladder.CurrentNumber}";

        // Opening a window is always allowed — the window says what is affordable.
        if (_button != null)
            _button.interactable = !GatesOnCost || _wallet.CanAfford(level.EntryCost);
    }
}
