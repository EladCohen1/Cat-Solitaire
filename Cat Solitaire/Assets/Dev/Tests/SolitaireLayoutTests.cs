using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The board shapes themselves: that a drawing reads back as the board it looks
/// like, that the generator honours what it was asked for, and that a board which
/// cannot be dealt says so instead of throwing in front of a player.
/// </summary>
public class SolitaireLayoutTests
{
    readonly List<Object> _created = new();

    [TearDown]
    public void DestroyLayouts()
    {
        foreach (var asset in _created) Object.DestroyImmediate(asset);
        _created.Clear();
    }

    SolitaireBoardLayout Layout(string[] pattern, int stockCount = 23)
    {
        var layout = ScriptableObject.CreateInstance<SolitaireBoardLayout>();
        _created.Add(layout);
        layout.PatternText = string.Join("\n", pattern);
        layout.StockCount = stockCount;
        return layout;
    }

    static SolitaireLayoutGenerator.Settings Rolling(int seed) => new()
    {
        Seed = seed,
        Peaks = new Vector2Int(2, 4),
        PeakRows = new Vector2Int(2, 4),
        RaggedPeaks = true,
        TargetCardCount = 28,
    };

    [Test]
    public void ClassicDrawingIsTheClassicBoard()
    {
        var slots = SolitaireBoardLayout.BuildClassicTriPeaks();

        Assert.AreEqual(28, slots.Length, "the classic board is 28 cards");
        Assert.IsEmpty(SolitaireLayoutGenerator.Problems(slots, 23));
    }

    [Test]
    public void DefaultSettingsDrawTheClassicBoard()
    {
        var drawn = SolitaireLayoutGenerator.GeneratePattern(new SolitaireLayoutGenerator.Settings());

        Assert.AreEqual(SolitaireLayoutGenerator.ClassicTriPeaks, drawn);
    }

    [Test]
    public void OnlyTheBottomRowStartsUncovered()
    {
        foreach (var slot in SolitaireBoardLayout.BuildClassicTriPeaks())
            if (slot.Position.y > 0.001f)
                Assert.IsNotEmpty(slot.Blockers, $"the card at {slot.Position} is covered by nothing");
    }

    [Test]
    public void BlockersComeFromThePositionsNotTheAuthor()
    {
        //  X     one card resting on the two below it
        // X X
        var slots = SolitaireLayoutGenerator.FromPattern(new[] { ".X.", "X.X" });

        Assert.AreEqual(3, slots.Length);
        Assert.AreEqual(new[] { 1, 2 }, slots[0].Blockers, "the top card is held by both below it");
        Assert.IsEmpty(slots[1].Blockers);
        Assert.IsEmpty(slots[2].Blockers);
    }

    [Test]
    public void DotsAndSpacesAreBothGaps()
    {
        Assert.AreEqual(
            SolitaireLayoutGenerator.FromPattern(new[] { ".X.", "X.X" }).Length,
            SolitaireLayoutGenerator.FromPattern(new[] { " X ", "X X" }).Length);
    }

    [Test]
    public void SlotsDrawBackOutAsTheSamePicture()
    {
        var slots = SolitaireBoardLayout.BuildClassicTriPeaks();

        Assert.AreEqual(SolitaireLayoutGenerator.ClassicTriPeaks,
                        SolitaireLayoutGenerator.ToPattern(slots));
    }

    [Test]
    public void RolledBoardsHitTheCardCountTheyWereAskedFor([Range(1, 40)] int seed)
    {
        var slots = SolitaireLayoutGenerator.FromPattern(
            SolitaireLayoutGenerator.GeneratePattern(Rolling(seed)));

        Assert.AreEqual(28, slots.Length, $"seed {seed} drew {slots.Length} cards, not 28");
        Assert.IsEmpty(SolitaireLayoutGenerator.Problems(slots, 23), $"seed {seed}");
    }

    [Test]
    public void RollingTheSeedChangesTheBoard()
    {
        var shapes = new HashSet<string>();

        for (var seed = 1; seed <= 40; seed++)
            shapes.Add(string.Join("|", SolitaireLayoutGenerator.GeneratePattern(Rolling(seed))));

        Assert.Greater(shapes.Count, 1, "every seed drew the same board");
    }

    [Test]
    public void TheSameSeedAlwaysDrawsTheSameBoard()
    {
        Assert.AreEqual(SolitaireLayoutGenerator.GeneratePattern(Rolling(7)),
                        SolitaireLayoutGenerator.GeneratePattern(Rolling(7)));
    }

    [Test]
    public void ABoardTooBigForADeckIsReportedNotThrown()
    {
        var slots = SolitaireLayoutGenerator.FromPattern(
            SolitaireLayoutGenerator.GeneratePattern(new SolitaireLayoutGenerator.Settings
            {
                Seed = 1,
                Peaks = new Vector2Int(4, 4),
                PeakRows = new Vector2Int(5, 5),
                TargetCardCount = 45,
            }));

        Assert.IsNotEmpty(SolitaireLayoutGenerator.Problems(slots, 23),
                          "45 cards plus 23 in the draw pile does not fit a deck");
    }

    [Test]
    public void AFloatingCardIsReported()
    {
        // The row below is too narrow to hold up the right-hand card above it.
        var slots = SolitaireLayoutGenerator.FromPattern(new[] { "X...X", "X...." });

        Assert.IsNotEmpty(SolitaireLayoutGenerator.Problems(slots, 23));
    }

    [Test]
    public void AGeneratedBoardCanActuallyBeDealtAndPlayed()
    {
        var layout = Layout(SolitaireLayoutGenerator.GeneratePattern(Rolling(3)));
        var result = SolitaireLayoutProbe.Measure(layout, deals: 20);

        Assert.AreEqual(20, result.Deals, "the board could not be dealt");
        Assert.Greater(result.AverageCardsTaken, 0f, "no card came off the board in 20 deals");
    }

    [Test]
    public void AFlatBoardIsEasierThanTheClassicOne()
    {
        var flat = Layout(new[] { "X.X.X.X.X.X.X.X.X.X.X.X.X.X.X.X.X.X.X.X.X.X.X.X.X.X.X.X" });
        var classic = Layout(SolitaireLayoutGenerator.ClassicTriPeaks);

        Assert.Greater(SolitaireLayoutProbe.Measure(flat, 40).ClearRate,
                       SolitaireLayoutProbe.Measure(classic, 40).ClearRate);
    }
}
