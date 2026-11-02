using System.Collections.Generic;
using UnityEngine;


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
    
    void OnEnable() => Build();
    
    void Start() => Build();

    void OnDestroy()
    {
        if (_progression != null) _progression.ObjectUnlocked -= OnObjectUnlocked;
        if (_wallet != null) _wallet.Changed -= OnCurrencyChanged;
    }

    public void Open()
    {
        Build();
        RefreshRows();  
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
            if (definition == null) continue;  

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
