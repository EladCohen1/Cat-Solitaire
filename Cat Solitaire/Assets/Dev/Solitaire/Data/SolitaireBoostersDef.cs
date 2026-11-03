using UnityEngine;

/// <summary>
/// What the things you can buy during a round cost, and what a good run pays.
/// One asset so a designer can balance all three against each other in one place,
/// rather than hunting through a scene for numbers.
///
/// Leave a price empty and that booster is free; leave the run bonus at 0 and it
/// is switched off. Neither is an error — a level that does not use them says so
/// by leaving them alone.
/// </summary>
[CreateAssetMenu(menuName = "Cat/Solitaire Boosters")]
public class SolitaireBoostersDef : ScriptableObject
{
    [Tooltip("What taking one move back costs.")]
    public Price UndoPrice;

    [Tooltip("What arming one wildcard costs.")]
    public Price WildcardPrice;

    [Header("Run bonus")]
    [Tooltip("Cards taken in an unbroken run before the bonus pays. 0 switches it off.")]
    public int StreakLength = 4;

    [Tooltip("What a completed run pays, every time one is completed.")]
    public CurrencyAmount[] StreakReward;
}
