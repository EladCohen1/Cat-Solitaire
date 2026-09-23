using NUnit.Framework;
using UnityEngine;

public class WagerLadderTests
{
    CurrencyDef _coins, _stars;
    WagerLadderDef _ladder;
    LevelDef _level;

    [SetUp]
    public void SetUp()
    {
        _coins = ScriptableObject.CreateInstance<CurrencyDef>();
        _coins.Id = "coins";
        _stars = ScriptableObject.CreateInstance<CurrencyDef>();
        _stars.Id = "stars";

        _ladder = ScriptableObject.CreateInstance<WagerLadderDef>();
        _ladder.Tiers = new[]
        {
            Tier("x1", 900, stars: 1, coins: 600),
            Tier("x2", 1020, stars: 2, coins: 800),
            Tier("x4", 1300, stars: 4, coins: 1200, unlocksAt: 5),
        };

        _level = ScriptableObject.CreateInstance<LevelDef>();
        _level.Id = "level-1";
        _level.WinRewards = new[] { new CurrencyAmount { Currency = _coins, Amount = 220 } };
        _level.Wagers = _ladder;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var o in new Object[] { _coins, _stars, _ladder, _level })
            Object.DestroyImmediate(o);
    }

    [Test]
    public void EachRungCarriesItsOwnCostAndPayout()
    {
        var top = _ladder.At(2);

        Assert.AreEqual(1300, top.Cost.Costs[0].Amount);
        Assert.AreEqual(4, top.WinRewards[0].Amount, "Stars go 1, 2, 4 — not a flat multiple of the rung.");
        Assert.AreEqual(1200, top.WinRewards[1].Amount, "Coins go 600, 800, 1200 on the same rungs.");
    }

    [Test]
    public void ARungWithNoRewardsOfItsOwn_FallsBackToTheLevel()
    {
        var plain = new WagerTier { Label = "x1", Cost = new Price() };

        var rewards = WagerLadderDef.RewardsOf(plain, _level);

        Assert.AreEqual(1, rewards.Length);
        Assert.AreEqual(220, rewards[0].Amount, "A one-rung ladder should not have to restate the level's numbers.");
    }

    [Test]
    public void NoWagerAtAll_PaysTheLevelsOwnRewards()
    {
        var rewards = WagerLadderDef.RewardsOf(null, _level);

        Assert.AreEqual(220, rewards[0].Amount);
    }

    [Test]
    public void ALockedRung_StaysShutUntilItsLevel()
    {
        Assert.IsFalse(_ladder.IsUnlocked(2, levelNumber: 4), "Level 4 has not reached the x4 rung.");
        Assert.IsTrue(_ladder.IsUnlocked(2, levelNumber: 5));
        Assert.IsTrue(_ladder.IsUnlocked(0, levelNumber: 1), "The bottom rung is open from the start.");
    }

    [Test]
    public void AskingForARungOffTheEnd_ClampsInsteadOfThrowing()
    {
        Assert.AreSame(_ladder.Tiers[0], _ladder.At(-3));
        Assert.AreSame(_ladder.Tiers[2], _ladder.At(99));
    }

    [Test]
    public void AnEmptyLadder_HasNoRungs()
    {
        var empty = ScriptableObject.CreateInstance<WagerLadderDef>();

        Assert.IsFalse(empty.HasTiers);
        Assert.AreEqual(0, empty.TierCount);
        Assert.IsNull(empty.At(0));

        Object.DestroyImmediate(empty);
    }

    WagerTier Tier(string label, int cost, int stars, int coins, int unlocksAt = 0) => new WagerTier
    {
        Label = label,
        Cost = new Price { Costs = new[] { new CurrencyAmount { Currency = _coins, Amount = cost } } },
        WinRewards = new[]
        {
            new CurrencyAmount { Currency = _stars, Amount = stars },
            new CurrencyAmount { Currency = _coins, Amount = coins },
        },
        UnlocksAtLevel = unlocksAt,
    };
}
