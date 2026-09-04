using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The scene progress popup: a row for every object in the chapter, bought and still
/// to buy, opened from the hub's progress button.
///
/// It owns the list. Rows are built once — a chapter's contents never change at
/// runtime — and refreshed whenever a purchase lands or a balance moves, so this is
/// the only object listening however long the list gets.
///
/// The headline and the progress bar at the top are not handled here: put a
/// <see cref="ChapterProgressView"/> on them and it fills in the chapter name, the
/// x/y and the fill, exactly as it already does for the bar in the hub.
///
/// Put this component on something that stays active — the Canvas or a holder — and
/// point <see cref="_window"/> at the panel it should show and hide, or the first tap
/// arrives before Start has ever run.
/// </summary>
public class ObjectProgressWindow : MonoBehaviour
{
    [Header("List")]
    [SerializeField] HubObjectBarView _barPrefab;
    [Tooltip("The Content object inside the Scroll View. Rows are spawned here.")]
    [SerializeField] RectTransform _listRoot;

    [Header("Window")]
    [Tooltip("The panel switched on and off. Left empty, this GameObject is used.")]
    [SerializeField] GameObject _window;
    [SerializeField] bool _closedOnStart = true;

    readonly List<HubObjectBarView> _rows = new();

    Wallet _wallet;
    ProgressionService _progression;

    public bool IsOpen => Window.activeSelf;

    GameObject Window => _window != null ? _window : gameObject;

    void Start()
    {
        if (_barPrefab == null || _listRoot == null)
        {
            Debug.LogError($"{name}: needs a bar prefab and a list root.", this);
            enabled = false;
            return;
        }

        // Start(), not Awake() — GameBootstrap builds the services in its own Awake,
        // and Awake order between objects is undefined.
        _wallet = GameBootstrap.Instance.Wallet;
        _progression = GameBootstrap.Instance.Progression;

        _progression.ObjectUnlocked += OnObjectUnlocked;
        _wallet.Changed += OnCurrencyChanged;

        BuildRows();
        RefreshRows();

        if (_closedOnStart) Close();
    }

    void OnDestroy()
    {
        if (_progression != null) _progression.ObjectUnlocked -= OnObjectUnlocked;
        if (_wallet != null) _wallet.Changed -= OnCurrencyChanged;
    }

    /// <summary>Hook these to the progress button and the window's close button.</summary>
    public void Open()
    {
        RefreshRows();   // catch anything that moved while the window was shut
        Window.SetActive(true);
    }

    public void Close() => Window.SetActive(false);

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    void OnObjectUnlocked(HubObjectDef _) => RefreshRows();

    void OnCurrencyChanged(CurrencyDef _) => RefreshRows();

    void BuildRows()
    {
        var chapter = _progression.Chapter;
        if (chapter == null || chapter.Objects == null)
        {
            Debug.LogWarning($"{name}: the chapter has no objects, so the list is empty.", this);
            return;
        }

        foreach (var definition in chapter.Objects)
        {
            if (definition == null) continue;   // an empty slot left in the chapter asset

            var row = Instantiate(_barPrefab, _listRoot);
            row.name = $"Bar - {definition.name}";
            row.Bind(definition, _progression, _wallet);

            _rows.Add(row);
        }
    }

    void RefreshRows()
    {
        foreach (var row in _rows) row.Refresh();
    }
}
