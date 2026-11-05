using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the gameplay HUD from the round: the run meter, and the two things you
/// can buy while playing. It reads the runner and never the other way round, so the
/// board plays exactly the same with none of this wired up.
///
/// Prices come from the boosters asset rather than the labels, so what the button
/// says and what it charges cannot drift apart.
///
/// Nothing here is required. Every field left empty is simply skipped, which is what
/// lets the level scene be opened and played on its own while the HUD is half built.
/// </summary>
public class SolitaireHudView : MonoBehaviour
{
    [Tooltip("Left empty, the runner is looked for in the scene.")]
    [SerializeField] SolitaireLevelRunner _runner;

    [Header("Take a move back")]
    [SerializeField] Button _undoButton;
    [SerializeField] TMP_Text _undoPriceLabel;

    [Header("Wildcard")]
    [SerializeField] Button _wildcardButton;
    [SerializeField] TMP_Text _wildcardPriceLabel;

    [Tooltip("Shown while a wildcard is armed and waiting to be spent.")]
    [SerializeField] GameObject _wildcardArmedMarker;

    [Header("Run meter")]
    [Tooltip("One per notch, left to right. Left empty, the notches are found under the group " +
             "named below — they sit inside the HUD prefab, so there is nothing in the scene to " +
             "drag onto this field.")]
    [SerializeField] Toggle[] _notches;

    [Tooltip("The object laying the notches out. Its children are used, in the order it lays " +
             "them out, when nothing is wired above.")]
    [SerializeField] string _notchGroupName = "NotchGroup";

    [Tooltip("Flashed up when a run pays out.")]
    [SerializeField] GameObject _bonusBanner;

    [SerializeField] float _bannerSeconds = 1.2f;

    [Header("Also optional")]
    [SerializeField] Button _quitButton;
    [SerializeField] TMP_Text _scoreLabel;
    [SerializeField] TMP_Text _stockCountLabel;

    Coroutine _banner;
    bool _meterChecked;

    void Awake()
    {
        if (_runner == null) _runner = FindAnyObjectByType<SolitaireLevelRunner>();

        if (_runner == null)
        {
            Debug.LogWarning($"{name}: no SolitaireLevelRunner in the scene, so the HUD has " +
                             "nothing to show. The board still plays.", this);
            return;
        }

        if (_undoButton != null) _undoButton.onClick.AddListener(_runner.BuyUndo);
        if (_wildcardButton != null) _wildcardButton.onClick.AddListener(_runner.BuyWildcard);
        if (_quitButton != null) _quitButton.onClick.AddListener(_runner.Quit);

        if (_notches == null || _notches.Length == 0) _notches = FindNotches();

        _runner.Changed += Refresh;
        _runner.StreakPaid += ShowBonus;
    }

    /// <summary>
    /// The notches live inside the HUD prefab, so a field in the scene cannot point at
    /// them. They are found by the group that lays them out instead, which also hands
    /// them over in the order it draws them — left to right, the order they fill in.
    /// </summary>
    Toggle[] FindNotches()
    {
        if (string.IsNullOrEmpty(_notchGroupName)) return System.Array.Empty<Toggle>();

        // Inactive included: the panel may well start hidden.
        var toggles = FindObjectsByType<Toggle>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var toggle in toggles)
        {
            var group = toggle.transform.parent;
            if (group == null || group.name != _notchGroupName) continue;

            return group.GetComponentsInChildren<Toggle>(includeInactive: true);
        }

        return System.Array.Empty<Toggle>();
    }

    void Start()
    {
        if (_bonusBanner != null) _bonusBanner.SetActive(false);

        Refresh();
    }

    void Refresh()
    {
        if (_runner == null) return;

        CheckTheMeterCanReadTheRun();

        ShowPrice(_undoPriceLabel, _runner.UndoPrice);
        ShowPrice(_wildcardPriceLabel, _runner.WildcardPrice);

        if (_undoButton != null) _undoButton.interactable = _runner.CanBuyUndo;
        if (_wildcardButton != null) _wildcardButton.interactable = _runner.CanBuyWildcard;
        if (_wildcardArmedMarker != null) _wildcardArmedMarker.SetActive(_runner.WildcardArmed);

        FillMeter();

        if (_scoreLabel != null) _scoreLabel.text = _runner.Score.ToString();
        if (_stockCountLabel != null) _stockCountLabel.text = _runner.StockRemaining.ToString();
    }

    /// <summary>
    /// Lights one notch per card in the current run. Set without notifying, because
    /// these are pips being drawn, not switches being thrown.
    /// </summary>
    void FillMeter()
    {
        if (_notches == null) return;

        var lit = _runner.StreakMeter;

        for (var i = 0; i < _notches.Length; i++)
            if (_notches[i] != null)
                _notches[i].SetIsOnWithoutNotify(i < lit);
    }

    /// <summary>
    /// A meter with the wrong number of notches cannot show the run honestly: it either
    /// pays before it looks full or fills and sits there. Said once, when the round has
    /// started and the run length is known.
    /// </summary>
    void CheckTheMeterCanReadTheRun()
    {
        if (_meterChecked || _runner.StreakLength <= 0) return;
        _meterChecked = true;

        if (_notches == null || _notches.Length == 0)
        {
            Debug.LogWarning($"{name}: found no notches under '{_notchGroupName}', so the run " +
                             "meter will never light up.", this);
            return;
        }

        if (_notches.Length != _runner.StreakLength)
            Debug.LogWarning($"{name}: the meter has {_notches.Length} notches but a run pays every " +
                             $"{_runner.StreakLength} cards. One of the two is wrong — the notches in " +
                             "the HUD prefab, or Streak Length on the boosters asset.", this);
    }

    /// <summary>
    /// What one of these costs, read off the price itself. Several currencies in one
    /// price is more than a single label can say, so it shows the first and leaves
    /// the rest to a row of slots.
    /// </summary>
    static void ShowPrice(TMP_Text label, Price price)
    {
        if (label == null) return;

        var costs = price?.Costs;
        label.text = costs != null && costs.Length > 0 ? costs[0].Amount.ToString() : "0";
    }

    void ShowBonus()
    {
        if (_bonusBanner == null) return;

        if (_banner != null) StopCoroutine(_banner);
        _banner = StartCoroutine(FlashBanner());
    }

    IEnumerator FlashBanner()
    {
        _bonusBanner.SetActive(true);
        yield return new WaitForSeconds(_bannerSeconds);
        _bonusBanner.SetActive(false);

        _banner = null;
    }

    void OnDestroy()
    {
        if (_runner == null) return;

        _runner.Changed -= Refresh;
        _runner.StreakPaid -= ShowBonus;
    }
}
