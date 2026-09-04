using System;
using System.Collections.Generic;

/// <summary>
/// Builds and shuffles the 52-card deck. The shuffle is seeded so a level deals
/// the same board every time it is replayed — the tests rely on that, and so does
/// any "everyone gets today's board" feature later on.
/// </summary>
public static class CardDeck
{
    public const int Size = 52;

    public static List<Card> BuildStandard()
    {
        var cards = new List<Card>(Size);

        foreach (CardSuit suit in Enum.GetValues(typeof(CardSuit)))
            for (var rank = Card.LowestRank; rank <= Card.HighestRank; rank++)
                cards.Add(new Card(rank, suit));

        return cards;
    }

    public static List<Card> BuildShuffled(int seed)
    {
        var cards = BuildStandard();
        Shuffle(cards, new System.Random(seed));
        return cards;
    }

    public static void Shuffle(IList<Card> cards, System.Random random)
    {
        for (var i = cards.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            var swap = cards[i];
            cards[i] = cards[j];
            cards[j] = swap;
        }
    }
}
