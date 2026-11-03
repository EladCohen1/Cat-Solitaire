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
    [Tooltip("One per notch, in order. The meter fills left to right and wraps when it pays.")]
    [SerializeField] Toggle[] _notches;

    [Tooltip("Flashed up when a run pays out.")]
    [SerializeField] GameObject _bonusBanner;

    [SerializeField] float _bannerSeconds = 1.2f;

    [Header("Also optional")]
    [SerializeField] Button _quitButton;
    [SerializeField] TMP_Text _scoreLabel;
    [SerializeField] TMP_Text _stockCountLabel;

    Coroutine _banner;

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

        _runner.Changed += Refresh;
        _runner.StreakPaid += ShowBonus;
    }

    void Start()
    {
        if (_bonusBanner != null) _bonusBanner.SetActive(false);

        Refresh();
    }

    void Refresh()
    {
        if (_runner == null) return;

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
    /// Lights one notch per card in the current run. A meter with more notches than
    /// the run needs leaves the spares dark rather than guessing at a scale.
    /// </summary>
    void FillMeter()
    {
        if (_notches == null) return;

        var lit = _runner.StreakMeter;

        for (var i = 0; i < _notches.Length; i++)
            if (_notches[i] != null)
                _notches[i].isOn = i < lit;
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
