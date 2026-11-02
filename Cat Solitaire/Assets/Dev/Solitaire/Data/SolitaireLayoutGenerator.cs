using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds board shapes, so nobody has to type slot positions or work out by hand
/// which card covers which.
///
/// A board is drawn as text: one line per row, one character per half a card width,
/// which is the step peaks are built on so every card lands on a column. Dots and
/// spaces are gaps, anything else is a card. The classic three peaks are just this:
///
///     ...X.....X.....X...
///     ..X.X...X.X...X.X..
///     .X.X.X.X.X.X.X.X.X.
///     X.X.X.X.X.X.X.X.X.X
///
/// Blockers are never authored. Which card covers which is a question about where
/// they sit, so it is answered from where they sit — draw the shape and the rules
/// follow. That also means a hand-drawn board and a generated one are the same kind
/// of thing, and neither can disagree with itself about what is covered.
/// </summary>
public static class SolitaireLayoutGenerator
{
    /// <summary>One column of the drawing is half a card wide.</summary>
    public const float ColumnWidth = 0.5f;

    /// <summary>Rows overlap by half a card height.</summary>
    public const float RowSpacing = 0.5f;

    /// <summary>A card is covered by anything within half a width of it on the row below.</summary>
    public const float OverlapReach = 0.6f;

    /// <summary>What the three-peak board looks like drawn out.</summary>
    public static readonly string[] ClassicTriPeaks =
    {
        "...X.....X.....X...",
        "..X.X...X.X...X.X..",
        ".X.X.X.X.X.X.X.X.X.",
        "X.X.X.X.X.X.X.X.X.X",
    };

    [Serializable]
    public class Settings
    {
        [Tooltip("Same number, same board. Change it for a different board of the same description.")]
        public int Seed = 1;

        [Tooltip("How many peaks, picked anywhere between the two. Classic tri-peaks is three.")]
        public Vector2Int Peaks = new Vector2Int(3, 3);

        [Tooltip("Rows in a peak, not counting the row they all stand on. Classic is three.")]
        public Vector2Int PeakRows = new Vector2Int(3, 3);

        [Tooltip("On, peaks can come out different heights; off, they all match.")]
        public bool RaggedPeaks;

        [Tooltip("Card widths from one peak's middle to the next. Below the width of a peak " +
                 "they grow into each other and cards start sharing blockers.")]
        public float PeakSpacing = 3f;

        [Tooltip("How many cards the finished board should hold. The row along the bottom is " +
                 "widened or narrowed to reach it. 0 lets the shape decide.")]
        public int TargetCardCount = 28;
    }

    struct Cell
    {
        public int Row;
        public int Column;
    }

    /// <summary>
    /// Draws a board from a description. Peaks stand on one shared row however tall
    /// they are, so a short peak starts further down rather than hanging in the air.
    /// </summary>
    public static string[] GeneratePattern(Settings settings)
    {
        if (settings == null) settings = new Settings();

        var random = new System.Random(settings.Seed);

        var peakCount = Mathf.Max(1, PickBetween(random, settings.Peaks));
        var matchedHeight = Mathf.Max(1, PickBetween(random, settings.PeakRows));

        var heights = new List<int>(peakCount);
        for (var p = 0; p < peakCount; p++)
            heights.Add(settings.RaggedPeaks
                ? Mathf.Max(1, PickBetween(random, settings.PeakRows))
                : matchedHeight);

        var spacing = Mathf.Max(2, Mathf.RoundToInt(settings.PeakSpacing / ColumnWidth));

        // The row along the bottom is what the card count is tuned with, so the peaks
        // have to leave room for one. Peaks that would fill the board on their own are
        // cut down until they do — a description that cannot be honoured exactly is
        // worth less than a board of the size the level was costed for.
        List<Cell> cards;
        int tallest;

        while (true)
        {
            cards = BuildPeaks(heights, spacing, out tallest);

            if (settings.TargetCardCount <= 0) break;

            var span = ColumnRange(cards, tallest - 1);
            if (cards.Count + SmallestBaseRow(span) <= settings.TargetCardCount) break;

            if (!Shrink(heights)) break;
        }

        AddBaseRow(cards, settings, tallest);

        return Draw(cards);
    }

    /// <summary>Places every peak, bottom-aligned, and says how many rows deep they go.</summary>
    static List<Cell> BuildPeaks(List<int> heights, int spacing, out int tallest)
    {
        tallest = 0;
        foreach (var height in heights) tallest = Mathf.Max(tallest, height);

        var centres = new int[heights.Count];
        for (var p = 0; p < heights.Count; p++) centres[p] = p * spacing;

        // Every peak's bottom row wants the same half-card offset, so that the row they
        // all stand on sits between their cards rather than squarely underneath them.
        // A peak of a different height lands on the other offset, so nudge it over.
        if (heights.Count > 0)
        {
            var wantedParity = Parity(centres[0] - heights[0] + 1);

            for (var p = 1; p < heights.Count; p++)
                if (Parity(centres[p] - heights[p] + 1) != wantedParity)
                    centres[p] += 1;
        }

        var cards = new List<Cell>();
        var taken = new HashSet<long>();

        for (var p = 0; p < heights.Count; p++)
            for (var depth = 0; depth < heights[p]; depth++)
            {
                var row = tallest - heights[p] + depth;

                for (var i = 0; i <= depth; i++)
                {
                    var column = centres[p] - depth + 2 * i;

                    // Peaks set close together share cards where they meet. One card is
                    // one card: counting it twice is what makes a board come out short
                    // of the size the level was costed for.
                    if (taken.Add(Key(row, column)))
                        cards.Add(new Cell { Row = row, Column = column });
                }
            }

        return cards;
    }

    /// <summary>
    /// Takes a row off the tallest peak, and when they are all down to a single card,
    /// takes a peak away. False once there is nothing left to give.
    /// </summary>
    static bool Shrink(List<int> heights)
    {
        var tallest = -1;

        for (var i = 0; i < heights.Count; i++)
            if (tallest < 0 || heights[i] > heights[tallest]) tallest = i;

        if (tallest < 0) return false;

        if (heights[tallest] > 1)
        {
            heights[tallest]--;
            return true;
        }

        if (heights.Count > 1)
        {
            heights.RemoveAt(heights.Count - 1);
            return true;
        }

        return false;
    }

    /// <summary>
    /// The narrowest row the peaks can stand on without a card hanging over the end
    /// of it. Reaching one card past the peaks at either end is the roomy version.
    /// </summary>
    static int SmallestBaseRow((int min, int max) span) =>
        span.min > span.max ? 0 : Mathf.Max(1, (span.max - span.min) / 2);

    static int RoomyBaseRow((int min, int max) span) =>
        span.min > span.max ? 0 : (span.max - span.min) / 2 + 2;

    /// <summary>
    /// The row every peak stands on. Its width is what the card count is tuned with,
    /// because widening it adds cards without disturbing the peaks.
    /// </summary>
    static void AddBaseRow(List<Cell> cards, Settings settings, int baseRow)
    {
        var span = ColumnRange(cards, baseRow - 1);
        if (span.min > span.max) return;

        var count = settings.TargetCardCount > 0
            ? settings.TargetCardCount - cards.Count
            : RoomyBaseRow(span);

        if (count <= 0) return;

        var middle = Mathf.FloorToInt((span.min + span.max) / 2f);
        var start = middle - (count - 1);

        // Sitting squarely under the peaks instead of between them would cover each
        // card with exactly one other, which is a stack rather than a board.
        if (Parity(start) == Parity(span.min)) start -= 1;

        for (var i = 0; i < count; i++)
            cards.Add(new Cell { Row = baseRow, Column = start + 2 * i });
    }

    /// <summary>Turns placed cards into the drawing, trimmed to what is actually used.</summary>
    static string[] Draw(List<Cell> cards)
    {
        if (cards.Count == 0) return Array.Empty<string>();

        int minColumn = int.MaxValue, maxColumn = int.MinValue, maxRow = 0;

        foreach (var card in cards)
        {
            minColumn = Mathf.Min(minColumn, card.Column);
            maxColumn = Mathf.Max(maxColumn, card.Column);
            maxRow = Mathf.Max(maxRow, card.Row);
        }

        var width = maxColumn - minColumn + 1;
        var rows = new char[maxRow + 1][];

        for (var row = 0; row <= maxRow; row++)
        {
            rows[row] = new char[width];
            for (var column = 0; column < width; column++) rows[row][column] = '.';
        }

        foreach (var card in cards) rows[card.Row][card.Column - minColumn] = 'X';

        var drawn = new string[rows.Length];
        for (var row = 0; row < rows.Length; row++) drawn[row] = new string(rows[row]);

        return drawn;
    }

    /// <summary>
    /// Reads a drawing into slots, working out from the positions themselves which
    /// cards cover which. Slots come out row by row from the top, left to right.
    /// </summary>
    public static SolitaireBoardLayout.Slot[] FromPattern(IReadOnlyList<string> pattern)
    {
        if (pattern == null || pattern.Count == 0) return Array.Empty<SolitaireBoardLayout.Slot>();

        var columnsByRow = new List<List<int>>();

        foreach (var line in pattern)
        {
            var columns = new List<int>();

            if (line != null)
                for (var column = 0; column < line.Length; column++)
                    if (IsCard(line[column]))
                        columns.Add(column);

            columnsByRow.Add(columns);
        }

        // A drawing that opens or closes with blank lines would otherwise push the
        // board off its own baseline, and an empty row below is not a row of cards.
        while (columnsByRow.Count > 0 && columnsByRow[columnsByRow.Count - 1].Count == 0)
            columnsByRow.RemoveAt(columnsByRow.Count - 1);

        while (columnsByRow.Count > 0 && columnsByRow[0].Count == 0)
            columnsByRow.RemoveAt(0);

        var rowCount = columnsByRow.Count;
        var slots = new List<SolitaireBoardLayout.Slot>();
        var rowStart = new int[rowCount];

        for (var row = 0; row < rowCount; row++)
        {
            rowStart[row] = slots.Count;
            var y = (rowCount - 1 - row) * RowSpacing;

            foreach (var column in columnsByRow[row])
                slots.Add(new SolitaireBoardLayout.Slot
                {
                    Position = new Vector2(column * ColumnWidth, y),
                });
        }

        // The bottom row is covered by nothing, which is why it starts out playable.
        for (var row = 0; row < rowCount - 1; row++)
            for (var i = 0; i < columnsByRow[row].Count; i++)
            {
                var x = columnsByRow[row][i] * ColumnWidth;
                var blockers = new List<int>();

                for (var j = 0; j < columnsByRow[row + 1].Count; j++)
                    if (Mathf.Abs(columnsByRow[row + 1][j] * ColumnWidth - x) < OverlapReach)
                        blockers.Add(rowStart[row + 1] + j);

                slots[rowStart[row] + i].Blockers = blockers.ToArray();
            }

        return slots.ToArray();
    }

    /// <summary>Draws slots back out as a pattern, so a baked board can still be read.</summary>
    public static string[] ToPattern(IReadOnlyList<SolitaireBoardLayout.Slot> slots)
    {
        if (slots == null || slots.Count == 0) return Array.Empty<string>();

        var cards = new List<Cell>();
        float highest = float.MinValue;

        foreach (var slot in slots) highest = Mathf.Max(highest, slot.Position.y);

        foreach (var slot in slots)
            cards.Add(new Cell
            {
                Row = Mathf.RoundToInt((highest - slot.Position.y) / RowSpacing),
                Column = Mathf.RoundToInt(slot.Position.x / ColumnWidth),
            });

        return Draw(cards);
    }

    /// <summary>
    /// What is wrong with a board, in the order a designer would want to hear it.
    /// Empty means nothing is wrong.
    /// </summary>
    public static List<string> Problems(IReadOnlyList<SolitaireBoardLayout.Slot> slots, int stockCount)
    {
        var problems = new List<string>();

        if (slots == null || slots.Count == 0)
        {
            problems.Add("The board has no cards on it.");
            return problems;
        }

        var needed = slots.Count + 1 + Mathf.Max(0, stockCount);

        if (needed > CardDeck.Size)
            problems.Add($"Needs {needed} cards ({slots.Count} on the board + 1 face up + {stockCount} " +
                         $"in the draw pile) but a deck holds {CardDeck.Size}. Take {needed - CardDeck.Size} " +
                         "off the board or out of the draw pile.");

        for (var i = 0; i < slots.Count; i++)
        {
            var blockers = slots[i].Blockers;
            if (blockers == null) continue;

            foreach (var blocker in blockers)
            {
                if (blocker < 0 || blocker >= slots.Count)
                    problems.Add($"Card {i} is covered by card {blocker}, which is not on the board.");
                else if (blocker == i)
                    problems.Add($"Card {i} covers itself, so it can never be played.");
            }
        }

        // A card off the bottom row with nothing covering it can be played from the
        // word go and looks like it is hanging in mid-air. Nearly always a row drawn
        // too narrow to hold up the one above it.
        var lowest = float.MaxValue;
        foreach (var slot in slots) lowest = Mathf.Min(lowest, slot.Position.y);

        var floating = 0;
        foreach (var slot in slots)
            if (slot.Position.y > lowest + 0.001f && (slot.Blockers == null || slot.Blockers.Length == 0))
                floating++;

        if (floating > 0)
            problems.Add($"{floating} card(s) sit above the bottom row with nothing covering them, so " +
                         "they are playable from the start. Widen the row underneath them.");

        for (var i = 0; i < slots.Count; i++)
            for (var j = i + 1; j < slots.Count; j++)
                if ((slots[i].Position - slots[j].Position).sqrMagnitude < 0.0001f)
                {
                    problems.Add($"Cards {i} and {j} sit on the same spot.");
                    break;
                }

        return problems;
    }

    static bool IsCard(char c) => c != '.' && c != ' ' && c != '_' && c != '\t';

    static int Parity(int value) => ((value % 2) + 2) % 2;

    static long Key(int row, int column) => ((long)row << 32) ^ (uint)column;

    static (int min, int max) ColumnRange(List<Cell> cards, int row)
    {
        int min = int.MaxValue, max = int.MinValue;

        foreach (var card in cards)
            if (card.Row == row)
            {
                min = Mathf.Min(min, card.Column);
                max = Mathf.Max(max, card.Column);
            }

        return (min, max);
    }

    /// <summary>Inclusive both ends, and tolerant of the two being the wrong way round.</summary>
    static int PickBetween(System.Random random, Vector2Int range)
    {
        var low = Mathf.Min(range.x, range.y);
        var high = Mathf.Max(range.x, range.y);

        return low == high ? low : random.Next(low, high + 1);
    }
}
