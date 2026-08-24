using NUnit.Framework;
using UnityEngine;

public class WalletTests
{
    CurrencyDef _coins;
    CurrencyDef _stars;
    PlayerProfile _profile;
    Wallet _wallet;

    [SetUp]
    public void SetUp()
    {
        _coins = NewCurrency("coins");
        _stars = NewCurrency("stars");
        _profile = new PlayerProfile();
        _wallet = new Wallet(_profile);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_coins);
        Object.DestroyImmediate(_stars);
    }

    [Test]
    public void NewWallet_StartsEmpty()
    {
        Assert.AreEqual(0, _wallet.Get(_coins));
    }

    [Test]
    public void Grant_IncreasesBalance()
    {
        _wallet.Grant(Amounts((_coins, 50)));
        Assert.AreEqual(50, _wallet.Get(_coins));
    }

    [Test]
    public void CanAfford_IsTrueAtExactBalance()
    {
        _wallet.Grant(Amounts((_coins, 50)));
        Assert.IsTrue(_wallet.CanAfford(PriceOf((_coins, 50))));
        Assert.IsFalse(_wallet.CanAfford(PriceOf((_coins, 51))));
    }

    [Test]
    public void TrySpend_WithoutEnoughFunds_FailsAndChangesNothing()
    {
        _wallet.Grant(Amounts((_coins, 10)));

        Assert.IsFalse(_wallet.TrySpend(PriceOf((_coins, 25))));
        Assert.AreEqual(10, _wallet.Get(_coins), "A failed purchase must not touch the balance.");
    }

    [Test]
    public void TrySpend_WithEnoughFunds_DeductsExactCost()
    {
        _wallet.Grant(Amounts((_coins, 100)));

        Assert.IsTrue(_wallet.TrySpend(PriceOf((_coins, 30))));
        Assert.AreEqual(70, _wallet.Get(_coins));
    }

    [Test]
    public void TrySpend_MultiCurrency_FailsIfAnySingleCurrencyIsShort()
    {
        _wallet.Grant(Amounts((_coins, 100), (_stars, 1)));

        Assert.IsFalse(_wallet.TrySpend(PriceOf((_coins, 50), (_stars, 5))));
        Assert.AreEqual(100, _wallet.Get(_coins), "Partial spending must never happen.");
        Assert.AreEqual(1, _wallet.Get(_stars));
    }

    [Test]
    public void Changed_FiresForTheCurrencyThatMoved()
    {
        CurrencyDef fired = null;
        _wallet.Changed += c => fired = c;

        _wallet.Grant(Amounts((_stars, 3)));

        Assert.AreSame(_stars, fired);
    }

    [Test]
    public void Balance_NeverGoesNegative()
    {
        _wallet.Grant(Amounts((_coins, -999)));
        Assert.AreEqual(0, _wallet.Get(_coins));
    }

    [Test]
    public void Profile_RoundTripsThroughJson()
    {
        _wallet.Grant(Amounts((_coins, 42), (_stars, 7)));

        var restored = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(_profile));
        var restoredWallet = new Wallet(restored);

        Assert.AreEqual(42, restoredWallet.Get(_coins));
        Assert.AreEqual(7, restoredWallet.Get(_stars));
    }

    [Test]
    public void UnconfiguredPrice_CountsAsFree()
    {
        Assert.IsTrue(_wallet.CanAfford(new Price()));
        Assert.IsTrue(_wallet.TrySpend(new Price()));
        Assert.IsTrue(_wallet.CanAfford(null));
    }

    [Test]
    public void UnconfiguredRewards_GrantNothingAndDoNotThrow()
    {
        Assert.DoesNotThrow(() => _wallet.Grant(null));
        Assert.AreEqual(0, _wallet.Get(_coins));
    }

    // ---- helpers ----

    static CurrencyDef NewCurrency(string id)
    {
        var c = ScriptableObject.CreateInstance<CurrencyDef>();
        c.Id = id;
        c.DisplayName = id;
        return c;
    }

    static CurrencyAmount[] Amounts(params (CurrencyDef currency, int amount)[] entries)
    {
        var result = new CurrencyAmount[entries.Length];
        for (var i = 0; i < entries.Length; i++)
            result[i] = new CurrencyAmount { Currency = entries[i].currency, Amount = entries[i].amount };
        return result;
    }

    static Price PriceOf(params (CurrencyDef currency, int amount)[] entries)
        => new Price { Costs = Amounts(entries) };
}
