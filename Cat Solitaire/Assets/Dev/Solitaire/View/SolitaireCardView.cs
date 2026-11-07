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
