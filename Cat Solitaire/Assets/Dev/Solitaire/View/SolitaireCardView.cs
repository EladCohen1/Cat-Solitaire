using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// One card in the world: the card mesh with the atlas material on it. Picking a
/// face is two floats on the shader.
///
/// Card.fbx is two mesh objects back to back, Card_Front and Card_Back, each with one
/// material of its own — so the face lives on one renderer and the back on another.
/// The front is found by material name rather than assumed, because a card built the
/// other way (one mesh, two submeshes) is just as likely and the slot order is the
/// mesh's business either way.
///
/// Only the front is ever written: the back art is whatever CardBack was authored
/// with, and a face-down card is simply a card turned around. Writing a back cell
/// into the front material would leave the real back side showing from behind and
/// give the card two backs.
///
/// Each card gets its own copies of both materials. A MaterialPropertyBlock would be
/// cheaper, but Shader Graph shaders under URP are SRP Batcher compatible, which
/// keeps exposed properties in the per-material constant buffer and ignores block
/// overrides entirely — every card would show whatever the shared material says.
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

    /// <summary>
    /// Works out which renderer and slot hold the readable side of the card, and
    /// whether there is a back side to turn to. Card.fbx keeps the two sides on
    /// separate mesh objects, but a card built as one mesh with two submeshes is just
    /// as valid, so both the renderers and their slots are searched by material name.
    /// </summary>
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

        // No material said it was the face. Rather than write the rank onto whatever
        // comes first — which may well be the back — take the renderer already wired,
        // and failing that the first one with a mesh, and say so.
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

    /// <summary>
    /// Loose enough that Card_Front answers to CardFront, and that a copy named
    /// "CardFront (Instance)" still answers to it.
    /// </summary>
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

    public void Hide() => gameObject.SetActive(false);

    /// <summary>Stops the board answering taps once the round is decided.</summary>
    public void Freeze()
    {
        if (_collider != null) _collider.enabled = false;
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
