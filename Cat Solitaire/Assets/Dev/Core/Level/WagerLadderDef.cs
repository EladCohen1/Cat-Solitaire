using System;
using UnityEngine;

/// <summary>
/// One rung of the bet bar: what it costs to play at this rung, and what a win pays
/// back. The payout is written out in full rather than derived from a multiplier,
/// because a rung rarely scales everything evenly — stars might double while coins
/// go up by a third.
/// </summary>
[Serializable]
public class WagerTier
{
    [Tooltip("Shown on the bar — \"x1\", \"x2\".")]
    public string Label;

    [Tooltip("What playing at this rung costs. Empty means free.")]
    public Price Cost;

    [Tooltip("What a win pays at this rung. Empty falls back to the level's own WinRewards.")]
    public CurrencyAmount[] WinRewards;

    [Tooltip("The level number this rung unlocks at — the padlock on the bar. 0 is always open.")]
    [Min(0)] public int UnlocksAtLevel;
}

/// <summary>
/// The bet bar a level offers: pay more up front, win more back. A level points at
/// one of these, so a ladder can be shared across a run of levels or authored per
/// level when the numbers need to differ.
/// </summary>
[CreateAssetMenu(menuName = "Cat/Wager Ladder")]
public class WagerLadderDef : ScriptableObject
{
    public WagerTier[] Tiers;

    public bool HasTiers => Tiers != null && Tiers.Length > 0;

    public int TierCount => HasTiers ? Tiers.Length : 0;

    public WagerTier At(int index) =>
        HasTiers ? Tiers[Mathf.Clamp(index, 0, Tiers.Length - 1)] : null;

    /// <summary>A rung the player has not reached yet shows a padlock and cannot be picked.</summary>
    public bool IsUnlocked(int index, int levelNumber)
    {
        var tier = At(index);
        return tier != null && levelNumber >= tier.UnlocksAtLevel;
    }

    /// <summary>
    /// What this rung pays on a win. Rungs that name no rewards of their own fall
    /// back to the level, so a one-rung ladder needs no duplicated numbers.
    /// </summary>
    public static CurrencyAmount[] RewardsOf(WagerTier tier, LevelDef level)
    {
        if (tier != null && tier.WinRewards != null && tier.WinRewards.Length > 0)
            return tier.WinRewards;

        return level != null ? level.WinRewards : Array.Empty<CurrencyAmount>();
    }
}
