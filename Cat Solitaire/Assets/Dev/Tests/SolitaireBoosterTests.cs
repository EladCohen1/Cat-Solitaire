using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Taking a move back, playing a card the rules would not normally allow, and the
/// bonus for an unbroken run. All three change what a round is worth, so each is
/// pinned here rather than checked by playing the game and looking at it.
/// </summary>
public class SolitaireBoosterTests
{
    readonly List<Object> _created = new();

    [TearDown]
    public void DestroyLayouts()
    {
        foreach (var asset in _created) Object.DestroyImmediate(asset);
        _created.Clear();
    }

    SolitaireBoardLayout Board(string[] pattern, int stockCount)
    {
        var layout = ScriptableObject.CreateInstance<SolitaireBoardLayout>();
        _created.Add(layout);
        layout.PatternText = string.Join("\n", pattern);
        layout.StockCount = stockCount;
        return layout;
    }

    /// <summary>Two free cards with a third resting on both, so blocking is real.</summary>
    SolitaireBoardLayout Tiny(int stockCount) => Board(new[] { ".X.", "X.X" }, stockCount);

    /// <summary>A row where nothing covers anything, for testing runs.</summary>
    SolitaireBoardLayout Row(int stockCount) => Board(new[] { "X.X.X.X" }, stockCount);

    static Card Rank(int rank) => new(rank, CardSuit.Hearts);

    static List<Card> Deal(params int[] ranks)
    {
        var cards = new List<Card>(ranks.Length);
        foreach (var rank in ranks) cards.Add(Rank(rank));
        return cards;
    }

    [Test]
    public void NothingToUndoBeforeAMoveIsMade()
    {
        var game = new SolitaireGame(Tiny(2), Deal(5, 3, 9, 4, 10, 11));

        Assert.IsFalse(game.CanUndo);
        Assert.IsFalse(game.TryUndo());
    }

    [Test]
    public void UndoingAPlayPutsTheCardAndTheScoreBack()
    {
        var game = new SolitaireGame(Tiny(2), Deal(5, 3, 9, 4, 10, 11));

        Assert.IsTrue(game.TryPlay(1), "the 3 should play onto the 4");
        Assert.IsTrue(game.TryUndo());

        Assert.IsFalse(game.IsCleared(1), "the card is back on the board");
        Assert.AreEqual(0, game.CardsCleared);
        Assert.AreEqual(4, game.Active.Rank, "the waste card is back");
        Assert.AreEqual(0, game.Score);
        Assert.AreEqual(0, game.Combo);
        Assert.IsFalse(game.CanUndo, "one move taken back leaves nothing to take back");
    }

    [Test]
    public void UndoingADrawPutsTheCardBackOnThePile()
    {
        var game = new SolitaireGame(Tiny(2), Deal(5, 8, 9, 4, 10, 11));
        var active = game.Active.Rank;
        var stock = game.StockRemaining;

        Assert.IsTrue(game.TryDraw());
        Assert.IsTrue(game.TryUndo());

        Assert.AreEqual(active, game.Active.Rank);
        Assert.AreEqual(stock, game.StockRemaining);
    }

    [Test]
    public void ADecidedRoundCannotBeUndone()
    {
        var game = new SolitaireGame(Board(new[] { "X.X" }, 0), Deal(3, 4, 2));

        game.TryPlay(0);
        game.TryPlay(1);

        Assert.AreEqual(SolitaireStatus.Won, game.Status);
        Assert.IsFalse(game.CanUndo);
        Assert.IsFalse(game.TryUndo());
    }

    [Test]
    public void AWildcardPlaysACardThatDoesNotMatch()
    {
        var game = new SolitaireGame(Tiny(2), Deal(5, 9, 9, 4, 10, 11));

        Assert.IsFalse(game.IsPlayable(1), "a 9 does not go on a 4");

        Assert.IsTrue(game.TryArmWildcard());
        Assert.IsFalse(game.TryArmWildcard(), "arming it twice is not a second wildcard");
        Assert.IsTrue(game.IsPlayable(1));

        game.TryPlay(1);
        Assert.IsFalse(game.WildcardArmed, "it is spent on the play it allowed");
    }

    [Test]
    public void AWildcardSurvivesAPlayThatWasLegalAnyway()
    {
        var game = new SolitaireGame(Tiny(2), Deal(5, 3, 9, 4, 10, 11));

        game.TryArmWildcard();
        game.TryPlay(1);   // the 3 matched the 4 on its own

        Assert.IsTrue(game.WildcardArmed);
    }

    [Test]
    public void UndoingAWildcardPlayGivesTheWildcardBack()
    {
        var game = new SolitaireGame(Tiny(2), Deal(5, 9, 9, 4, 10, 11));

        game.TryArmWildcard();
        game.TryPlay(1);
        game.TryUndo();

        Assert.IsTrue(game.WildcardArmed, "it was paid for, so it comes back with the move");
    }

    [Test]
    public void AnUnbrokenRunPaysOutAndTheMeterWraps()
    {
        var game = new SolitaireGame(Row(2), Deal(3, 4, 5, 6, 2, 12, 13)) { StreakLength = 4 };
        var paid = 0;
        game.StreakCompleted += () => paid++;

        Assert.AreEqual(0, game.StreakMeter);

        game.TryPlay(0);
        Assert.AreEqual(1, game.StreakMeter);

        game.TryPlay(1);
        game.TryPlay(2);
        Assert.AreEqual(0, paid, "three is not a run yet");

        game.TryPlay(3);
        Assert.AreEqual(1, paid);
        Assert.AreEqual(0, game.StreakMeter, "the meter wraps once it pays");
        Assert.AreEqual(4, game.Combo, "the scoring streak keeps running past a payout");
    }

    [Test]
    public void TakingAMoveBackAndPlayingItAgainDoesNotPayTwice()
    {
        var game = new SolitaireGame(Row(2), Deal(3, 4, 5, 6, 2, 12, 13)) { StreakLength = 4 };
        var paid = 0;
        game.StreakCompleted += () => paid++;

        game.TryPlay(0);
        game.TryPlay(1);
        game.TryPlay(2);
        game.TryPlay(3);
        Assert.AreEqual(1, paid);

        game.TryUndo();
        game.TryPlay(3);

        Assert.AreEqual(1, paid, "undo would otherwise be a way of printing money");
    }

    [Test]
    public void TheStreakBonusIsOffUntilALevelAsksForIt()
    {
        var game = new SolitaireGame(Row(2), Deal(3, 4, 5, 6, 2, 12, 13));
        var paid = 0;
        game.StreakCompleted += () => paid++;

        game.TryPlay(0);
        game.TryPlay(1);
        game.TryPlay(2);
        game.TryPlay(3);

        Assert.AreEqual(0, paid);
        Assert.AreEqual(0, game.StreakMeter);
    }
}
