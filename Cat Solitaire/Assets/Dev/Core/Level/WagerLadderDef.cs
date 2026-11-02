using System;
using UnityEngine;

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

[CreateAssetMenu(menuName = "Cat/Wager Ladder")]
public class WagerLadderDef : ScriptableObject
{
    public WagerTier[] Tiers;

    public bool HasTiers => Tiers != null && Tiers.Length > 0;

    public int TierCount => HasTiers ? Tiers.Length : 0;

    public WagerTier At(int index) =>
        HasTiers ? Tiers[Mathf.Clamp(index, 0, Tiers.Length - 1)] : null;
    
    public bool IsUnlocked(int index, int levelNumber)
    {
        var tier = At(index);
        return tier != null && levelNumber >= tier.UnlocksAtLevel;
    }
    
    public static CurrencyAmount[] RewardsOf(WagerTier tier, LevelDef level)
    {
        if (tier != null && tier.WinRewards != null && tier.WinRewards.Length > 0)
            return tier.WinRewards;

        return level != null ? level.WinRewards : Array.Empty<CurrencyAmount>();
    }
}
