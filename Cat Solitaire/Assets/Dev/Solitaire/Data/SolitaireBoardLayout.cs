using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The shape of one board: where every tableau slot sits and which slots lie on
/// top of it. Positions are measured in card widths and heights, never pixels, so
/// the view is free to scale the whole board to whatever screen it lands on.
///
/// There are three ways to get a shape, and they end in the same place:
///
///   * leave everything empty for the classic three peaks;
///   * draw one in <see cref="Pattern"/> and bake it;
///   * describe one in <see cref="Generator"/> and roll seeds until you like it.
///
/// Blockers are never typed in. Whichever route is taken, which card covers which
/// is worked out from where the cards sit, so a board cannot disagree with itself.
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

    [Tooltip("The baked board. Empty falls back to the drawing below, and failing that to the classic three peaks.")]
    public Slot[] Slots;

    [Tooltip("How many of the cards left over after the deal go into the draw pile. The rest sit out the round.")]
    public int StockCount = 23;

    [Header("Drawing")]
    [Tooltip("One line per row, one character per half a card width. Dots and spaces are gaps, " +
             "anything else is a card. Bake it into Slots from the context menu, or leave it " +
             "and it is read at play time.")]
    [TextArea(4, 12)]
    public string PatternText;

    [Header("Generator")]
    [Tooltip("A description to roll boards from. Use 'Roll A New Board' until you like one.")]
    public SolitaireLayoutGenerator.Settings Generator = new SolitaireLayoutGenerator.Settings();

    /// <summary>The drawing, split into rows. Empty when nothing is drawn.</summary>
    public string[] Pattern =>
        string.IsNullOrWhiteSpace(PatternText)
            ? Array.Empty<string>()
            : PatternText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    /// <summary>
    /// The board as the game sees it: what was baked, else what was drawn, else the
    /// classic three peaks.
    /// </summary>
    public IReadOnlyList<Slot> EffectiveSlots
    {
        get
        {
            if (Slots != null && Slots.Length > 0) return Slots;

            var pattern = Pattern;
            return pattern.Length > 0
                ? SolitaireLayoutGenerator.FromPattern(pattern)
                : BuildClassicTriPeaks();
        }
    }

    /// <summary>
    /// The standard 28-card board: three peaks of 1 + 2 + 3 cards sitting on a
    /// shared row of ten. Every card is overlapped by the two cards half a width
    /// to either side of it on the row below, and those two are its blockers.
    /// </summary>
    public static Slot[] BuildClassicTriPeaks() =>
        SolitaireLayoutGenerator.FromPattern(SolitaireLayoutGenerator.ClassicTriPeaks);

    [ContextMenu("Bake Classic Tri-Peaks")]
    void BakeClassicTriPeaks()
    {
        PatternText = string.Join("\n", SolitaireLayoutGenerator.ClassicTriPeaks);
        Slots = BuildClassicTriPeaks();
        Report("Classic tri-peaks");
    }

    /// <summary>Draws a board from the generator settings, without baking it.</summary>
    [ContextMenu("Generate Board")]
    void GenerateBoard()
    {
        PatternText = string.Join("\n", SolitaireLayoutGenerator.GeneratePattern(Generator));
        Slots = Array.Empty<Slot>();
        Report($"Generated from seed {Generator.Seed}");
    }

    /// <summary>Rolls the seed on, for flicking through boards until one looks right.</summary>
    [ContextMenu("Roll A New Board")]
    void RollANewBoard()
    {
        Generator.Seed++;
        GenerateBoard();
    }

    /// <summary>
    /// Freezes whatever is drawn into <see cref="Slots"/>, for nudging a single card
    /// by hand. Blockers are worked out as it bakes, so they stay honest.
    /// </summary>
    [ContextMenu("Bake Drawing Into Slots")]
    void BakeDrawing()
    {
        var pattern = Pattern;

        if (pattern.Length == 0)
        {
            Debug.LogWarning($"{name}: nothing drawn to bake.", this);
            return;
        }

        Slots = SolitaireLayoutGenerator.FromPattern(pattern);
        Report("Baked");
    }

    /// <summary>Goes back to the drawing, so the generator is in charge again.</summary>
    [ContextMenu("Clear Baked Slots")]
    void ClearBakedSlots()
    {
        Slots = Array.Empty<Slot>();
        Report("Cleared");
    }

    [ContextMenu("Check This Board")]
    void CheckThisBoard() => Report("Checked");

    /// <summary>
    /// Deals and plays this board 200 times and prints how it went. Slow enough to be
    /// worth asking for rather than doing on every change, quick enough to sit through.
    /// </summary>
    [ContextMenu("Play This Board 200 Times")]
    void PlayThisBoard()
    {
        var result = SolitaireLayoutProbe.Measure(this);

        Debug.Log($"{name}: {result}\n" + string.Join("\n", Pattern), this);
    }

    /// <summary>
    /// Prints what this board comes to and anything wrong with it. Worth a look
    /// before shipping a level: a board the deck cannot fill throws at deal time.
    /// </summary>
    void Report(string what)
    {
        var slots = EffectiveSlots;
        var problems = SolitaireLayoutGenerator.Problems(slots, StockCount);
        var needed = slots.Count + 1 + Mathf.Max(0, StockCount);

        var summary = $"{name}: {what} — {slots.Count} cards on the board, " +
                      $"{StockCount} in the draw pile, {needed} of {CardDeck.Size} dealt.";

        if (problems.Count == 0)
        {
            Debug.Log(summary + "\n" + string.Join("\n", Pattern), this);
            return;
        }

        Debug.LogWarning(summary + "\n  " + string.Join("\n  ", problems) +
                         "\n" + string.Join("\n", Pattern), this);
    }
}
