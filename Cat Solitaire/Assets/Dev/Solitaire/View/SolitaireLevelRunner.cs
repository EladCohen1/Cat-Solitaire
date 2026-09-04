using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runs one solitaire level: builds the board from the level's rule asset, lets
/// the player play it, and hands exactly one <see cref="LevelResult"/> back to
/// GameFlow. This is the Solitaire assembly's only point of contact with the meta
/// game, and it never touches the wallet — GameFlow owns the money.
/// </summary>
public class SolitaireLevelRunner : MonoBehaviour, ILevelRunner
{
    [Header("Content")]
    [Tooltip("Used when the LevelDef carries no solitaire rules, and when this scene is played on its own.")]
    [SerializeField] SolitaireLevelDef _fallbackRules;

    [Header("Board")]
    [Tooltip("Left empty, the runner builds a full-screen board root under itself.")]
    [SerializeField] RectTransform _boardRoot;
    [SerializeField] float _columnSpacing = 160f;   // pixels per card width in the layout
    [SerializeField] float _rowSpacing = 210f;      // pixels per card height in the layout
    [SerializeField] Vector2 _boardOffset = new Vector2(0f, 140f);
    [SerializeField] Vector2 _stockPosition = new Vector2(-260f, -330f);
    [SerializeField] Vector2 _wastePosition = new Vector2(0f, -330f);

    [Header("Flow")]
    [Tooltip("How long the finished board stays on screen before the result goes back to the hub.")]
    [SerializeField] float _endOfRoundPause = 0.9f;

    Action<LevelResult> _onComplete;
    SolitaireGame _game;
    SolitaireBoardLayout _runtimeLayout;   // only when the level ships no layout of its own
    SolitaireCardView[] _cardViews;
    SolitaireCardView _wasteView;
    Button _stockButton;
    Image _stockBack;
    TMP_Text _stockLabel, _scoreLabel, _messageLabel;
    int _targetScore;
    bool _roundOver;

    public void Run(LevelDef def, Action<LevelResult> onComplete)
    {
        _onComplete = onComplete;

        var rules = def != null ? def.RuleData as SolitaireLevelDef : null;

        if (rules == null && def != null && def.RuleData != null)
            Debug.LogWarning($"Level '{LevelName(def)}' carries {def.RuleData.GetType().Name} as its rule data, " +
                             "which this runner cannot read. Falling back to the default board.", this);

        if (rules == null) rules = _fallbackRules;

        try
        {
            Build(rules);
        }
        catch (Exception e)
        {
            // Bad content must not leave GameFlow waiting on a callback that never comes.
            Debug.LogException(e, this);
            Finish(LevelOutcome.Quit);
        }
    }

    /// <summary>Wire this to a quit button. Quitting pays nothing, by GameFlow's rules.</summary>
    public void Quit() => Finish(LevelOutcome.Quit);

    void Build(SolitaireLevelDef rules)
    {
        _targetScore = rules != null ? rules.TargetScore : 0;

        var layout = rules != null && rules.Layout != null ? rules.Layout : DefaultLayout();
        var seed = rules != null && rules.Seed != 0
            ? rules.Seed
            : UnityEngine.Random.Range(int.MinValue, int.MaxValue);

        _game = new SolitaireGame(layout, seed);

        BuildBoard(layout);
        BuildPiles();
        BuildHud();

        _game.Changed += Refresh;
        Refresh();
    }

    SolitaireBoardLayout DefaultLayout()
    {
        // Left in memory only — it is never saved as an asset, so it is ours to destroy.
        _runtimeLayout = ScriptableObject.CreateInstance<SolitaireBoardLayout>();
        _runtimeLayout.name = "Classic Tri-Peaks (runtime)";
        return _runtimeLayout;
    }

    void BuildBoard(SolitaireBoardLayout layout)
    {
        if (_boardRoot == null) _boardRoot = SolitaireUi.Stretch("Board", transform);

        for (var i = _boardRoot.childCount - 1; i >= 0; i--)
            Destroy(_boardRoot.GetChild(i).gameObject);

        var slots = layout.EffectiveSlots;
        var center = LayoutCenter(slots);

        _cardViews = new SolitaireCardView[slots.Count];

        for (var i = 0; i < slots.Count; i++)
        {
            // Slots come out of the layout peaks first, so the lower rows are created
            // last and draw on top of the cards they overlap — which is what blocks them.
            var view = SolitaireCardView.Create(_boardRoot, $"Card {i}", i);
            view.SetPosition(ToPixels(slots[i].Position - center) + _boardOffset);
            view.Clicked += OnCardClicked;

            _cardViews[i] = view;
        }
    }

    void BuildPiles()
    {
        _stockBack = SolitaireUi.Panel("Stock", _boardRoot, SolitaireCardView.CardSize, new Color(0.24f, 0.35f, 0.62f));
        ((RectTransform)_stockBack.transform).anchoredPosition = _stockPosition;

        _stockButton = _stockBack.gameObject.AddComponent<Button>();
        _stockButton.targetGraphic = _stockBack;
        _stockButton.onClick.AddListener(OnStockClicked);

        _stockLabel = SolitaireUi.Label("Count", _stockBack.transform, string.Empty, 56f, Color.white);
        var countRect = (RectTransform)_stockLabel.transform;
        countRect.anchorMin = Vector2.zero;
        countRect.anchorMax = Vector2.one;
        countRect.offsetMin = Vector2.zero;
        countRect.offsetMax = Vector2.zero;

        _wasteView = SolitaireCardView.Create(_boardRoot, "Waste", -1);
        _wasteView.SetPosition(_wastePosition);
    }

    void BuildHud()
    {
        var hud = SolitaireUi.Stretch("HUD", transform);

        _scoreLabel = SolitaireUi.Label("Score", hud, string.Empty, 48f, Color.white, TextAlignmentOptions.TopLeft);
        TopBar((RectTransform)_scoreLabel.transform, top: 40f, height: 60f);

        _messageLabel = SolitaireUi.Label("Message", hud, string.Empty, 64f, Color.white, TextAlignmentOptions.Top);
        TopBar((RectTransform)_messageLabel.transform, top: 120f, height: 80f);

        var quit = SolitaireUi.Button("Quit", hud, "Quit", new Vector2(180f, 80f));
        var quitRect = (RectTransform)quit.transform;
        quitRect.anchorMin = quitRect.anchorMax = new Vector2(1f, 1f);
        quitRect.pivot = new Vector2(1f, 1f);
        quitRect.anchoredPosition = new Vector2(-40f, -40f);
        quit.onClick.AddListener(Quit);
    }

    /// <summary>Pins a label across the top of the screen, inset by a 40px margin.</summary>
    static void TopBar(RectTransform rect, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(-80f, height);
        rect.anchoredPosition = new Vector2(0f, -top);
    }

    void OnCardClicked(SolitaireCardView view)
    {
        if (_game == null || view.Slot < 0) return;

        _game.TryPlay(view.Slot);   // an illegal tap is simply ignored
    }

    void OnStockClicked()
    {
        if (_game == null) return;

        _game.TryDraw();
    }

    void Refresh()
    {
        for (var i = 0; i < _cardViews.Length; i++)
        {
            if (_game.IsCleared(i)) _cardViews[i].Hide();
            else _cardViews[i].Show(_game.CardAt(i), _game.IsPlayable(i), _game.IsBlocked(i));
        }

        _wasteView.Show(_game.Active, playable: false, blocked: false);
        _wasteView.Freeze();

        _stockLabel.text = _game.StockRemaining.ToString();
        _stockButton.interactable = _game.StockRemaining > 0 && _game.Status == SolitaireStatus.Playing;

        _scoreLabel.text = _game.Combo > 1
            ? $"{_game.Score}   x{_game.Combo}"
            : _game.Score.ToString();

        if (_game.Status != SolitaireStatus.Playing && !_roundOver) OnRoundOver();
    }

    void OnRoundOver()
    {
        _roundOver = true;
        _game.Changed -= Refresh;

        foreach (var view in _cardViews) view.Freeze();
        _stockButton.interactable = false;

        var won = _game.Status == SolitaireStatus.Won && _game.Score >= _targetScore;

        _messageLabel.text = _game.Status == SolitaireStatus.Won
            ? won ? "Board cleared!" : $"Cleared, but {_targetScore} points were needed"
            : "No moves left";

        StartCoroutine(FinishAfterPause(won ? LevelOutcome.Win : LevelOutcome.Lose));
    }

    IEnumerator FinishAfterPause(LevelOutcome outcome)
    {
        // Let the last card land before the hub comes back.
        yield return new WaitForSeconds(_endOfRoundPause);

        Finish(outcome);
    }

    void Finish(LevelOutcome outcome)
    {
        // Clear the callback first: a double-tap must never report twice, or the
        // player gets paid twice for one level.
        var callback = _onComplete;
        if (callback == null) return;
        _onComplete = null;

        if (_game != null) _game.Changed -= Refresh;

        callback(new LevelResult
        {
            Outcome = outcome,
            Score = _game != null ? _game.Score : 0
        });
    }

    Vector2 ToPixels(Vector2 layoutPosition) =>
        new Vector2(layoutPosition.x * _columnSpacing, layoutPosition.y * _rowSpacing);

    static Vector2 LayoutCenter(System.Collections.Generic.IReadOnlyList<SolitaireBoardLayout.Slot> slots)
    {
        var min = slots[0].Position;
        var max = slots[0].Position;

        foreach (var slot in slots)
        {
            min = Vector2.Min(min, slot.Position);
            max = Vector2.Max(max, slot.Position);
        }
        return (min + max) * 0.5f;
    }

    static string LevelName(LevelDef def) => string.IsNullOrEmpty(def.Id) ? def.name : def.Id;

    void OnDestroy()
    {
        if (_game != null) _game.Changed -= Refresh;
        if (_runtimeLayout != null) Destroy(_runtimeLayout);
    }
}
