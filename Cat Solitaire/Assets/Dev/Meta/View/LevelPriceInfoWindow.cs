using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelPriceInfoWindow : MonoBehaviour
{
    [Header("Window")]
    [Tooltip("The panel Open and Close switch. Left empty, this GameObject is used.")]
    [SerializeField] GameObject _window;
    [SerializeField] Button _playButton;

    [Header("Bet bar")]
    [SerializeField] RewardBar _bar;
    [SerializeField] Button _raiseButton;
    [SerializeField] Button _lowerButton;

    [Header("Slots")]
    [Tooltip("One per currency the entry cost can be paid in, in order.")]
    [SerializeField] PriceSlotInfo[] _priceSlots;
    [Tooltip("One per currency a win can pay, in order. Spare slots are switched off.")]
    [SerializeField] RewardSlotInfo[] _rewardSlots;

    [Header("Optional")]
    [SerializeField] TMP_Text _levelNumberLabel;
    [SerializeField] TMP_Text _costText;
    [SerializeField] Color _affordableColor = Color.white;
    [SerializeField] Color _tooExpensiveColor = new Color(0.85f, 0.25f, 0.3f);

    LevelDef _level;
    Wallet _wallet;
    LevelLadder _ladder;
    int _tier;

    public bool IsOpen => Window.activeSelf;

    GameObject Window => _window != null ? _window : gameObject;

    WagerLadderDef Ladder => _level != null ? _level.Wagers : null;

    int TierCount => Ladder != null && Ladder.HasTiers ? Ladder.TierCount : 1;

    WagerTier CurrentTier => Ladder != null ? Ladder.At(_tier) : null;

    void Start()
    {
        _wallet = GameBootstrap.Instance.Wallet;
        _ladder = GameBootstrap.Instance.Ladder;

        _wallet.Changed += OnCurrencyChanged;
        if (_bar != null) _bar.Changed += OnBarMoved;
        if (_raiseButton != null) _raiseButton.onClick.AddListener(Raise);
        if (_lowerButton != null) _lowerButton.onClick.AddListener(Lower);
    }

    void OnDestroy()
    {
        if (_wallet != null) _wallet.Changed -= OnCurrencyChanged;
        if (_bar != null) _bar.Changed -= OnBarMoved;
    }

    public void Open(LevelDef level)
    {
        if (level == null)
        {
            Debug.LogError($"{name}: opened with no level.", this);
            return;
        }

        _level = level;
        _tier = 0;

        if (_bar != null)
        {
            _bar.Configure(0, TierCount);
            _bar.SetValue(BarValueFor(_tier));
        }

        Refresh();
        Window.SetActive(true);
    }

    public void Close() => Window.SetActive(false);
    
    public void Raise() => SelectTier(_tier + 1);

    public void Lower() => SelectTier(_tier - 1);

    public void SelectTier(int tier)
    {
        var wanted = Mathf.Clamp(tier, 0, TierCount - 1);
        
        if (Ladder != null && !Ladder.IsUnlocked(wanted, LevelNumber))
        {
            if (_bar != null) _bar.SetValue(BarValueFor(_tier));
            return;
        }

        _tier = wanted;
        if (_bar != null) _bar.SetValue(BarValueFor(_tier));

        Refresh();
    }
    
    public void Play()
    {
        if (_level == null) return;
        
        var tier = CurrentTier;
        Close();
        GameFlow.Instance.PlayLevel(_level, tier);
    }

    int LevelNumber => _ladder != null ? _ladder.CurrentNumber : 1;

    void OnBarMoved(int value) => SelectTier(TierFor(value));

    // The bar reads as rungs filled, so the bottom rung is 1 of 3, not 0 of 3.
    static int BarValueFor(int tier) => tier + 1;

    static int TierFor(int barValue) => barValue - 1;

    void OnCurrencyChanged(CurrencyDef _)
    {
        if (IsOpen) Refresh();
    }

    void Refresh()
    {
        if (_level == null) return;

        var tier = CurrentTier;
        var price = GameFlow.PriceOf(_level, tier);
        var rewards = GameFlow.WinRewardsOf(_level, tier);
        var affordable = _wallet.CanAfford(price);

        if (_levelNumberLabel) _levelNumberLabel.text = $"Level {LevelNumber}";
        if (_costText) _costText.text = Describe(price);

        Fill(_priceSlots, price?.Costs, affordable ? _affordableColor : _tooExpensiveColor);
        Fill(_rewardSlots, rewards, _affordableColor);

        if (_playButton ) _playButton.interactable = affordable;
        if (_raiseButton ) _raiseButton.interactable = CanMoveTo(_tier + 1);
        if (_lowerButton ) _lowerButton.interactable = CanMoveTo(_tier - 1);
    }

    bool CanMoveTo(int tier) =>
        tier >= 0 && tier < TierCount && (Ladder == null || Ladder.IsUnlocked(tier, LevelNumber));

 
    static void Fill(BaseSlotInfo[] slots, CurrencyAmount[] amounts, Color color)
    {
        if (slots == null) return;

        for (var i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            var used = amounts != null && i < amounts.Length && amounts[i].Currency != null;
            slots[i].gameObject.SetActive(used);
            if (!used) continue;

            slots[i].SetInfoText(amounts[i].Amount);
            slots[i].SetInfoTextColor(color);
            slots[i].SetIconColor(Color.white);

            if (amounts[i].Currency.Icon != null) slots[i].SetIcon(amounts[i].Currency.Icon);
        }
    }

    static string Describe(Price price)
    {
        var costs = price?.Costs;
        if (costs == null || costs.Length == 0) return "FREE";

        return "Cost";

        /*var text = "";
        foreach (var cost in costs)
            text += (text.Length > 0 ? "  " : "") + cost.Amount;
        return text;*/
    }
}
