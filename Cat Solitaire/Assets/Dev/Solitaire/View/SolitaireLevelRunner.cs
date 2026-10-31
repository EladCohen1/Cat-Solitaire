using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runs one solitaire level: builds the board from the level's rule asset, lets the
/// player play it, and hands exactly one <see cref="LevelResult"/> back to GameFlow.
/// This is the Solitaire assembly's only point of contact with the meta game, and it
/// never touches the wallet — GameFlow owns the money.
///
/// The board is laid out in world space in front of the level camera, so cards are
/// real objects that can be tweened, flipped and lit. That camera needs a
/// PhysicsRaycaster on it or no card will ever answer a tap.
/// </summary>
public class SolitaireLevelRunner : MonoBehaviour, ILevelRunner
{
    [Header("Content")]
    [Tooltip("Used when the LevelDef carries no solitaire rules, and when this scene is played on its own.")]
    [SerializeField] SolitaireLevelDef _fallbackRules;

    [Header("Board")]
    [SerializeField] SolitaireCardView _cardPrefab;
    [Tooltip("Cards are parented here. Left empty, the runner's own transform is used.")]
    [SerializeField] Transform _boardRoot;
    [SerializeField] Transform _stockAnchor;
    [SerializeField] Transform _wasteAnchor;

    [Tooltip("World distance between neighbouring slots, across and down the layout.")]
    [SerializeField] Vector2 _slotSpacing = new Vector2(1.1f, 1.5f);

    [Tooltip("How much nearer the camera each row sits, so lower rows overlap the peaks.")]
    [SerializeField] float _rowDepth = 0.02f;

    [Header("HUD")]
    [SerializeField] TMP_Text _scoreLabel;
    [SerializeField] TMP_Text _stockCountLabel;
    [SerializeField] TMP_Text _messageLabel;
    [SerializeField] Button _stockButton;
    [SerializeField] Button _quitButton;

    [Header("Flow")]
    [Tooltip("How long the finished board stays on screen before the result goes back to the hub.")]
    [SerializeField] float _endOfRoundPause = 0.9f;

    /// <summary>Raised once the board exists, for a deal animation to take over.</summary>
    public event Action Dealt;

    /// <summary>Raised with the slot a card just left, for a flight to the waste pile.</summary>
    public event Action<int> CardPlayed;

    Action<LevelResult> _onComplete;
    SolitaireGame _game;
    SolitaireBoardLayout _runtimeLayout;   // only when the level ships no layout of its own
    SolitaireCardView[] _cardViews;
    SolitaireCardView _wasteView;
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

    /// <summary>Wire this to the draw pile if you would rather not use a collider on it.</summary>
    public void Draw()
    {
        if (_game != null) _game.TryDraw();
    }

    void Build(SolitaireLevelDef rules)
    {
        _targetScore = rules != null ? rules.TargetScore : 0;

        var layout = rules != null && rules.Layout != null ? rules.Layout : DefaultLayout();
        var seed = rules != null && rules.Seed != 0
            ? rules.Seed
            : UnityEngine.Random.Range(int.MinValue, int.MaxValue);

        _game = new SolitaireGame(layout, seed);

        BuildBoard(layout);
        BuildWaste();

        if (_stockButton != null) _stockButton.onClick.AddListener(Draw);
        if (_quitButton != null) _quitButton.onClick.AddListener(Quit);

        _game.Changed += Refresh;
        Refresh();

        Dealt?.Invoke();
    }

    SolitaireBoardLayout DefaultLayout()
    {
        // Lives in memory only — never saved as an asset, so it is ours to destroy.
        _runtimeLayout = ScriptableObject.CreateInstance<SolitaireBoardLayout>();
        _runtimeLayout.name = "Classic Tri-Peaks (runtime)";
        return _runtimeLayout;
    }

    void BuildBoard(SolitaireBoardLayout layout)
    {
        if (_cardPrefab == null) throw new InvalidOperationException($"{name}: no card prefab assigned.");
        if (_boardRoot == null) _boardRoot = transform;

        var slots = layout.EffectiveSlots;
        var center = LayoutCenter(slots);

        _cardViews = new SolitaireCardView[slots.Count];

        for (var i = 0; i < slots.Count; i++)
        {
            var view = Instantiate(_cardPrefab, _boardRoot);
            view.name = $"Card {i}";
            view.Bind(i);
            view.transform.localPosition = PositionOf(slots[i].Position - center);
            view.Clicked += OnCardClicked;

            _cardViews[i] = view;
        }
    }

    void BuildWaste()
    {
        _wasteView = Instantiate(_cardPrefab, _wasteAnchor != null ? _wasteAnchor : _boardRoot);
        _wasteView.name = "Waste";
        _wasteView.Bind(-1);
        _wasteView.transform.localPosition = Vector3.zero;
        _wasteView.Freeze();
    }

    /// <summary>
    /// Layout coordinates are in card widths and heights. Rows nearer the bottom are
    /// also nearer the camera, which is what makes a peak look covered.
    /// </summary>
    Vector3 PositionOf(Vector2 slot) =>
        new Vector3(slot.x * _slotSpacing.x, slot.y * _slotSpacing.y, slot.y * _rowDepth);

    void OnCardClicked(SolitaireCardView view)
    {
        if (_game == null || view.Slot < 0) return;

        if (_game.TryPlay(view.Slot)) CardPlayed?.Invoke(view.Slot);
    }

    void Refresh()
    {
        for (var i = 0; i < _cardViews.Length; i++)
        {
            if (_game.IsCleared(i)) _cardViews[i].Hide();
            else _cardViews[i].Show(_game.CardAt(i), faceUp: true, _game.IsPlayable(i), _game.IsBlocked(i));
        }

        _wasteView.Show(_game.Active, faceUp: true, playable: false, blocked: false);
        _wasteView.Freeze();

        if (_stockCountLabel != null) _stockCountLabel.text = _game.StockRemaining.ToString();
        if (_stockButton != null)
            _stockButton.interactable = _game.StockRemaining > 0 && _game.Status == SolitaireStatus.Playing;

        if (_scoreLabel != null)
            _scoreLabel.text = _game.Combo > 1 ? $"{_game.Score}   x{_game.Combo}" : _game.Score.ToString();

        if (_game.Status != SolitaireStatus.Playing && !_roundOver) OnRoundOver();
    }

    void OnRoundOver()
    {
        _roundOver = true;
        _game.Changed -= Refresh;

        foreach (var view in _cardViews) view.Freeze();
        if (_stockButton != null) _stockButton.interactable = false;

        var won = _game.Status == SolitaireStatus.Won && _game.Score >= _targetScore;

        if (_messageLabel != null)
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
            Score = _game != null ? _game.Score : 0,
        });
    }

    static Vector2 LayoutCenter(IReadOnlyList<SolitaireBoardLayout.Slot> slots)
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
