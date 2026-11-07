#!/usr/bin/env bash
# Applies the deal animation. Run from the folder that contains the .git directory:
#     bash apply-deal-animation.sh
set -euo pipefail

cd "$(dirname "$0")"
if [ ! -d .git ]; then echo "ERROR: run this from the repository root (the folder with .git in it)"; exit 1; fi
DIR="Cat Solitaire/Assets/Dev/Solitaire/View"
if [ ! -d "$DIR" ]; then echo "ERROR: cannot find $DIR"; exit 1; fi

# --- SolitaireCardView.cs: check it is the version this change was built on ---
HAVE=$(sha256sum "$DIR/SolitaireCardView.cs" | cut -d" " -f1)
if [ "$HAVE" != "cfa7b42b23144b7f88a96bd9e0e37886b6b6b3b5b48875dfbb374de75d1753c6" ]; then
  echo "ERROR: $DIR/SolitaireCardView.cs is not the version expected."
  echo "  expected cfa7b42b23144b7f88a96bd9e0e37886b6b6b3b5b48875dfbb374de75d1753c6"
  echo "  found    $HAVE"
  echo "  Commit or stash your changes to it, or tell Claude, and stop here."
  exit 1
fi

# --- SolitaireLevelRunner.cs: check it is the version this change was built on ---
HAVE=$(sha256sum "$DIR/SolitaireLevelRunner.cs" | cut -d" " -f1)
if [ "$HAVE" != "8b1a54ff385f6117bce2d69f6e4e3db130ad3ffb1398b63950e95415e6db39c5" ]; then
  echo "ERROR: $DIR/SolitaireLevelRunner.cs is not the version expected."
  echo "  expected 8b1a54ff385f6117bce2d69f6e4e3db130ad3ffb1398b63950e95415e6db39c5"
  echo "  found    $HAVE"
  echo "  Commit or stash your changes to it, or tell Claude, and stop here."
  exit 1
fi

echo "writing SolitaireCardView.cs"
cat > "$DIR/SolitaireCardView.cs" <<'CLAUDE_EOF_SolitaireCardView_cs'
using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class SolitaireCardView : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Leave empty to find the renderer carrying the card face by material name.")]
    [SerializeField] MeshRenderer _frontRenderer;
    [SerializeField] Collider _collider;

    [Header("Atlas")]
    [Tooltip("Cells across and down the atlas texture.")]
    [SerializeField] int _columns = 8;
    [SerializeField] int _rows = 8;

    [Tooltip("Cell the card back sits in. The rest follow it: 2 up to Ace, suit after suit. " +
             "Only used on a mesh with no back side of its own.")]
    [SerializeField] int _backTile;

    [Tooltip("How many suits the atlas actually holds. While only one is drawn, every " +
             "suit borrows it — the rules do not care about suit, only the art does.")]
    [SerializeField, Min(1)] int _suitsInAtlas = 1;

    [Header("Materials")]
    [Tooltip("Material on the side of the card the player reads. Matched ignoring case, " +
             "spaces and underscores, so Card_Front finds CardFront.")]
    [SerializeField] string _frontMaterialName = "CardFront";

    [Tooltip("Material on the other side. Finding it is what tells the card it has a " +
             "real back to turn to.")]
    [SerializeField] string _backMaterialName = "CardBack";

    [Tooltip("Leave at -1 to find the front slot by name. Set an index only if the " +
             "material names change.")]
    [SerializeField] int _frontMaterialSlot = -1;

    [Header("Shader")]
    [SerializeField] string _tileXProperty = "_Tile_X";
    [SerializeField] string _tileYProperty = "_Tile_Y";

    [Tooltip("Off writes whole cell numbers. On writes a 0-1 offset instead, for a " +
             "shader that does not scale them itself.")]
    [SerializeField] bool _normalizeTile;

    [Tooltip("On if the shader counts rows up from the bottom of the texture. SHDR_Cards " +
             "flips rows itself, so this stays off — the atlas is read from the top down.")]
    [SerializeField] bool _countRowsFromBottom;

    [Header("Face down")]
    [Tooltip("Turned this far, the mesh's back side faces the camera. Half a turn about " +
             "Y for a card standing up to the camera.")]
    [SerializeField] Vector3 _faceDownRotation = new Vector3(0f, 180f, 0f);

    [Header("Look")]
    [SerializeField] Color _faceTint = Color.white;
    [SerializeField] Color _playableTint = new Color(1f, 0.97f, 0.78f);
    [SerializeField] Color _blockedTint = new Color(0.68f, 0.68f, 0.72f);
    [SerializeField] string _tintProperty = "_Color";

    /// <summary>Ranks per suit — Ace through King, however the atlas orders them.</summary>
    const int RanksPerSuit = 13;

    public event Action<SolitaireCardView> Clicked;

    /// <summary>Which tableau slot this card came from, or -1 for the stock and waste piles.</summary>
    public int Slot { get; private set; } = -1;

    /// <summary>Which way the card is turned. A flip animation can read this.</summary>
    public bool FaceUp { get; private set; } = true;

    Material[] _materials;   // our copies, ours to destroy
    Material _front;
    bool _hasBackSide;

    void Reset() => FindParts();

    void Awake()
    {
        FindParts();

        if (_frontRenderer == null)
        {
            Debug.LogError($"{name}: found no renderer carrying a '{_frontMaterialName}' material, " +
                           "and nothing with a mesh to fall back on — there is nothing to draw a card on.", this);
            return;
        }

        // Asking for .materials rather than .sharedMaterials is what makes the copies,
        // and the renderer goes on using them, so every card gets its own.
        _materials = _frontRenderer.materials;

        if (_materials.Length == 0)
        {
            Debug.LogError($"{name}: '{_frontRenderer.name}' has no material on it.", this);
            return;
        }

        _front = _materials[Mathf.Clamp(_frontMaterialSlot, 0, _materials.Length - 1)];
    }
    
    void FindParts()
    {
        if (_collider == null) _collider = GetComponentInChildren<Collider>();

        var renderers = GetComponentsInChildren<MeshRenderer>(includeInactive: true);

        // The back only has to be found, never written, so a shared material is enough.
        foreach (var renderer in renderers)
            if (SlotNamed(renderer, _backMaterialName) >= 0)
            {
                _hasBackSide = true;
                break;
            }

        // Explicit wiring wins, as long as it says which slot it means.
        if (_frontRenderer != null && _frontMaterialSlot >= 0) return;

        foreach (var renderer in renderers)
        {
            var slot = SlotNamed(renderer, _frontMaterialName);
            if (slot < 0) continue;

            _frontRenderer = renderer;
            _frontMaterialSlot = slot;
            return;
        }
        
        if (_frontRenderer == null) _frontRenderer = FirstDrawnRenderer(renderers);
        if (_frontMaterialSlot < 0) _frontMaterialSlot = 0;

        if (_frontRenderer != null)
            Debug.LogWarning($"{name}: no material named '{_frontMaterialName}' under this card, so the " +
                             $"face is going to '{_frontRenderer.name}' slot 0. If that is the back of the " +
                             "card, every card will look the same — name the materials or wire the front " +
                             "renderer and slot by hand.", this);
    }

    /// <summary>The slot on this renderer holding a material of that name, or -1.</summary>
    static int SlotNamed(MeshRenderer renderer, string wanted)
    {
        if (renderer == null || string.IsNullOrEmpty(wanted)) return -1;

        // sharedMaterials, so that looking does not quietly instance every card's art.
        var materials = renderer.sharedMaterials;

        for (var i = 0; i < materials.Length; i++)
            if (materials[i] != null && NameMatches(materials[i].name, wanted))
                return i;

        return -1;
    }
    
    static bool NameMatches(string candidate, string wanted) =>
        !string.IsNullOrEmpty(candidate) &&
        Simplify(candidate).StartsWith(Simplify(wanted), StringComparison.OrdinalIgnoreCase);

    static string Simplify(string name) => name.Replace("_", string.Empty).Replace(" ", string.Empty);

    /// <summary>A renderer with no mesh under it draws nothing, so skip past one.</summary>
    static MeshRenderer FirstDrawnRenderer(MeshRenderer[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var filter = candidate.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null) return candidate;
        }

        return candidates.Length > 0 ? candidates[0] : null;
    }

    public void Bind(int slot) => Slot = slot;

    /// <param name="faceUp">False turns the card around to show its back.</param>
    /// <param name="playable">Can be taken right now — lit.</param>
    /// <param name="blocked">Still covered by another card — dimmed and deaf to taps.</param>
    public void Show(Card card, bool faceUp, bool playable, bool blocked)
    {
        gameObject.SetActive(true);

        SetFaceUp(faceUp);

        // Face down, the mesh's own back side is what the camera sees, so there is
        // nothing to write. A mesh with only one material has no back side to turn
        // to, so there the back cell is drawn on the only face it has.
        if (faceUp) SetTile(TileOf(card));
        else if (!_hasBackSide) SetTile(_backTile);

        // Only the face is tinted. The back keeps the look CardBack was authored with.
        SetTint(blocked ? _blockedTint : playable ? _playableTint : _faceTint);

        // A covered card is scenery: let the tap reach whatever lies on top of it.
        if (_collider != null) _collider.enabled = !blocked;
    }

    public void Hide()
    {
        
        gameObject.SetActive(false);
    }

    /// <summary>True when the mesh has a back to turn to, rather than one printed side.</summary>
    public bool HasBackSide => _hasBackSide;

    /// <summary>Where this card sits turned one way or the other.</summary>
    public Quaternion TurnedTo(bool faceUp) =>
        faceUp ? Quaternion.identity : Quaternion.Euler(_faceDownRotation);

    /// <summary>Turns the card at once. An animation that drove the turn settles up with this.</summary>
    public void TurnNow(bool faceUp) => SetFaceUp(faceUp);

    /// <summary>Stops the board answering taps once the round is decided.</summary>
    public void Freeze()
    {
        if (_collider) _collider.enabled = false;
    }

    public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(this);

    /// <summary>
    /// Turns the card over. Instant for now — a flip tween belongs here, which is why
    /// the rotation lives in one place.
    /// </summary>
    void SetFaceUp(bool faceUp)
    {
        FaceUp = faceUp;

        if (!_hasBackSide) return;
        
        transform.localRotation = faceUp ? Quaternion.identity : Quaternion.Euler(_faceDownRotation);

    }

    /// <summary>
    /// The back sits first and the faces follow it, so an Ace lands after the King
    /// of its suit — which is the order the atlas is drawn in, not the order
    /// <see cref="Card.Rank"/> counts in.
    /// </summary>
    int TileOf(Card card)
    {
        var rank = card.Rank == Card.LowestRank ? RanksPerSuit - 1 : card.Rank - 2;
        var suit = (int)card.Suit % Mathf.Max(1, _suitsInAtlas);

        return _backTile + 1 + suit * RanksPerSuit + rank;
    }

    void SetTile(int tile)
    {
        if (_front == null) return;

        var columns = Mathf.Max(1, _columns);
        var rows = Mathf.Max(1, _rows);

        var x = tile % columns;
        var y = tile / columns;
        if (_countRowsFromBottom) y = rows - 1 - y;

        _front.SetFloat(_tileXProperty, _normalizeTile ? x / (float)columns : x);
        _front.SetFloat(_tileYProperty, _normalizeTile ? y / (float)rows : y);
    }

    void SetTint(Color color)
    {
        if (_front == null || string.IsNullOrEmpty(_tintProperty)) return;

        _front.SetColor(_tintProperty, color);
    }

    void OnDestroy()
    {
        Clicked = null;

        // The copies are ours, so they are ours to clean up.
        if (_materials == null) return;

        foreach (var material in _materials)
            if (material != null) Destroy(material);
    }
}
CLAUDE_EOF_SolitaireCardView_cs

echo "writing SolitaireLevelRunner.cs"
cat > "$DIR/SolitaireLevelRunner.cs" <<'CLAUDE_EOF_SolitaireLevelRunner_cs'
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

    [Tooltip("What buying a move back or a wildcard costs, and what a run pays. " +
             "Left empty, both are free and the run bonus is off.")]
    [SerializeField] SolitaireBoostersDef _boosters;

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

    /// <summary>Raised with the slot a card just returned to, for a flight back.</summary>
    public event Action<int> CardReturned;

    /// <summary>Raised after every change, for the HUD to read the round back.</summary>
    public event Action Changed;

    /// <summary>Raised when a run pays out, for the bonus to be shown landing.</summary>
    public event Action StreakPaid;

    Action<LevelResult> _onComplete;
    ILevelPurse _purse;
    SolitaireGame _game;
    SolitaireBoardLayout _runtimeLayout;   // only when the level ships no layout of its own
    SolitaireCardView[] _cardViews;
    SolitaireCardView _wasteView;
    SolitaireCardView _stockView;
    int _targetScore;
    bool _roundOver;

    public void Run(LevelDef def, ILevelPurse purse, Action<LevelResult> onComplete)
    {
        _onComplete = onComplete;

        // No purse means the level scene is being played on its own, so the things
        // you would buy are free rather than missing — that is the whole point of
        // being able to open this scene and press play.
        _purse = purse;

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

    /// <summary>The cards on the board, in slot order, for a deal to drive. Empty until it is built.</summary>
    public IReadOnlyList<SolitaireCardView> Cards => _cardViews;

    /// <summary>Where the draw pile sits, for a deal to fly cards in from.</summary>
    public Transform StockAnchor => _stockAnchor;

    /// <summary>The draw pile itself, for a deal to stop it being tapped mid-flight.</summary>
    public SolitaireCardView StockView => _stockView;

    /// <summary>True when nothing covers this slot, so its card is turned face up.</summary>
    public bool IsRevealed(int slot) =>
        _game != null && slot >= 0 && slot < _game.SlotCount &&
        !_game.IsCleared(slot) && !_game.IsBlocked(slot);

    /// <summary>
    /// Puts the board back the way the rules say it is. An animation that moved or
    /// turned the cards itself calls this to hand them back.
    /// </summary>
    public void RefreshBoard()
    {
        if (_game != null) Refresh();
    }

    // ---- what the HUD reads -------------------------------------------------

    public bool CanUndo => _game != null && _game.CanUndo;
    public bool WildcardArmed => _game != null && _game.WildcardArmed;
    public int StreakMeter => _game != null ? _game.StreakMeter : 0;
    public int StreakLength => _game != null ? _game.StreakLength : 0;
    public int Score => _game != null ? _game.Score : 0;
    public int StockRemaining => _game != null ? _game.StockRemaining : 0;

    public Price UndoPrice => _boosters != null ? _boosters.UndoPrice : null;
    public Price WildcardPrice => _boosters != null ? _boosters.WildcardPrice : null;

    /// <summary>Affordable and worth buying right now — what greys the button out.</summary>
    public bool CanBuyUndo => CanUndo && Affordable(UndoPrice);

    public bool CanBuyWildcard =>
        _game != null && _game.Status == SolitaireStatus.Playing &&
        !WildcardArmed && Affordable(WildcardPrice);

    // ---- what the HUD buttons call ------------------------------------------

    /// <summary>
    /// Buys one move back. Nothing is charged for a move that cannot be taken back,
    /// and nothing is taken back if the money is not there — in that order, so a
    /// player is never charged for nothing.
    /// </summary>
    public void BuyUndo()
    {
        if (_game == null || !_game.CanUndo) return;
        if (!Spend(UndoPrice)) return;

        _game.TryUndo();
    }

    /// <summary>Buys one wildcard, armed until a card that needs it is played.</summary>
    public void BuyWildcard()
    {
        if (_game == null || _game.Status != SolitaireStatus.Playing || _game.WildcardArmed) return;
        if (!Spend(WildcardPrice)) return;

        _game.TryArmWildcard();
    }

    bool Affordable(Price price) => _purse == null || _purse.CanAfford(price);

    bool Spend(Price price) => _purse == null || _purse.TrySpend(price);

    /// <summary>
    /// Pays for a run as it happens rather than at the end of the round, so the
    /// counter moves while the player is still looking at the cards.
    /// </summary>
    void PayForTheRun()
    {
        if (_boosters != null) _purse?.Grant(_boosters.StreakReward);

        StreakPaid?.Invoke();
    }

    void Build(SolitaireLevelDef rules)
    {
        _targetScore = rules != null ? rules.TargetScore : 0;

        var layout = rules != null && rules.Layout != null ? rules.Layout : DefaultLayout();
        var seed = rules != null && rules.Seed != 0
            ? rules.Seed
            : UnityEngine.Random.Range(int.MinValue, int.MaxValue);

        _game = new SolitaireGame(layout, seed)
        {
            StreakLength = _boosters != null ? _boosters.StreakLength : 0,
        };

        BuildBoard(layout);
        BuildPiles();

        if (_stockButton != null) _stockButton.onClick.AddListener(Draw);
        if (_quitButton != null) _quitButton.onClick.AddListener(Quit);

        _game.Changed += Refresh;
        _game.StreakCompleted += PayForTheRun;
        _game.SlotRestored += slot => CardReturned?.Invoke(slot);
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

    void BuildPiles()
    {
        _wasteView = Instantiate(_cardPrefab, _wasteAnchor != null ? _wasteAnchor : _boardRoot);
        _wasteView.name = "Waste";
        _wasteView.Bind(-1);
        _wasteView.transform.localPosition = Vector3.zero;
        _wasteView.Freeze();

        // The draw pile is a face-down card you tap, so a board needs no UI to be played.
        _stockView = Instantiate(_cardPrefab, _stockAnchor != null ? _stockAnchor : _boardRoot);
        _stockView.name = "Stock";
        _stockView.Bind(-1);
        _stockView.transform.localPosition = Vector3.zero;
        _stockView.Clicked += OnStockClicked;
    }

    /// <summary>
    /// Layout coordinates are in card widths and heights. Rows nearer the bottom are
    /// also nearer the camera, which is what makes a peak look covered.
    /// </summary>
    Vector3 PositionOf(Vector2 slot) =>
        new Vector3(slot.x * _slotSpacing.x, slot.y * _slotSpacing.y, slot.y * _rowDepth);

    void OnStockClicked(SolitaireCardView _) => Draw();

    void OnCardClicked(SolitaireCardView view)
    {
        if (_game == null || view.Slot < 0) return;

        if (_game.TryPlay(view.Slot)) CardPlayed?.Invoke(view.Slot);
    }

    void Refresh()
    {
        for (var i = 0; i < _cardViews.Length; i++)
        {
            if (_game.IsCleared(i))
            {
                _cardViews[i].Hide();
                continue;
            }

            // Tri-peaks deals the peaks face down: a card turns over the moment the
            // last card lying on it is taken, which is the same test that decides
            // whether it can be played at all.
            var blocked = _game.IsBlocked(i);
            _cardViews[i].Show(_game.CardAt(i), faceUp: !blocked, playable: _game.IsPlayable(i), blocked: blocked);
        }

        _wasteView.Show(_game.Active, faceUp: true, playable: false, blocked: false);
        _wasteView.Freeze();

        // Face down, so what the card carries never shows — the draw pile is a back.
        if (_game.StockRemaining > 0)
            _stockView.Show(_game.Active, faceUp: false, playable: false, blocked: false);
        else
            _stockView.Hide();

        if (_stockCountLabel != null) _stockCountLabel.text = _game.StockRemaining.ToString();
        if (_stockButton != null)
            _stockButton.interactable = _game.StockRemaining > 0 && _game.Status == SolitaireStatus.Playing;

        if (_scoreLabel != null)
            _scoreLabel.text = _game.Combo > 1 ? $"{_game.Score}   x{_game.Combo}" : _game.Score.ToString();

        Changed?.Invoke();

        if (_game.Status != SolitaireStatus.Playing && !_roundOver) OnRoundOver();
    }

    void OnRoundOver()
    {
        _roundOver = true;
        _game.Changed -= Refresh;

        foreach (var view in _cardViews) view.Freeze();
        if (_stockView != null) _stockView.Freeze();
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
        if (_game != null)
        {
            _game.Changed -= Refresh;
            _game.StreakCompleted -= PayForTheRun;
        }
        if (_runtimeLayout != null) Destroy(_runtimeLayout);
    }
}
CLAUDE_EOF_SolitaireLevelRunner_cs

echo "writing SolitaireDealAnimation.cs"
cat > "$DIR/SolitaireDealAnimation.cs" <<'CLAUDE_EOF_SolitaireDealAnimation_cs'
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Deals the board: every card starts on the draw pile and flies to its place one
/// after another, so the board draws itself as a trail rather than appearing. Once
/// they have all landed, the cards nothing is covering turn face up.
///
/// It drives the card transforms itself and hands them back to the runner at the
/// end, so the rules never know a deal was animated — take this component off and
/// the round plays the same, the board just appears all at once.
///
/// The whole deal is one Sequence, which is what lets the turn be timed off the last
/// card landing instead of guessed at, and what makes it one thing to stop.
/// </summary>
public class SolitaireDealAnimation : MonoBehaviour
{
    [Tooltip("Left empty, the runner is looked for in the scene.")]
    [SerializeField] SolitaireLevelRunner _runner;

    [Tooltip("Where the cards fly in from. Left empty, the draw pile is used.")]
    [SerializeField] Transform _dealFrom;

    [Header("Dealing")]
    [Tooltip("How long one card takes to reach its place.")]
    [SerializeField] float _moveSeconds = 0.35f;

    [Tooltip("The gap between one card setting off and the next. This is what makes the trail.")]
    [SerializeField] float _delayBetweenCards = 0.045f;

    [SerializeField] Ease _moveEase = Ease.OutCubic;

    [Tooltip("Cards start this much smaller and grow as they fly. 1 keeps them full size.")]
    [SerializeField] float _dealtFromScale = 1f;

    [Header("Turning the front row over")]
    [Tooltip("A beat after the last card lands, before any of them turn over.")]
    [SerializeField] float _pauseBeforeTurning = 0.15f;

    [SerializeField] float _turnSeconds = 0.28f;
    [SerializeField] float _delayBetweenTurns = 0.05f;
    [SerializeField] Ease _turnEase = Ease.InOutCubic;

    /// <summary>Raised once the board is dealt and turned, for whatever comes next.</summary>
    public event System.Action Finished;

    /// <summary>True while the board is still being dealt.</summary>
    public bool Running => _deal != null && _deal.IsActive() && _deal.IsPlaying();

    Sequence _deal;

    void Awake()
    {
        if (_runner == null) _runner = FindAnyObjectByType<SolitaireLevelRunner>();

        if (_runner == null)
        {
            Debug.LogWarning($"{name}: no SolitaireLevelRunner in the scene, so there is no " +
                             "board to deal.", this);
            return;
        }

        _runner.Dealt += Deal;
    }

    void OnDestroy()
    {
        if (_runner != null) _runner.Dealt -= Deal;

        // This build of DOTween has no SetLink, so the tweens have to be put down by
        // hand — one left running against a destroyed card is an exception a frame later.
        StopDealing();
    }

    void StopDealing()
    {
        if (_deal != null && _deal.IsActive()) _deal.Kill();

        _deal = null;
    }

    void Deal()
    {
        StopDealing();

        var cards = _runner.Cards;
        if (cards == null || cards.Count == 0) return;

        // Where each card belongs, read before any of them move: the runner has already
        // laid the board out, so this is the layout rather than a guess at it.
        var places = new Vector3[cards.Count];
        for (var i = 0; i < cards.Count; i++) places[i] = cards[i].transform.localPosition;

        var from = StartingPoint(cards[0].transform.parent);

        // The draw pile too: a tap on it mid-deal would draw a card, and the refresh
        // that followed would turn the board face up around cards still in flight.
        if (_runner.StockView != null) _runner.StockView.Freeze();

        var scaled = !Mathf.Approximately(_dealtFromScale, 1f);

        // Every card starts face down, on the pile, deaf to taps. This has to happen
        // before the tweens are made: DOTween reads where a tween starts from as it is
        // built, not when it comes round to playing it.
        foreach (var card in cards)
        {
            card.TurnNow(false);
            card.Freeze();
            card.transform.localPosition = from;
            if (scaled) card.transform.localScale = Vector3.one * _dealtFromScale;
        }

        _deal = DOTween.Sequence();

        for (var i = 0; i < cards.Count; i++)
        {
            var at = i * _delayBetweenCards;
            var card = cards[i].transform;

            _deal.Insert(at, card.DOLocalMove(places[i], _moveSeconds).SetEase(_moveEase));

            if (scaled) _deal.Insert(at, card.DOScale(Vector3.one, _moveSeconds).SetEase(_moveEase));
        }

        // The last card sets off last, so the board is down one flight after that.
        var lastLanded = (cards.Count - 1) * _delayBetweenCards + _moveSeconds;
        var turnAt = lastLanded + _pauseBeforeTurning;
        var turned = 0;

        for (var slot = 0; slot < cards.Count; slot++)
        {
            if (!_runner.IsRevealed(slot)) continue;
            if (!cards[slot].HasBackSide) continue;   // one printed side: nothing to turn

            _deal.Insert(turnAt + turned * _delayBetweenTurns,
                         cards[slot].transform.DOLocalRotate(Vector3.zero, _turnSeconds).SetEase(_turnEase));
            turned++;
        }

        _deal.OnComplete(HandBackToTheRules);
    }

    void HandBackToTheRules()
    {
        _deal = null;

        var cards = _runner.Cards;

        // The tween moved the transforms; this is what tells each card which way it is
        // now facing, so the next Show does not think it still has to turn one.
        if (cards != null)
            for (var slot = 0; slot < cards.Count; slot++)
                if (_runner.IsRevealed(slot)) cards[slot].TurnNow(true);

        // And the rules decide from here what is face up and what answers taps.
        _runner.RefreshBoard();

        Finished?.Invoke();
    }

    /// <summary>
    /// The draw pile, in the board's own space — the cards are parented to the board,
    /// so a point from anywhere else has to be brought into it first.
    /// </summary>
    Vector3 StartingPoint(Transform boardSpace)
    {
        var source = _dealFrom != null ? _dealFrom : _runner.StockAnchor;

        if (source == null) return Vector3.zero;
        if (boardSpace == null) return source.position;

        return boardSpace.InverseTransformPoint(source.position);
    }
}
CLAUDE_EOF_SolitaireDealAnimation_cs

echo "writing SolitaireDealAnimation.cs.meta"
cat > "$DIR/SolitaireDealAnimation.cs.meta" <<'CLAUDE_EOF_SolitaireDealAnimation_cs_meta'
fileFormatVersion: 2
guid: 3bd6cbc0397743f09bf986d6d0d41525
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
CLAUDE_EOF_SolitaireDealAnimation_cs_meta

echo
echo "Done. Now:"
echo "  1. git add -A && git commit -m \"Deal the board as a trail of cards, then turn the front row over\""
echo "  2. git push -u origin Liron"
echo "  3. In Unity: select the Solitaire object, Add Component, Solitaire Deal Animation. No wiring needed."
