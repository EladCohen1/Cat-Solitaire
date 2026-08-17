using UnityEngine;
using System;

public class Wallet
{
    readonly PlayerProfile _profile;
    public event Action<CurrencyDef> Changed;

    public Wallet(PlayerProfile profile) => _profile = profile;

    public int Get(CurrencyDef c) => _profile.GetAmount(c.Id);

    // An unconfigured Price/reward list in an asset means "free"/"nothing",
    // not a crash — designers leave these empty all the time.
    public bool CanAfford(Price price)
    {
        if (price?.Costs == null) return true;

        foreach (var c in price.Costs)
            if (Get(c.Currency) < c.Amount) return false;
        return true;
    }

    public bool TrySpend(Price price)
    {
        if (!CanAfford(price)) return false;
        if (price?.Costs == null) return true;

        foreach (var c in price.Costs) Add(c.Currency, -c.Amount);
        return true;
    }

    public void Grant(CurrencyAmount[] rewards)
    {
        if (rewards == null) return;

        foreach (var r in rewards) Add(r.Currency, r.Amount);
    }

    void Add(CurrencyDef c, int delta)
    {
        _profile.SetAmount(c.Id, Mathf.Max(0, Get(c) + delta));
        Changed?.Invoke(c);
    }
}
