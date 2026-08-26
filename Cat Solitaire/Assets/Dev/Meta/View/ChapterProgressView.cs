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

    ProgressionService _progression;

    void Start()
    {
        _progression = GameBootstrap.Instance.Progression;
        _progression.ObjectUnlocked += OnObjectUnlocked;
        Refresh();
    }

    void OnDestroy()
    {
        if (_progression != null) _progression.ObjectUnlocked -= OnObjectUnlocked;
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
    }
}
