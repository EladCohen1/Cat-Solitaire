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
