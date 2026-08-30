using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The scene progress popup: a row for every object in the chapter, bought and still
/// to buy.
///
/// It owns the list. Rows are built once — a chapter's contents never change at
/// runtime — and refreshed whenever a purchase lands or a balance moves, so this is
/// the only object listening however long the list gets.
///
/// Place it either on the popup itself, opened by a button that switches the
/// GameObject on, or on something that stays active with <see cref="_window"/>
/// pointing at the popup and buttons hooked to Open/Close. Both work: the list is
/// built the first time the component is enabled with the game running, and whether
/// the popup starts visible is whatever you set in the scene.
///
/// The headline and the progress bar at the top are not handled here: put a
/// <see cref="ChapterProgressView"/> on them and it fills in the chapter name, the
/// x/y, the fill and the chest, exactly as it already does for the bar in the hub.
/// </summary>
public class ObjectProgressWindow : MonoBehaviour
{
    [Header("List")]
    [SerializeField] HubObjectBarView _barPrefab;
    [Tooltip("The Content object inside the Scroll View. Rows are spawned here.")]
    [SerializeField] RectTransform _listRoot;

    [Header("Window")]
    [Tooltip("The panel Open and Close switch. Left empty, this GameObject is used.")]
    [SerializeField] GameObject _window;

    readonly List<HubObjectBarView> _rows = new();

    Wallet _wallet;
    ProgressionService _progression;
    bool _built;

    public bool IsOpen => Window.activeSelf;

    GameObject Window => _window != null ? _window : gameObject;

    // Covers the popup that starts inactive and is switched on by a button: by then
    // the services are certainly up.
    void OnEnable() => Build();

    // Covers the always-active placement, where OnEnable can beat GameBootstrap.Awake.
    void Start() => Build();

    void OnDestroy()
    {
        if (_progression != null) _progression.ObjectUnlocked -= OnObjectUnlocked;
        if (_wallet != null) _wallet.Changed -= OnCurrencyChanged;
    }

    /// <summary>Hook these to the progress button and the window's close button.</summary>
    public void Open()
    {
        Build();
        RefreshRows();   // catch anything that moved while the window was shut
        Window.SetActive(true);
    }

    public void Close() => Window.SetActive(false);

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    void Build()
    {
        if (_built) return;

        // Nothing to bind to yet — Start will come back round once the services exist.
        if (GameBootstrap.Instance == null) return;

        if (_barPrefab == null || _listRoot == null)
        {
            Debug.LogError($"{name}: needs a bar prefab and a list root.", this);
            enabled = false;
            return;
        }

        _built = true;

        _wallet = GameBootstrap.Instance.Wallet;
        _progression = GameBootstrap.Instance.Progression;

        _progression.ObjectUnlocked += OnObjectUnlocked;
        _wallet.Changed += OnCurrencyChanged;

        BuildRows();
        RefreshRows();
    }

    void OnObjectUnlocked(HubObjectDef _) => RefreshRows();

    void OnCurrencyChanged(CurrencyDef _) => RefreshRows();

    void BuildRows()
    {
        var chapter = _progression.Chapter;
        if (chapter == null || chapter.Objects == null || chapter.Objects.Length == 0)
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
