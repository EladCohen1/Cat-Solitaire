using UnityEngine;

/// <summary>
/// The composition root: the single place where the profile is loaded and the
/// services are constructed. Everything else receives them from here.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    public static GameBootstrap Instance { get; private set; }

    [SerializeField] HubChapterDef _startingChapter;

    PlayerProfile _profile;

    public Wallet Wallet { get; private set; }
    public ProgressionService Progression { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _profile = SaveService.Load();
        Wallet = new Wallet(_profile);
        Progression = new ProgressionService(_profile, Wallet, _startingChapter);

        // Anything that changes the profile writes it back.
        Wallet.Changed += _ => Save();
        Progression.ObjectUnlocked += _ => Save();
    }

    public void Save()
    {
        if (_profile != null) SaveService.Save(_profile);
    }

    public void RecordLevelCompleted()
    {
        if (_profile == null) return;

        _profile.LevelsCompleted++;
        Save();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) Save();   // on mobile this is the reliable one, not OnApplicationQuit
    }

    void OnApplicationQuit() => Save();
}
