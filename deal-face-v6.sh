#!/usr/bin/env bash
set -euo pipefail
echo "=============================================="
echo " dealing + card face  v6"
echo "=============================================="
cd "$(dirname "$0")"
if [ ! -d .git ]; then echo "ERROR: put this in the repository root (the folder with .git)"; exit 1; fi
DIR="Cat Solitaire/Assets/Dev/Solitaire/View"
hash_of() { tr -d "\r" < "$1" | sha256sum | cut -d" " -f1; }

HAVE=$(hash_of "$DIR/SolitaireCardView.cs")
if [ "$HAVE" != "5ff3bbfb1e2b3936ce27543b8cb68fa0a0404c082f0f43b4dfaf5cb0f3e71e79" ]; then
  echo; echo "########## STOPPED - NOTHING WRITTEN ##########"
  echo "file:     SolitaireCardView.cs"
  echo "expected: 5ff3bbfb1e2b3936ce27543b8cb68fa0a0404c082f0f43b4dfaf5cb0f3e71e79"
  echo "found:    $HAVE"
  echo "Send those lines to Claude. Do not commit."
  echo "###############################################"
  exit 1
fi
echo "checked  SolitaireCardView.cs"

HAVE=$(hash_of "$DIR/SolitaireLevelRunner.cs")
if [ "$HAVE" != "7e7cc5dc59fe576c8f2232b8a9dc7c834c16a53cd89f47edf9ddad5ddcb8fe58" ]; then
  echo; echo "########## STOPPED - NOTHING WRITTEN ##########"
  echo "file:     SolitaireLevelRunner.cs"
  echo "expected: 7e7cc5dc59fe576c8f2232b8a9dc7c834c16a53cd89f47edf9ddad5ddcb8fe58"
  echo "found:    $HAVE"
  echo "Send those lines to Claude. Do not commit."
  echo "###############################################"
  exit 1
fi
echo "checked  SolitaireLevelRunner.cs"

echo "writing  SolitaireCardView.cs"
cat > "$DIR/SolitaireCardView.cs" <<'CLAUDE_SolitaireCardView_cs'
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

    [Tooltip("How long the card takes to turn over when the rules reveal it mid-round. " +
             "0 turns it at once. The opening deal times its own turns separately.")]
    [SerializeField] float _turnSeconds = 0.25f;

    [SerializeField] Ease _turnEase = Ease.InOutCubic;

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

    Tweener _turn;
    bool? _turningTo;   // the face a turn in flight is heading for
    bool _dealt;        // the first turn is the deal, not a card being turned over

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

        // The face is written whichever way the card is turned. It is on its own side
        // of the mesh, so nobody can see it being set — and it has to be set now,
        // because turning the card over is a rotation and asks nothing of this card:
        // left until the next refresh, every card comes up showing whatever its
        // material was last left with rather than the card it is.
        //
        // A mesh with no back side has only the one face, so there the back cell is
        // what goes on it while the card is turned away.
        if (_hasBackSide) SetTile(TileOf(card));
        else SetTile(faceUp ? TileOf(card) : _backTile);

        // Only the face is tinted. The back keeps the look CardBack was authored with.
        SetTint(blocked ? _blockedTint : playable ? _playableTint : _faceTint);

        // A covered card is scenery: let the tap reach whatever lies on top of it.
        if (_collider != null) _collider.enabled = !blocked;
    }

    public void Hide()
    {
        StopTurning();

        gameObject.SetActive(false);
    }

    /// <summary>True when the mesh has a back to turn to, rather than one printed side.</summary>
    public bool HasBackSide => _hasBackSide;

    public void GoTo(Transform target, Sequence sequence)
    {
        
    }

    /// <summary>Where this card sits turned one way or the other.</summary>
    public Quaternion TurnedTo(bool faceUp) => faceUp ? Quaternion.identity : Quaternion.Euler(_faceDownRotation);

    /// <summary>Turns the card at once, with no tween — for a deal to set it up.</summary>
    public void TurnNow(bool faceUp) => Turn(faceUp, 0f);

    /// <summary>
    /// Turns the card over, taking its own time over it. A deal says when a card
    /// turns; how long it takes is the card's business, so there is one place to
    /// change it rather than one per animation that turns a card.
    /// </summary>
    public void TurnOver(bool faceUp) => Turn(faceUp, _turnSeconds);

    /// <summary>Stops the board answering taps once the round is decided.</summary>
    public void Freeze()
    {
        if (_collider) _collider.enabled = false;
    }

    public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(this);

    /// <summary>Turns the card over, over <see cref="_turnSeconds"/>.</summary>
    void SetFaceUp(bool faceUp) => Turn(faceUp, _turnSeconds);

    /// <summary>
    /// Turns the card to face one way or the other.
    ///
    /// Every move refreshes every card, and nearly all of them are already facing the
    /// way they are being told to, so a turn only happens when one is actually needed.
    /// Without that, a card would restart its turn on every move for the rest of the
    /// round and never finish one.
    /// </summary>
    void Turn(bool faceUp, float seconds)
    {
        FaceUp = faceUp;

        if (!_hasBackSide) return;

        // The first turn is the card being put down, not turned over, so it happens
        // at once however long a turn is set to take — otherwise the whole board
        // visibly flips itself face down the moment it is dealt.
        if (!_dealt)
        {
            _dealt = true;
            seconds = 0f;
        }

        if (_turningTo == faceUp) return;                           // already on its way
        if (_turningTo == null && AlreadyFacing(faceUp)) return;    // already there

        StopTurning();

        if (seconds <= 0f)
        {
            transform.localRotation = TurnedTo(faceUp);
            return;
        }

        _turningTo = faceUp;
        _turn = transform
            .DOLocalRotate(faceUp ? Vector3.zero : _faceDownRotation, seconds)
            .SetEase(_turnEase)
            .OnComplete(StopTurning);
    }

    /// <summary>
    /// Read off the transform rather than a flag of our own, so a turn driven from
    /// outside — a deal animating the cards itself — cannot leave the two disagreeing.
    /// </summary>
    bool AlreadyFacing(bool faceUp) =>
        Quaternion.Angle(transform.localRotation, TurnedTo(faceUp)) < 0.5f;

    void StopTurning()
    {
        if (_turn != null && _turn.IsActive()) _turn.Kill();

        _turn = null;
        _turningTo = null;
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

        // No SetLink in this build of DOTween, so a turn still running against a card
        // that has gone is an exception a frame later unless it is put down here.
        StopTurning();

        // The copies are ours, so they are ours to clean up.
        if (_materials == null) return;

        foreach (var material in _materials)
            if (material != null) Destroy(material);
    }
}
CLAUDE_SolitaireCardView_cs

echo "writing  SolitaireLevelRunner.cs"
cat > "$DIR/SolitaireLevelRunner.cs" <<'CLAUDE_SolitaireLevelRunner_cs'
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
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
    [Tooltip("Where the cards are thrown from. Left empty, the board simply appears.")]
    [SerializeField] Transform _dealingPoint;

    [Tooltip("How long one card takes to reach its place.")]
    [SerializeField] float _dealSeconds = 0.4f;

    [Tooltip("The gap between one card being thrown and the next. This is what makes it " +
             "read as dealing rather than the board arriving at once.")]
    [SerializeField] float _delayBetweenCards = 0.055f;

    [Tooltip("How high a card lifts on its way over. 0 slides it flat across the table.")]
    [SerializeField] float _dealArc = 0.5f;

    [SerializeField] Ease _dealEase = Ease.OutQuad;

    [Tooltip("Cards leave the hand this much smaller and grow as they fly. 1 keeps them full size.")]
    [SerializeField] float _dealtFromScale = 0.85f;

    [Tooltip("A beat after the last card lands, before the uncovered ones turn over.")]
    [SerializeField] float _pauseBeforeTurning = 0.15f;

    [Tooltip("The gap between one card turning over and the next.")]
    [SerializeField] float _delayBetweenTurns = 0.05f;

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
    Sequence _deal;
    bool _dealing;

    private IReadOnlyList<SolitaireBoardLayout.Slot> slots;
    private Vector2 center;

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

        slots = layout.EffectiveSlots;
        center = LayoutCenter(slots);

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

        PlacingCards();
    }

    /// <summary>
    /// Deals the board: each card is thrown from the dealing point to its place, a
    /// beat after the one before, and the ones nothing covers turn over once they
    /// have all landed.
    ///
    /// The cards are already sitting where they belong when this runs — it takes them
    /// back to the hand and throws them in. That way the layout stays the one thing
    /// that decides where a card goes, and a board with no dealing point set simply
    /// appears, exactly as it did before.
    /// </summary>
    void PlacingCards()
    {
        StopDealing();

        if (_dealingPoint == null || _dealSeconds <= 0f) return;

        _dealing = true;

        var from = (_boardRoot != null ? _boardRoot : transform).InverseTransformPoint(_dealingPoint.position);
        var scaled = !Mathf.Approximately(_dealtFromScale, 1f);
        var places = new Vector3[_cardViews.Length];

        for (var i = 0; i < _cardViews.Length; i++)
        {
            var card = _cardViews[i];
            places[i] = card.transform.localPosition;

            // Face down and deaf to taps, back in the hand. This has to happen before
            // the tweens are built: DOTween reads where a tween starts from as it is
            // built, not when it comes round to playing it.
            card.TurnNow(false);
            card.Freeze();
            card.transform.localPosition = from;
            if (scaled) card.transform.localScale = Vector3.one * _dealtFromScale;
        }

        _deal = DOTween.Sequence();

        for (var i = 0; i < _cardViews.Length; i++)
        {
            var at = i * _delayBetweenCards;
            var card = _cardViews[i].transform;

            // A jump returns a Sequence and a move a Tweener, so they meet as Tween.
            Tween flight = _dealArc > 0f
                ? card.DOLocalJump(places[i], _dealArc, 1, _dealSeconds).SetEase(_dealEase)
                : (Tween)card.DOLocalMove(places[i], _dealSeconds).SetEase(_dealEase);

            _deal.Insert(at, flight);

            if (scaled) _deal.Insert(at, card.DOScale(Vector3.one, _dealSeconds).SetEase(_dealEase));
        }

        // The last card is thrown last, so the board is down one flight after that.
        var allLanded = (_cardViews.Length - 1) * _delayBetweenCards + _dealSeconds;
        var turnAt = allLanded + _pauseBeforeTurning;
        var turned = 0;

        for (var i = 0; i < _cardViews.Length; i++)
        {
            if (_game.IsBlocked(i)) continue;

            var card = _cardViews[i];
            _deal.InsertCallback(turnAt + turned * _delayBetweenTurns, () => card.TurnOver(true));
            turned++;
        }

        _deal.OnComplete(FinishDealing);
    }

    void FinishDealing()
    {
        _deal = null;
        _dealing = false;

        // Hands the board back to the rules: what is face up, and what answers taps.
        Refresh();
    }

    void StopDealing()
    {
        if (_deal != null && _deal.IsActive()) _deal.Kill();

        _deal = null;
        _dealing = false;
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

            // Mid-deal the cards are still in the air and face down. They are told
            // what they really are all the same, so that each comes up showing its
            // own face and its own colour; what stops them being played is the
            // freeze below, not a pretend blocking that would also grey them.
            _cardViews[i].Show(_game.CardAt(i),
                               faceUp: !blocked && !_dealing,
                               playable: _game.IsPlayable(i),
                               blocked: blocked);
        }

        _wasteView.Show(_game.Active, faceUp: true, playable: false, blocked: false);
        _wasteView.Freeze();

        // Face down, so what the card carries never shows — the draw pile is a back.
        if (_game.StockRemaining > 0)
            _stockView.Show(_game.Active, faceUp: false, playable: false, blocked: false);
        else
            _stockView.Hide();

        // Nothing is played while the board is in the air. A tap on the pile would
        // draw a card, and the refresh that followed would turn the board face up
        // around cards still in flight.
        if (_dealing)
        {
            foreach (var view in _cardViews) view.Freeze();
            _stockView.Freeze();
        }

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
        // No SetLink in this build of DOTween, so the deal has to be put down by hand.
        StopDealing();

        if (_game != null)
        {
            _game.Changed -= Refresh;
            _game.StreakCompleted -= PayForTheRun;
        }
        if (_runtimeLayout != null) Destroy(_runtimeLayout);
    }
}
CLAUDE_SolitaireLevelRunner_cs

if [ -f "$DIR/SolitaireDealAnimation.cs" ]; then rm "$DIR/SolitaireDealAnimation.cs"; echo "removed  SolitaireDealAnimation.cs"; fi
if [ -f "$DIR/SolitaireDealAnimation.cs.meta" ]; then rm "$DIR/SolitaireDealAnimation.cs.meta"; echo "removed  SolitaireDealAnimation.cs.meta"; fi

echo; echo "=============================================="
echo " SUCCESS"
echo "=============================================="
echo "  rm deal-face-v6.sh"
echo "  git add -A"
echo "  git commit -m \"Deal the cards one at a time, with their own faces\""
echo "  git push -u origin Liron"
