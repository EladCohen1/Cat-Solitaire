using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays a board over and over to see what kind of round it gives, so a shape can be
/// judged before a level is built on it. A board that is never cleared is a wall, and
/// one that clears every time is scenery.
///
/// It plays the real <see cref="SolitaireGame"/>, so what it measures is the game as
/// shipped rather than a second copy of the rules that could drift from it. It plays
/// well but not perfectly — a player who never misses would clear more of these boards
/// than this does — so read the number as how one board compares with another, not as
/// the share of players who will win.
/// </summary>
public static class SolitaireLayoutProbe
{
    public struct Result
    {
        public int Deals;
        public int Cleared;
        public int CardsOnBoard;
        public float AverageCardsTaken;
        public float AverageStockLeft;

        public float ClearRate => Deals == 0 ? 0f : Cleared / (float)Deals;

        public override string ToString() =>
            $"cleared {Cleared} of {Deals} deals ({ClearRate * 100f:0.#}%), " +
            $"average {AverageCardsTaken:0.#} of {CardsOnBoard} cards taken, " +
            $"{AverageStockLeft:0.#} left in the draw pile";
    }

    /// <summary>
    /// Deals this board the given number of times and plays each one out. Seeds run
    /// on from <paramref name="firstSeed"/>, so the same call always reports the same
    /// numbers and two boards can be compared on the same deals.
    /// </summary>
    public static Result Measure(SolitaireBoardLayout layout, int deals = 200, int firstSeed = 1)
    {
        var result = new Result();
        if (layout == null || deals <= 0) return result;

        var slots = layout.EffectiveSlots;
        if (slots == null || slots.Count == 0) return result;

        // Who covers whom, read the other way round: taking a card frees whatever
        // was waiting on it, and that is the question the strategy keeps asking.
        var covers = CoveredBy(slots);

        var takenTotal = 0;
        var stockTotal = 0;

        for (var i = 0; i < deals; i++)
        {
            SolitaireGame game;

            try
            {
                game = new SolitaireGame(layout, firstSeed + i);
            }
            catch (System.Exception e)
            {
                // A board the deck cannot fill is a fact about the board, not a crash
                // to hide: report nothing rather than a number that means nothing.
                Debug.LogWarning($"Cannot deal this board: {e.Message}");
                return result;
            }

            Play(game, slots, covers);

            if (game.CardsRemaining == 0) result.Cleared++;
            takenTotal += game.CardsCleared;
            stockTotal += game.StockRemaining;
        }

        result.Deals = deals;
        result.CardsOnBoard = slots.Count;
        result.AverageCardsTaken = takenTotal / (float)deals;
        result.AverageStockLeft = stockTotal / (float)deals;

        return result;
    }

    static void Play(SolitaireGame game, IReadOnlyList<SolitaireBoardLayout.Slot> slots, List<int>[] covers)
    {
        // Every move either takes a card or a card off the draw pile, so the round
        // cannot run longer than those two put together. The guard is for a rules
        // change that breaks that, not for anything the rules do today.
        var moves = slots.Count + game.StockRemaining + 1;

        while (moves-- > 0 && game.Status == SolitaireStatus.Playing)
        {
            var move = BestMove(game, slots, covers);

            if (move >= 0) game.TryPlay(move);
            else if (!game.TryDraw()) break;
        }
    }

    /// <summary>
    /// Takes whichever playable card opens up the most, because a card that frees
    /// nothing can usually wait and one that frees two rarely can.
    /// </summary>
    static int BestMove(SolitaireGame game, IReadOnlyList<SolitaireBoardLayout.Slot> slots, List<int>[] covers)
    {
        var best = -1;
        var bestFreed = -1;

        for (var slot = 0; slot < game.SlotCount; slot++)
        {
            if (!game.IsPlayable(slot)) continue;

            var freed = 0;

            foreach (var waiting in covers[slot])
                if (!game.IsCleared(waiting) && OnlyBlockerLeft(game, slots[waiting].Blockers, slot))
                    freed++;

            if (freed <= bestFreed) continue;

            bestFreed = freed;
            best = slot;
        }

        return best;
    }

    /// <summary>True when this is the last card lying on that one.</summary>
    static bool OnlyBlockerLeft(SolitaireGame game, int[] blockers, int slot)
    {
        if (blockers == null) return false;

        foreach (var blocker in blockers)
            if (blocker != slot && !game.IsCleared(blocker)) return false;

        return true;
    }

    /// <summary>For each slot, the slots it is lying on top of.</summary>
    static List<int>[] CoveredBy(IReadOnlyList<SolitaireBoardLayout.Slot> slots)
    {
        var covers = new List<int>[slots.Count];
        for (var i = 0; i < covers.Length; i++) covers[i] = new List<int>();

        for (var i = 0; i < slots.Count; i++)
        {
            var blockers = slots[i].Blockers;
            if (blockers == null) continue;

            foreach (var blocker in blockers)
                if (blocker >= 0 && blocker < covers.Length)
                    covers[blocker].Add(i);
        }

        return covers;
    }
}
