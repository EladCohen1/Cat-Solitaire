using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChapterProgressView : MonoBehaviour
{
    [SerializeField] TMP_Text _chapterNameLabel;
    [SerializeField] TMP_Text _progressLabel;

    [Header("Optional")]
    [SerializeField] Slider _progressFill;
    [SerializeField] GameObject _completeBadge;
    [Tooltip("The chest at the end of the bar. Hook its OnClick to ClaimReward.")]
    [SerializeField] Button _claimButton;

    ProgressionService _progression;

    void Start()
    {
        _progression = GameBootstrap.Instance.Progression;
        _progression.ObjectUnlocked += OnObjectUnlocked;
        _progression.ChapterRewardClaimed += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        if (_progression == null) return;

        _progression.ObjectUnlocked -= OnObjectUnlocked;
        _progression.ChapterRewardClaimed -= Refresh;
    }

    /// <summary>Hook the chest button's OnClick here. Pays out once, then goes quiet.</summary>
    public void ClaimReward()
    {
        if (_progression != null) _progression.TryClaimChapterReward();
    }

    void OnObjectUnlocked(HubObjectDef _) => Refresh();

    void Refresh()
    {
        var chapter = _progression.Chapter;
        if (chapter == null) return;

        var unlocked = _progression.UnlockedCount;
        var required = Mathf.Max(1, chapter.RequiredUnlocks);

        if (_chapterNameLabel != null) _chapterNameLabel.text = chapter.DisplayName;
        if (_progressLabel != null) _progressLabel.text = $"{unlocked}/{required}";
        if (_progressFill != null) _progressFill.value = Mathf.Clamp01((float)unlocked / required);
        if (_completeBadge != null) _completeBadge.SetActive(_progression.IsChapterComplete);

        // The chest stays on the bar once claimed, as the thing the goal was for.
        if (_claimButton != null) _claimButton.interactable = _progression.CanClaimChapterReward;
    }
}
