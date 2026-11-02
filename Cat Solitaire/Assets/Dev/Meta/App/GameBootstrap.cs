using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    public static GameBootstrap Instance { get; private set; }

    [SerializeField] HubChapterDef _startingChapter;
    [SerializeField] LevelSequenceDef _levels;

    PlayerProfile _profile;

    public Wallet Wallet { get; private set; }
    public ProgressionService Progression { get; private set; }
    public LevelLadder Ladder { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _profile = SaveService.Load();
        Wallet = new Wallet(_profile);
        Progression = new ProgressionService(_profile, Wallet, _startingChapter);
        Ladder = new LevelLadder(_profile, _levels);
        
        Wallet.Changed += _ => Save();
        Progression.ObjectUnlocked += _ => Save();
        Progression.ChapterRewardClaimed += Save; 
    }

    public void Save()
    {
        if (_profile != null) SaveService.Save(_profile);
    }

    public void RecordLevelCompleted()
    {
        if (_profile == null) return;

        Ladder.RecordWin();  
        Save();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) Save();  
    }

    void OnApplicationQuit() => Save();
}
