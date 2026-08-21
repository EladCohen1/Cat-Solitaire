using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Cat/Currency")]
public class CurrencyDef : ScriptableObject
{
    public string Id;            // "coins" / "stars"
    public string DisplayName;
    public Sprite Icon;
}

[Serializable] public struct CurrencyAmount { public CurrencyDef Currency; public int Amount; }
[Serializable] public class  Price { public CurrencyAmount[] Costs; }