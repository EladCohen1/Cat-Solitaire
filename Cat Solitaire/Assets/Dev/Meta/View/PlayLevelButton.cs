using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    
    LevelDef Level => _level != null ? _level : _ladder != null ? _ladder.Current : null;
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
        if (!level) return;

        if (_levelNumberLabel && _ladder != null)
            _levelNumberLabel.text = $"Level {_ladder.CurrentNumber}";
        
        if (_button)
            _button.interactable = !GatesOnCost || _wallet.CanAfford(level.EntryCost);
    }
}
