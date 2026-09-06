using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One card on screen. It knows nothing about the rules: it is told what to show
/// and it reports taps. Placeholder art — a panel with the rank and suit on it —
/// so swapping in real card art means changing this file and nothing else.
/// </summary>
public class SolitaireCardView : MonoBehaviour
{
    public static readonly Vector2 CardSize = new Vector2(150f, 210f);

    static readonly Color FaceColor = new Color(0.98f, 0.97f, 0.94f);
    static readonly Color PlayableColor = new Color(1f, 0.95f, 0.65f);
    static readonly Color BlockedColor = new Color(0.62f, 0.62f, 0.66f);
    static readonly Color RedInk = new Color(0.78f, 0.16f, 0.20f);
    static readonly Color BlackInk = new Color(0.12f, 0.12f, 0.16f);

    /// <summary>The tapped card. The runner turns this into a move.</summary>
    public event Action<SolitaireCardView> Clicked;

    public int Slot { get; private set; } = -1;

    RectTransform _rect;
    Image _background;
    TMP_Text _label;
    Button _button;

    public static SolitaireCardView Create(Transform parent, string name, int slot)
    {
        var background = SolitaireUi.Panel(name, parent, CardSize, FaceColor);

        var view = background.gameObject.AddComponent<SolitaireCardView>();
        view.Slot = slot;
        view._rect = (RectTransform)background.transform;
        view._background = background;

        view._button = background.gameObject.AddComponent<Button>();
        view._button.targetGraphic = background;
        view._button.onClick.AddListener(view.OnClicked);

        view._label = SolitaireUi.Label("Face", background.transform, string.Empty, 56f, BlackInk);
        var labelRect = (RectTransform)view._label.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return view;
    }

    public void SetPosition(Vector2 anchoredPosition) => _rect.anchoredPosition = anchoredPosition;

    /// <param name="playable">The card can be taken right now — highlight it.</param>
    /// <param name="blocked">Another card still lies on top of it.</param>
    public void Show(Card card, bool playable, bool blocked)
    {
        gameObject.SetActive(true);

        _label.text = card.RankLabel + "\n" + card.SuitLabel;
        _label.color = blocked ? BlackInk : card.IsRed ? RedInk : BlackInk;

        _background.color = blocked ? BlockedColor : playable ? PlayableColor : FaceColor;

        // A blocked card is scenery: let taps fall through to whatever is on top of it.
        _button.interactable = !blocked;
        _background.raycastTarget = !blocked;
    }

    public void Hide() => gameObject.SetActive(false);

    /// <summary>Stops the board reacting to taps once the round is decided.</summary>
    public void Freeze()
    {
        _button.interactable = false;
        _background.raycastTarget = false;
    }

    void OnClicked() => Clicked?.Invoke(this);

    void OnDestroy() => Clicked = null;
}
