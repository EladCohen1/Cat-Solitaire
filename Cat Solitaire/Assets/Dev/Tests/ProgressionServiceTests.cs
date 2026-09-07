using NUnit.Framework;
using UnityEngine;

public class ProgressionServiceTests
{
    CurrencyDef _stars;
    HubObjectDef _a, _b, _c;
    HubChapterDef _chapter;
    PlayerProfile _profile;
    Wallet _wallet;
    ProgressionService _progression;

    [SetUp]
    public void SetUp()
    {
        _stars = ScriptableObject.CreateInstance<CurrencyDef>();
        _stars.Id = "stars";

        _a = NewObject("a", 10);
        _b = NewObject("b", 10);
        _c = NewObject("c", 10);

        _chapter = ScriptableObject.CreateInstance<HubChapterDef>();
        _chapter.Id = "chapter1";
        _chapter.Objects = new[] { _a, _b, _c };
        _chapter.RequiredUnlocks = 2;   // buy 2 of 3 to finish the chapter
        _chapter.CompletionRewards = new[] { new CurrencyAmount { Currency = _stars, Amount = 50 } };

        _profile = new PlayerProfile();
        _wallet = new Wallet(_profile);
        _progression = new ProgressionService(_profile, _wallet, _chapter);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var o in new Object[] { _stars, _a, _b, _c, _chapter })
            Object.DestroyImmediate(o);
    }

    [Test]
    public void CannotPurchase_WithoutEnoughCurrency()
    {
        Assert.IsFalse(_progression.CanPurchase(_a));
        Assert.IsFalse(_progression.TryPurchase(_a));
        Assert.IsFalse(_progression.IsUnlocked(_a));
    }

    [Test]
    public void Purchase_UnlocksAndDeducts()
    {
        GrantStars(10);

        Assert.IsTrue(_progression.TryPurchase(_a));
        Assert.IsTrue(_progression.IsUnlocked(_a));
        Assert.AreEqual(0, _wallet.Get(_stars));
        Assert.AreEqual(1, _progression.UnlockedCount);
    }

    [Test]
    public void PurchasingTheSameObjectTwice_DoesNotChargeAgain()
    {
        GrantStars(20);
        _progression.TryPurchase(_a);

        Assert.IsFalse(_progression.TryPurchase(_a));
        Assert.AreEqual(10, _wallet.Get(_stars), "The second purchase must be rejected before spending.");
        Assert.AreEqual(1, _progression.UnlockedCount);
    }

    [Test]
    public void ChapterCompleted_FiresExactlyOnce_EvenIfMoreObjectsAreBought()
    {
        GrantStars(30);
        var fireCount = 0;
        _progression.ChapterCompleted += () => fireCount++;

        _progression.TryPurchase(_a);
        Assert.AreEqual(0, fireCount, "One of two required objects — not complete yet.");

        _progression.TryPurchase(_b);
        Assert.AreEqual(1, fireCount, "Second required object completes the chapter.");

        _progression.TryPurchase(_c);
        Assert.AreEqual(1, fireCount, "Extra purchases must not re-fire completion.");
    }

    [Test]
    public void ChapterReward_CannotBeClaimedBeforeTheChapterIsComplete()
    {
        GrantStars(10);
        _progression.TryPurchase(_a);   // one of the two the chapter asks for

        Assert.IsFalse(_progression.CanClaimChapterReward);
        Assert.IsFalse(_progression.TryClaimChapterReward());
        Assert.AreEqual(0, _wallet.Get(_stars));
    }

    [Test]
    public void ClaimingTheChapterReward_PaysOutAndIsRemembered()
    {
        CompleteTheChapter();

        Assert.IsTrue(_progression.CanClaimChapterReward);
        Assert.IsTrue(_progression.TryClaimChapterReward());

        Assert.AreEqual(50, _wallet.Get(_stars));
        Assert.IsTrue(_progression.IsChapterRewardClaimed);
        CollectionAssert.Contains(_profile.ClaimedChapterRewardIds, "chapter1",
            "The claim has to reach the profile, or it is forgotten on the next launch.");
    }

    [Test]
    public void TheChapterReward_IsOnlyEverPaidOnce()
    {
        CompleteTheChapter();

        var fireCount = 0;
        _progression.ChapterRewardClaimed += () => fireCount++;
        _progression.TryClaimChapterReward();

        Assert.IsFalse(_progression.TryClaimChapterReward());
        Assert.AreEqual(50, _wallet.Get(_stars), "The second claim must be rejected before granting.");
        Assert.AreEqual(1, fireCount);
    }

    // ---- helpers ----

    void CompleteTheChapter()
    {
        GrantStars(20);
        _progression.TryPurchase(_a);
        _progression.TryPurchase(_b);
    }


    void GrantStars(int amount)
        => _wallet.Grant(new[] { new CurrencyAmount { Currency = _stars, Amount = amount } });

    HubObjectDef NewObject(string id, int starCost)
    {
        var o = ScriptableObject.CreateInstance<HubObjectDef>();
        o.Id = id;
        o.DisplayName = id;
        o.Cost = new Price
        {
            Costs = new[] { new CurrencyAmount { Currency = _stars, Amount = starCost } }
        };
        return o;
    }
}
