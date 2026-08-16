using UnityEngine;
using System;

public class Wallet
{
    readonly PlayerProfile _profile;
    public event Action<CurrencyDef> Changed;

    public Wallet(PlayerProfile profile) => _profile = profile;

    public int Get(CurrencyDef c) => _profile.GetAmount(c.Id);

    public bool CanAfford(Price price)
    {
        foreach (var c in price.Costs)
            if (Get(c.Currency) < c.Amount) return false;
        return true;
    }

    public bool TrySpend(Price price)
    {
        if (!CanAfford(price)) return false;
        foreach (var c in price.Costs) Add(c.Currency, -c.Amount);
        return true;
    }

    public void Grant(CurrencyAmount[] rewards)
    {
        foreach (var r in rewards) Add(r.Currency, r.Amount);
    }

    void Add(CurrencyDef c, int delta)
    {
        _profile.SetAmount(c.Id, Mathf.Max(0, Get(c) + delta));
        Changed?.Invoke(c);
    }
}
