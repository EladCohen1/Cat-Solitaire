using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable] public class CurrencyEntry { public string Id; public int Amount; }

[Serializable]
public class PlayerProfile
{
    public int SaveVersion = 1;
    public List<CurrencyEntry> Wallet = new();
    public List<string> UnlockedObjectIds = new();
    public int LevelsCompleted;

    public int GetAmount(string id) => Wallet.Find(e => e.Id == id)?.Amount ?? 0;

    public void SetAmount(string id, int amount)
    {
        var e = Wallet.Find(x => x.Id == id);
        if (e == null) Wallet.Add(new CurrencyEntry { Id = id, Amount = amount });
        else e.Amount = amount;
    }
}
