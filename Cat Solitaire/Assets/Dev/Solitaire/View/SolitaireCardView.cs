using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// One card in the world: the card mesh with the atlas material on it. Picking a
/// face is two floats on the shader, written through a MaterialPropertyBlock so
/// every card shares one material and no instances are made.
///
/// It knows nothing about the rules — it is told what to show and it reports taps.
/// Taps need a PhysicsRaycaster on the level camera and a 3D collider — a Collider2D
/// is a different physics world and the raycaster will not see it.
///
/// An FBX drops in as a wrapper with the mesh on a child, so the renderer and the
/// collider are looked for in children as well as here.
/// </summary>
public class SolitaireCardView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] MeshRenderer _renderer;
    [SerializeField] Collider _collider;

    [Header("Atlas")]
    [Tooltip("Cells across and down the atlas texture.")]
    [SerializeField] int _columns = 8;
    [SerializeField] int _rows = 8;

    [Tooltip("Cell the card back sits in. The rest follow it: 2 up to Ace, suit after suit.")]
    [SerializeField] int _backTile;

    [Tooltip("How many suits the atlas actually holds. While only one is drawn, every " +
             "suit borrows it — the rules do not care about suit, only the art does.")]
    [SerializeField, Min(1)] int _suitsInAtlas = 1;

    [Header("Shader")]
    [SerializeField] string _tileXProperty = "_Tile_X";
    [SerializeField] string _tileYProperty = "_Tile_Y";

    [Tooltip("Off writes whole cell numbers. On writes a 0-1 offset instead, for a " +
             "shader that does not scale them itself.")]
    [SerializeField] bool _normalizeTile;

    [Tooltip("UVs start at the bottom of a texture while the atlas reads from the top, " +
             "so the row usually has to be counted the other way.")]
    [SerializeField] bool _countRowsFromBottom = true;

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

    MaterialPropertyBlock _block;

    void Reset() => FindParts();

    void Awake()
    {
        FindParts();
        _block = new MaterialPropertyBlock();

        if (_renderer == null)
            Debug.LogError($"{name}: no MeshRenderer here or under it — nothing to draw a card on.", this);
    }

    void FindParts()
    {
        if (_renderer == null) _renderer = GetComponentInChildren<MeshRenderer>();
        if (_collider == null) _collider = GetComponentInChildren<Collider>();
    }

    public void Bind(int slot) => Slot = slot;

    /// <param name="playable">Can be taken right now — lit.</param>
    /// <param name="blocked">Still covered by another card — dimmed and deaf to taps.</param>
    public void Show(Card card, bool faceUp, bool playable, bool blocked)
    {
        gameObject.SetActive(true);

        SetTile(faceUp ? TileOf(card) : _backTile);
        SetTint(blocked ? _blockedTint : playable ? _playableTint : _faceTint);

        // A covered card is scenery: let the tap reach whatever lies on top of it.
        if (_collider != null) _collider.enabled = !blocked;
    }

    public void Hide() => gameObject.SetActive(false);

    /// <summary>Stops the board answering taps once the round is decided.</summary>
    public void Freeze()
    {
        if (_collider != null) _collider.enabled = false;
    }

    public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(this);

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
        if (_renderer == null) return;

        var columns = Mathf.Max(1, _columns);
        var rows = Mathf.Max(1, _rows);

        var x = tile % columns;
        var y = tile / columns;
        if (_countRowsFromBottom) y = rows - 1 - y;

        _renderer.GetPropertyBlock(_block);
        _block.SetFloat(_tileXProperty, _normalizeTile ? x / (float)columns : x);
        _block.SetFloat(_tileYProperty, _normalizeTile ? y / (float)rows : y);
        _renderer.SetPropertyBlock(_block);
    }

    void SetTint(Color color)
    {
        if (_renderer == null || string.IsNullOrEmpty(_tintProperty)) return;

        _renderer.GetPropertyBlock(_block);
        _block.SetColor(_tintProperty, color);
        _renderer.SetPropertyBlock(_block);
    }

    void OnDestroy() => Clicked = null;
}
