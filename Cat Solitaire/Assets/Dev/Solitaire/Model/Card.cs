using System;

public enum CardSuit { Clubs, Diamonds, Hearts, Spades }

/// <summary>
/// One playing card. A value type on purpose: the game passes cards around by
/// copying them, so nothing can end up holding a live reference to a card that
/// has already been played off the board.
/// </summary>
[Serializable]
public struct Card : IEquatable<Card>
{
    public const int LowestRank = 1;    // Ace
    public const int HighestRank = 13;  // King

    public int Rank;
    public CardSuit Suit;

    public Card(int rank, CardSuit suit)
    {
        Rank = rank;
        Suit = suit;
    }

    public bool IsRed => Suit == CardSuit.Hearts || Suit == CardSuit.Diamonds;

    /// <summary>
    /// The tri-peaks matching rule: one rank apart, and the sequence wraps round,
    /// so a King takes an Ace and an Ace takes a King. Suit never matters.
    /// </summary>
    public bool Matches(Card other)
    {
        var diff = Math.Abs(Rank - other.Rank);
        return diff == 1 || diff == HighestRank - LowestRank;
    }

    public string RankLabel
    {
        get
        {
            switch (Rank)
            {
                case 1: return "A";
                case 11: return "J";
                case 12: return "Q";
                case 13: return "K";
                default: return Rank.ToString();
            }
        }
    }

    public string SuitLabel
    {
        get
        {
            switch (Suit)
            {
                case CardSuit.Clubs: return "♣";
                case CardSuit.Diamonds: return "♦";
                case CardSuit.Hearts: return "♥";
                default: return "♠";
            }
        }
    }

    public override string ToString() => RankLabel + SuitLabel;

    public bool Equals(Card other) => Rank == other.Rank && Suit == other.Suit;
    public override bool Equals(object obj) => obj is Card other && Equals(other);
    public override int GetHashCode() => Rank * 4 + (int)Suit;
}
