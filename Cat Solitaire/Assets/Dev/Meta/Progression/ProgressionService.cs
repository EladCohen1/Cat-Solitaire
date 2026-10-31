using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ProgressionService
{
    readonly PlayerProfile _profile;
    readonly Wallet _wallet;
    readonly HubChapterDef _chapter;

    public event Action<HubObjectDef> ObjectUnlocked;
    public event Action ChapterCompleted;
    public event Action ChapterRewardClaimed;

    public ProgressionService(PlayerProfile p, Wallet w, HubChapterDef c)
    { _profile = p; _wallet = w; _chapter = c; WarnAboutSharedIds(); }

    /// <summary>
    /// Unlocks are saved by id, so two objects sharing one are bought together and
    /// counted twice — progress jumps straight to full on the first purchase.
    /// Duplicating an asset and forgetting to rename the id is the usual way in.
    /// </summary>
    void WarnAboutSharedIds()
    {
        if (_chapter == null || _chapter.Objects == null) return;

        var seen = new HashSet<string>();

        foreach (var o in _chapter.Objects)
        {
            if (o == null) continue;

            if (!seen.Add(o.Id))
                Debug.LogError(
                    $"{_chapter.name}: '{o.name}' reuses the id '{o.Id}'. Unlocking any one of them unlocks them all.",
                    _chapter);
        }
    }

    public HubChapterDef Chapter => _chapter;

    public bool IsUnlocked(HubObjectDef o) => _profile.UnlockedObjectIds.Contains(o.Id);
    public bool CanPurchase(HubObjectDef o) => !IsUnlocked(o) && _wallet.CanAfford(o.Cost);

    public int  UnlockedCount => _chapter.Objects.Count(IsUnlocked);
    public bool IsChapterComplete => UnlockedCount >= _chapter.RequiredUnlocks;

    // Keyed by chapter id, not by asset name, so renaming the asset cannot hand the
    // chest out a second time.
    public bool IsChapterRewardClaimed => _profile.ClaimedChapterRewardIds.Contains(_chapter.Id);
    public bool CanClaimChapterReward => IsChapterComplete && !IsChapterRewardClaimed;

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

    /// <summary>
    /// Pays out the chapter's completion reward, once and only once. Nothing claims
    /// it automatically — the player taps the chest, so the moment can be celebrated.
    /// </summary>
    public bool TryClaimChapterReward()
    {
        if (!CanClaimChapterReward) return false;

        // Marked before the money moves: a save triggered by the grant must already
        // see the chest as claimed.
        _profile.ClaimedChapterRewardIds.Add(_chapter.Id);
        _wallet.Grant(_chapter.CompletionRewards);

        ChapterRewardClaimed?.Invoke();
        return true;
    }
}
