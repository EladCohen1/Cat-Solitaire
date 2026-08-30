using System;
using System.Collections.Generic;
using UnityEngine;

public enum SolitaireStatus { Playing, Won, Lost }

/// <summary>
/// The card game itself: the deal, the legal moves, the score, and the decision
/// that a round is over. No MonoBehaviour, no coroutines and nothing on screen —
/// the view reads this and calls <see cref="TryPlay"/> / <see cref="TryDraw"/>,
/// which is what makes the rules testable without ever entering play mode.
///
/// Winning here means the board was cleared. Whether that counts as a won *level*
/// is the runner's call, because only the runner knows the level's target score.
/// </summary>
public class SolitaireGame
{
    public const int PointsPerCard = 100;
    public const int BoardClearBonus = 1000;
    public const int PointsPerUnusedStockCard = 50;

    readonly IReadOnlyList<SolitaireBoardLayout.Slot> _slots;
    readonly Card[] _tableau;
    readonly bool[] _cleared;
    readonly List<Card> _stock;

    int _stockIndex;

    /// <summary>Raised after every accepted move, once the new state is settled.</summary>
    public event Action Changed;

    /// <summary>Raised with the slot a card just left, for the view to animate.</summary>
    public event Action<int> SlotCleared;

    public SolitaireGame(SolitaireBoardLayout layout, int seed)
        : this(layout, CardDeck.BuildShuffled(seed)) { }

    /// <summary>
    /// Deals a known set of cards: tableau slots in index order, then the starting
    /// waste card, then the draw pile. Tests use this to set up an exact board.
    /// </summary>
    public SolitaireGame(SolitaireBoardLayout layout, IReadOnlyList<Card> deal)
    {
        if (layout == null) throw new ArgumentNullException(nameof(layout));
        if (deal == null) throw new ArgumentNullException(nameof(deal));

        _slots = layout.EffectiveSlots;
        if (_slots == null || _slots.Count == 0)
            throw new ArgumentException("Board layout has no slots.", nameof(layout));

        var stockCount = Mathf.Max(0, layout.StockCount);
        var needed = _slots.Count + 1 + stockCount;   // tableau + the face-up card + the pile

        if (deal.Count < needed)
            throw new ArgumentException(
                $"Layout needs {needed} cards ({_slots.Count} tableau + 1 waste + {stockCount} stock) but was dealt {deal.Count}.",
                nameof(deal));

        _tableau = new Card[_slots.Count];
        _cleared = new bool[_slots.Count];

        for (var i = 0; i < _slots.Count; i++)
        {
            _tableau[i] = deal[i];

            var blockers = _slots[i].Blockers;
            if (blockers == null) continue;

            foreach (var blocker in blockers)
                if (blocker < 0 || blocker >= _slots.Count)
                    throw new ArgumentException($"Slot {i} lists blocker {blocker}, which is not a slot.", nameof(layout));
        }

        Active = deal[_slots.Count];

        _stock = new List<Card>(stockCount);
        for (var i = 0; i < stockCount; i++)
            _stock.Add(deal[_slots.Count + 1 + i]);
    }

    public SolitaireStatus Status { get; private set; } = SolitaireStatus.Playing;

    /// <summary>The face-up card everything is played onto.</summary>
    public Card Active { get; private set; }

    public int SlotCount => _tableau.Length;
    public int StockRemaining => _stock.Count - _stockIndex;
    public int CardsCleared { get; private set; }
    public int CardsRemaining => _tableau.Length - CardsCleared;
    public int Score { get; private set; }

    /// <summary>How many cards have come off the board since the last draw. Drives the scoring streak.</summary>
    public int Combo { get; private set; }

    public Card CardAt(int slot) => _tableau[slot];
    public Vector2 PositionOf(int slot) => _slots[slot].Position;
    public bool IsCleared(int slot) => _cleared[slot];

    /// <summary>True while another card still lies on top of this one.</summary>
    public bool IsBlocked(int slot)
    {
        var blockers = _slots[slot].Blockers;
        if (blockers == null) return false;

        foreach (var blocker in blockers)
            if (!_cleared[blocker]) return true;

        return false;
    }

    public bool IsPlayable(int slot) =>
        Status == SolitaireStatus.Playing &&
        !_cleared[slot] &&
        !IsBlocked(slot) &&
        _tableau[slot].Matches(Active);

    public bool HasPlayableSlot()
    {
        for (var i = 0; i < _tableau.Length; i++)
            if (IsPlayable(i)) return true;

        return false;
    }

    /// <summary>Takes the card in this slot onto the waste pile. Rejects anything illegal.</summary>
    public bool TryPlay(int slot)
    {
        if (slot < 0 || slot >= _tableau.Length) return false;
        if (!IsPlayable(slot)) return false;

        Active = _tableau[slot];
        _cleared[slot] = true;
        CardsCleared++;

        // Each card in an unbroken run is worth more than the last — that streak is
        // the whole reason to hunt for chains instead of drawing.
        Combo++;
        Score += PointsPerCard * Combo;

        SlotCleared?.Invoke(slot);
        Settle();
        return true;
    }

    /// <summary>Turns the top card of the draw pile face up, which breaks the streak.</summary>
    public bool TryDraw()
    {
        if (Status != SolitaireStatus.Playing || StockRemaining == 0) return false;

        Active = _stock[_stockIndex++];
        Combo = 0;

        Settle();
        return true;
    }

    void Settle()
    {
        if (Status == SolitaireStatus.Playing)
        {
            if (CardsCleared == _tableau.Length)
            {
                Score += BoardClearBonus + StockRemaining * PointsPerUnusedStockCard;
                Status = SolitaireStatus.Won;
            }
            else if (StockRemaining == 0 && !HasPlayableSlot())
            {
                Status = SolitaireStatus.Lost;
            }
        }

        Changed?.Invoke();
    }
}
