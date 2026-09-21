using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The hub's play button. Plays whatever level the ladder says is next, shows the
/// entry cost, and disables itself when the player cannot pay it. Hook the Button's
/// OnClick to <see cref="Play"/>.
/// </summary>
public class PlayLevelButton : MonoBehaviour
{
    [SerializeField] Button _button;

    [Header("Optional")]
    [Tooltip("Leave empty to play whatever comes next on the ladder. Set it to pin this button to one level, " +
             "which is useful for testing but means the button never advances.")]
    [SerializeField] LevelDef _level;
    [SerializeField] TMP_Text _costLabel;
    [SerializeField] TMP_Text _levelNumberLabel;

    Wallet _wallet;
    LevelLadder _ladder;

    /// <summary>The pinned level if there is one, otherwise whatever is next on the ladder.</summary>
    LevelDef Level => _level != null ? _level : _ladder != null ? _ladder.Current : null;

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

        _wallet.Changed += OnCurrencyChanged;
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
        if (level != null) GameFlow.Instance.PlayLevel(level);
    }

    void OnCurrencyChanged(CurrencyDef _) => Refresh();

    void Refresh()
    {
        var level = Level;
        if (level == null) return;

        if (_button != null) _button.interactable = _wallet.CanAfford(level.EntryCost);

        if (_costLabel != null)
        {
            var costs = level.EntryCost?.Costs;
            _costLabel.text = costs == null || costs.Length == 0 ? "FREE" : costs[0].Amount.ToString();
        }

        if (_levelNumberLabel != null && _ladder != null)
            _levelNumberLabel.text = $"Level {_ladder.CurrentNumber}";
    }
}
