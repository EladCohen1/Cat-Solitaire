using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The shape of one board: where every tableau slot sits and which slots lie on
/// top of it. Positions are measured in card widths and heights, never pixels, so
/// the view is free to scale the whole board to whatever screen it lands on.
///
/// Leave <see cref="Slots"/> empty to get the classic three-peak board. Use the
/// context menu to bake that arrangement into the asset when you want to hand-edit it.
/// </summary>
[CreateAssetMenu(menuName = "Cat/Solitaire Board Layout")]
public class SolitaireBoardLayout : ScriptableObject
{
    [Serializable]
    public class Slot
    {
        [Tooltip("In card widths (x) and card heights (y). Larger y is nearer the top of the screen.")]
        public Vector2 Position;

        [Tooltip("Slots overlapping this one. Every one of them must be cleared before this card can be played.")]
        public int[] Blockers = Array.Empty<int>();
    }

    public Slot[] Slots;

    [Tooltip("How many of the cards left over after the deal go into the draw pile. The rest sit out the round.")]
    public int StockCount = 23;

    /// <summary>The authored slots, or the classic three peaks when nothing is authored.</summary>
    public IReadOnlyList<Slot> EffectiveSlots =>
        Slots != null && Slots.Length > 0 ? Slots : BuildClassicTriPeaks();

    /// <summary>
    /// The standard 28-card board: three peaks of 1 + 2 + 3 cards sitting on a
    /// shared row of ten. Every card is overlapped by the two cards half a width
    /// to either side of it on the row below, and those two are its blockers.
    /// </summary>
    public static Slot[] BuildClassicTriPeaks()
    {
        float[][] rows =
        {
            new[] { 1.5f, 4.5f, 7.5f },
            new[] { 1f, 2f, 4f, 5f, 7f, 8f },
            new[] { 0.5f, 1.5f, 2.5f, 3.5f, 4.5f, 5.5f, 6.5f, 7.5f, 8.5f },
            new[] { 0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f },
        };

        const float rowSpacing = 0.5f;   // rows overlap by half a card
        const float overlapReach = 0.6f; // a card is covered by anything within half a width

        var slots = new List<Slot>();
        var rowStart = new int[rows.Length];

        for (var row = 0; row < rows.Length; row++)
        {
            rowStart[row] = slots.Count;
            var y = (rows.Length - 1 - row) * rowSpacing;

            foreach (var x in rows[row])
                slots.Add(new Slot { Position = new Vector2(x, y) });
        }

        // The bottom row is covered by nothing, which is why it starts out playable.
        for (var row = 0; row < rows.Length - 1; row++)
            for (var i = 0; i < rows[row].Length; i++)
            {
                var blockers = new List<int>();

                for (var j = 0; j < rows[row + 1].Length; j++)
                    if (Mathf.Abs(rows[row + 1][j] - rows[row][i]) < overlapReach)
                        blockers.Add(rowStart[row + 1] + j);

                slots[rowStart[row] + i].Blockers = blockers.ToArray();
            }

        return slots.ToArray();
    }

    [ContextMenu("Bake Classic Tri-Peaks")]
    void BakeClassicTriPeaks() => Slots = BuildClassicTriPeaks();
}
