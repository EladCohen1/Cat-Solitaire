using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What the player sees when a level ends: whether they won, what they scored and
/// what the round paid. It pays nothing itself — GameFlow has already granted the
/// rewards by the time this is told about them, so the numbers here are a report of
/// what happened, not a second payout.
///
/// Put it on something that stays active with <see cref="_window"/> pointing at the
/// panel, or on the panel itself.
/// </summary>
public class LevelResultWindow : MonoBehaviour
{
    [Header("Window")]
    [Tooltip("The panel this shows and hides. Left empty, this GameObject is used.")]
    [SerializeField] GameObject _window;
    [SerializeField] Button _closeButton;

    [Header("Result")]
    [SerializeField] TMP_Text _titleLabel;
    [Tooltip("One per currency a level can pay, in order. Spares are switched off.")]
    [SerializeField] RewardSlotInfo[] _rewardSlots;

    [Header("Optional")]
    [SerializeField] TMP_Text _scoreLabel;
    [SerializeField] GameObject _wonBadge;
    [SerializeField] GameObject _lostBadge;
    [Tooltip("Off means a loss closes back to the hub without a popup.")]
    [SerializeField] bool _showOnLoss = true;
    [SerializeField] string _wonTitle = "Level complete!";
    [SerializeField] string _lostTitle = "Out of moves";

    GameFlow _flow;

    GameObject Window => _window != null ? _window : gameObject;

    void Start()
    {
        _flow = GameFlow.Instance;

        if (_flow == null)
        {
            Debug.LogError($"{name}: no GameFlow to listen to.", this);
            enabled = false;
            return;
        }

        _flow.LevelFinished += OnLevelFinished;
        if (_closeButton != null) _closeButton.onClick.AddListener(Close);

        Close();
    }

    void OnDestroy()
    {
        if (_flow != null) _flow.LevelFinished -= OnLevelFinished;
    }

    public void Close() => Window.SetActive(false);

    /// <param name="granted">
    /// What the wallet actually received, already scaled by the rung the player bet
    /// on — not the level's authored rewards.
    /// </param>
    void OnLevelFinished(LevelDef level, LevelResult result, CurrencyAmount[] granted)
    {
        // Quitting is the player's own decision; they do not need telling about it.
        if (result.Outcome == LevelOutcome.Quit) return;

        var won = result.Outcome == LevelOutcome.Win;
        if (!won && !_showOnLoss) return;

        if (_titleLabel != null) _titleLabel.text = won ? _wonTitle : _lostTitle;
        if (_scoreLabel != null) _scoreLabel.text = result.Score.ToString();

        if (_wonBadge != null) _wonBadge.SetActive(won);
        if (_lostBadge != null) _lostBadge.SetActive(!won);

        CurrencySlots.Fill(_rewardSlots, granted, Color.white);

        Window.SetActive(true);
    }
}
