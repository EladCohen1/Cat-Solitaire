using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class SolitaireGameTests
{
    readonly List<Object> _created = new();

    [TearDown]
    public void DestroyLayouts()
    {
        foreach (var asset in _created) Object.DestroyImmediate(asset);
        _created.Clear();
    }

    /// <summary>Two free cards side by side with a third resting on both of them.</summary>
    SolitaireBoardLayout TinyPeak(int stockCount)
    {
        var layout = ScriptableObject.CreateInstance<SolitaireBoardLayout>();
        _created.Add(layout);

        layout.StockCount = stockCount;
        layout.Slots = new[]
        {
            new SolitaireBoardLayout.Slot { Position = new Vector2(0.5f, 0.5f), Blockers = new[] { 1, 2 } },
            new SolitaireBoardLayout.Slot { Position = new Vector2(0f, 0f) },
            new SolitaireBoardLayout.Slot { Position = new Vector2(1f, 0f) },
        };
        return layout;
    }

    SolitaireBoardLayout ClassicLayout()
    {
        var layout = ScriptableObject.CreateInstance<SolitaireBoardLayout>();
        _created.Add(layout);
        return layout;   // no authored slots, so it falls back to the classic three peaks
    }

    static Card C(int rank, CardSuit suit) => new Card(rank, suit);

    [Test]
    public void StandardDeck_IsFiftyTwoDistinctCards()
    {
        var deck = CardDeck.BuildStandard();

        Assert.AreEqual(CardDeck.Size, deck.Count);
        CollectionAssert.AllItemsAreUnique(deck);
    }

    [Test]
    public void Matching_IsOneRankApart_AndWrapsAroundAceAndKing()
    {
        Assert.IsTrue(C(5, CardSuit.Spades).Matches(C(6, CardSuit.Hearts)));
        Assert.IsTrue(C(5, CardSuit.Spades).Matches(C(4, CardSuit.Spades)));
        Assert.IsTrue(C(13, CardSuit.Clubs).Matches(C(1, CardSuit.Hearts)), "A King takes an Ace.");
        Assert.IsTrue(C(1, CardSuit.Clubs).Matches(C(13, CardSuit.Hearts)), "An Ace takes a King.");

        Assert.IsFalse(C(5, CardSuit.Spades).Matches(C(7, CardSuit.Spades)));
        Assert.IsFalse(C(5, CardSuit.Spades).Matches(C(5, CardSuit.Hearts)), "A card never takes its own rank.");
    }

    [Test]
    public void ClassicLayout_IsThreePeaksOverTenFreeCards()
    {
        var slots = SolitaireBoardLayout.BuildClassicTriPeaks();

        Assert.AreEqual(28, slots.Length);

        var free = 0;
        foreach (var slot in slots)
            if (slot.Blockers.Length == 0) free++;

        Assert.AreEqual(10, free, "Only the bottom row starts out uncovered.");

        for (var i = 0; i < 3; i++)
            Assert.AreEqual(2, slots[i].Blockers.Length, "Every peak sits on exactly two cards.");
    }

    [Test]
    public void Deal_LeavesTheBottomRowPlayableAndThePeakBlocked()
    {
        var game = new SolitaireGame(TinyPeak(stockCount: 1), new[]
        {
            C(5, CardSuit.Spades), C(2, CardSuit.Hearts), C(3, CardSuit.Diamonds),
            C(4, CardSuit.Clubs),
            C(13, CardSuit.Spades),
        });

        Assert.IsTrue(game.IsBlocked(0));
        Assert.IsFalse(game.IsBlocked(1));
        Assert.AreEqual(C(4, CardSuit.Clubs), game.Active);
        Assert.AreEqual(1, game.StockRemaining);
        Assert.AreEqual(SolitaireStatus.Playing, game.Status);
    }

    [Test]
    public void TryPlay_RejectsBlockedAndNonMatchingCards()
    {
        var game = new SolitaireGame(TinyPeak(stockCount: 1), new[]
        {
            C(5, CardSuit.Spades), C(2, CardSuit.Hearts), C(3, CardSuit.Diamonds),
            C(4, CardSuit.Clubs),
            C(13, CardSuit.Spades),
        });

        Assert.IsFalse(game.TryPlay(0), "The peak is still covered.");
        Assert.IsFalse(game.TryPlay(1), "A two does not take a four.");
        Assert.AreEqual(0, game.CardsCleared);
        Assert.AreEqual(0, game.Score);
    }

    [Test]
    public void ClearingBothCoveringCards_FreesThePeak()
    {
        var game = new SolitaireGame(TinyPeak(stockCount: 1), new[]
        {
            C(5, CardSuit.Spades), C(2, CardSuit.Hearts), C(3, CardSuit.Diamonds),
            C(4, CardSuit.Clubs),
            C(13, CardSuit.Spades),
        });

        Assert.IsTrue(game.TryPlay(2), "A three takes a four.");
        Assert.AreEqual(C(3, CardSuit.Diamonds), game.Active);
        Assert.IsTrue(game.IsCleared(2));
        Assert.IsTrue(game.IsBlocked(0), "One of the two covering cards is still there.");

        Assert.IsTrue(game.TryPlay(1), "A two takes a three.");
        Assert.IsFalse(game.IsBlocked(0));
    }

    [Test]
    public void Score_GrowsWithEachCardInTheStreak()
    {
        var game = new SolitaireGame(TinyPeak(stockCount: 1), new[]
        {
            C(5, CardSuit.Spades), C(2, CardSuit.Hearts), C(3, CardSuit.Diamonds),
            C(4, CardSuit.Clubs),
            C(13, CardSuit.Spades),
        });

        game.TryPlay(2);
        Assert.AreEqual(1, game.Combo);
        Assert.AreEqual(SolitaireGame.PointsPerCard, game.Score);

        game.TryPlay(1);
        Assert.AreEqual(2, game.Combo);
        Assert.AreEqual(SolitaireGame.PointsPerCard * 3, game.Score, "100 for the first card, 200 for the second.");
    }

    [Test]
    public void TryDraw_TakesTheTopOfTheStockAndBreaksTheStreak()
    {
        var game = new SolitaireGame(TinyPeak(stockCount: 1), new[]
        {
            C(5, CardSuit.Spades), C(2, CardSuit.Hearts), C(3, CardSuit.Diamonds),
            C(4, CardSuit.Clubs),
            C(13, CardSuit.Spades),
        });

        game.TryPlay(2);
        Assert.IsTrue(game.TryDraw());

        Assert.AreEqual(C(13, CardSuit.Spades), game.Active);
        Assert.AreEqual(0, game.Combo);
        Assert.AreEqual(0, game.StockRemaining);
        Assert.IsFalse(game.TryDraw(), "The pile is empty.");
    }

    [Test]
    public void ClearingTheBoard_WinsAndPaysTheBonus()
    {
        var game = new SolitaireGame(TinyPeak(stockCount: 1), new[]
        {
            C(1, CardSuit.Spades), C(2, CardSuit.Hearts), C(3, CardSuit.Diamonds),
            C(4, CardSuit.Clubs),
            C(13, CardSuit.Spades),
        });

        Assert.IsTrue(game.TryPlay(2));
        Assert.IsTrue(game.TryPlay(1));
        Assert.IsTrue(game.TryPlay(0), "An Ace takes a two.");

        Assert.AreEqual(SolitaireStatus.Won, game.Status);
        Assert.AreEqual(0, game.CardsRemaining);

        var streak = SolitaireGame.PointsPerCard * (1 + 2 + 3);
        Assert.AreEqual(streak + SolitaireGame.BoardClearBonus + SolitaireGame.PointsPerUnusedStockCard, game.Score,
            "The unplayed stock card is worth a bonus of its own.");
    }

    [Test]
    public void RunningOutOfMovesWithAnEmptyStock_Loses()
    {
        var game = new SolitaireGame(TinyPeak(stockCount: 0), new[]
        {
            C(9, CardSuit.Spades), C(2, CardSuit.Hearts), C(3, CardSuit.Diamonds),
            C(4, CardSuit.Clubs),
        });

        Assert.IsTrue(game.TryPlay(2));
        Assert.IsTrue(game.TryPlay(1));

        // The peak is free now, but a nine does not take a two and there is nothing to draw.
        Assert.IsFalse(game.IsBlocked(0));
        Assert.AreEqual(SolitaireStatus.Lost, game.Status);
        Assert.IsFalse(game.TryPlay(0));
    }

    [Test]
    public void TheSameSeed_DealsTheSameBoard()
    {
        var first = new SolitaireGame(ClassicLayout(), seed: 12345);
        var second = new SolitaireGame(ClassicLayout(), seed: 12345);
        var other = new SolitaireGame(ClassicLayout(), seed: 999);

        Assert.AreEqual(first.Active, second.Active);
        for (var i = 0; i < first.SlotCount; i++)
            Assert.AreEqual(first.CardAt(i), second.CardAt(i));

        var identical = first.Active.Equals(other.Active);
        for (var i = 0; i < first.SlotCount; i++)
            identical &= first.CardAt(i).Equals(other.CardAt(i));

        Assert.IsFalse(identical, "A different seed has to deal a different board.");
    }

    [Test]
    public void ClassicBoard_UsesTheWholeDeck()
    {
        var game = new SolitaireGame(ClassicLayout(), seed: 7);

        Assert.AreEqual(28, game.SlotCount);
        Assert.AreEqual(23, game.StockRemaining);
        Assert.AreEqual(CardDeck.Size, game.SlotCount + 1 + game.StockRemaining);
    }

    [Test]
    public void ADealTooSmallForTheLayout_IsRejected()
    {
        Assert.Throws<System.ArgumentException>(() =>
            new SolitaireGame(TinyPeak(stockCount: 5), new[] { C(1, CardSuit.Spades) }));
    }
}
