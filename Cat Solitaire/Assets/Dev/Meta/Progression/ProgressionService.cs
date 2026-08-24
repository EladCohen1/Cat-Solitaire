using System;
using System.Linq;
using UnityEngine;

public class ProgressionService
{
    readonly PlayerProfile _profile;
    readonly Wallet _wallet;
    readonly HubChapterDef _chapter;

    public event Action<HubObjectDef> ObjectUnlocked;
    public event Action ChapterCompleted;

    public ProgressionService(PlayerProfile p, Wallet w, HubChapterDef c)
    { _profile = p; _wallet = w; _chapter = c; }

    public HubChapterDef Chapter => _chapter;

    public bool IsUnlocked(HubObjectDef o) => _profile.UnlockedObjectIds.Contains(o.Id);
    public bool CanPurchase(HubObjectDef o) => !IsUnlocked(o) && _wallet.CanAfford(o.Cost);

    public int  UnlockedCount => _chapter.Objects.Count(IsUnlocked);
    public bool IsChapterComplete => UnlockedCount >= _chapter.RequiredUnlocks;

    public bool TryPurchase(HubObjectDef o)
    {
        if (!CanPurchase(o) || !_wallet.TrySpend(o.Cost)) return false;

        var wasComplete = IsChapterComplete;
        _profile.UnlockedObjectIds.Add(o.Id);
        ObjectUnlocked?.Invoke(o);

        // Only on the transition, otherwise every extra purchase re-fires it.
        if (!wasComplete && IsChapterComplete) ChapterCompleted?.Invoke();
        return true;
    }
}
