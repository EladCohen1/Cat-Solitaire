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
    readonly List<Move> _history = new List<Move>();

    int _stockIndex;

    /// <summary>
    /// The longest streak already paid for this round. Undo never lowers it, so
    /// taking a move back and playing it again cannot be farmed for the bonus.
    /// </summary>
    int _highestComboPaid;

    /// <summary>
    /// Everything a move changed that cannot be worked out again afterwards. Undo
    /// restores these rather than replaying the round, so taking back a move costs
    /// the same whether it is the first or the fiftieth.
    /// </summary>
    readonly struct Move
    {
        public readonly bool WasDraw;
        public readonly int Slot;
        public readonly Card PreviousActive;
        public readonly int Score;
        public readonly int Combo;
        public readonly bool SpentWildcard;

        public Move(bool wasDraw, int slot, Card previousActive, int score, int combo, bool spentWildcard)
        {
            WasDraw = wasDraw;
            Slot = slot;
            PreviousActive = previousActive;
            Score = score;
            Combo = combo;
            SpentWildcard = spentWildcard;
        }
    }

    /// <summary>Raised after every accepted move, once the new state is settled.</summary>
    public event Action Changed;

    /// <summary>Raised with the slot a card just left, for the view to animate.</summary>
    public event Action<int> SlotCleared;

    /// <summary>Raised with the slot a card just came back to, undoing a play.</summary>
    public event Action<int> SlotRestored;

    /// <summary>
    /// Raised when an unbroken run reaches <see cref="StreakLength"/>. Paying for it
    /// is the listener's business — the rules do not know what a coin is.
    /// </summary>
    public event Action StreakCompleted;

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

    /// <summary>
    /// Cards in a run before the streak pays out. 0 turns the bonus off entirely,
    /// which is what a level that does not use it leaves it as.
    /// </summary>
    public int StreakLength { get; set; }

    /// <summary>
    /// How full the streak meter is, 0 up to <see cref="StreakLength"/> - 1. The
    /// scoring streak itself keeps running past a payout; only the meter wraps.
    /// </summary>
    public int StreakMeter => StreakLength <= 0 ? 0 : Combo % StreakLength;

    /// <summary>
    /// While this is on, the next card that needs it can be played whatever its rank.
    /// Playing a card that already matched leaves it armed — it is spent on the play
    /// that could not have happened without it.
    /// </summary>
    public bool WildcardArmed { get; private set; }

    /// <summary>A move has been made this round that can be taken back.</summary>
    public bool CanUndo => Status == SolitaireStatus.Playing && _history.Count > 0;

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
        (WildcardArmed || _tableau[slot].Matches(Active));

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

        // A wildcard is spent on the play it made possible, not on one that was legal
        // anyway — so check before the active card changes out from under it.
        var neededWildcard = WildcardArmed && !_tableau[slot].Matches(Active);

        _history.Add(new Move(false, slot, Active, Score, Combo, neededWildcard));

        Active = _tableau[slot];
        _cleared[slot] = true;
        CardsCleared++;
        if (neededWildcard) WildcardArmed = false;

        // Each card in an unbroken run is worth more than the last — that streak is
        // the whole reason to hunt for chains instead of drawing.
        Combo++;
        Score += PointsPerCard * Combo;

        SlotCleared?.Invoke(slot);

        if (StreakLength > 0 && Combo % StreakLength == 0 && Combo > _highestComboPaid)
        {
            _highestComboPaid = Combo;
            StreakCompleted?.Invoke();
        }

        Settle();
        return true;
    }

    /// <summary>Turns the top card of the draw pile face up, which breaks the streak.</summary>
    public bool TryDraw()
    {
        if (Status != SolitaireStatus.Playing || StockRemaining == 0) return false;

        _history.Add(new Move(true, -1, Active, Score, Combo, false));

        Active = _stock[_stockIndex++];
        Combo = 0;

        // The run is over, so the next one starts the meter from nothing.
        _highestComboPaid = 0;

        Settle();
        return true;
    }

    /// <summary>
    /// Takes the last move back: a played card returns to the board, a drawn card
    /// returns to the pile, and the score and streak go back with them.
    ///
    /// What it will not give back is a streak bonus already paid. The run it was
    /// earned with can be replayed, but it will not pay twice, or taking a move back
    /// would be a way of printing money.
    /// </summary>
    public bool TryUndo()
    {
        if (!CanUndo) return false;

        var move = _history[_history.Count - 1];
        _history.RemoveAt(_history.Count - 1);

        if (move.WasDraw) _stockIndex--;
        else
        {
            _cleared[move.Slot] = false;
            CardsCleared--;
        }

        Active = move.PreviousActive;
        Score = move.Score;
        Combo = move.Combo;
        if (move.SpentWildcard) WildcardArmed = true;

        // Whatever run we have been put back into, it has already paid out once.
        _highestComboPaid = Mathf.Max(_highestComboPaid, Combo);

        if (!move.WasDraw) SlotRestored?.Invoke(move.Slot);

        Settle();
        return true;
    }

    /// <summary>
    /// Arms the wildcard, so the next card that needs it plays whatever its rank.
    /// Charging for it is the caller's business.
    /// </summary>
    public bool TryArmWildcard()
    {
        if (Status != SolitaireStatus.Playing || WildcardArmed) return false;

        WildcardArmed = true;

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
