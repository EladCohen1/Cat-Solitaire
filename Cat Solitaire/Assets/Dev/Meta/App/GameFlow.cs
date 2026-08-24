using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns the hub → level → hub round trip, and it is the only place money changes
/// hands around a level: it charges the entry cost before loading and grants the
/// rewards after the result comes back. The level itself never touches the wallet.
///
/// Lives on the same persistent GameObject as <see cref="GameBootstrap"/> so it
/// survives while the level scene is loaded.
/// </summary>
public class GameFlow : MonoBehaviour
{
    public static GameFlow Instance { get; private set; }

    [SerializeField] string _levelSceneName = "Solitaire";

    /// <summary>Fired after the level is unloaded and rewards are granted — hook a results popup here.</summary>
    public event Action<LevelDef, LevelResult, CurrencyAmount[]> LevelFinished;

    /// <summary>Fired when the player taps play but cannot pay the entry cost.</summary>
    public event Action<Price> EntryCostRejected;

    public bool IsLevelRunning { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public void PlayLevel(LevelDef level)
    {
        if (level == null)
        {
            Debug.LogError("GameFlow.PlayLevel called with no LevelDef.", this);
            return;
        }

        if (IsLevelRunning) return;

        var wallet = GameBootstrap.Instance.Wallet;
        if (!wallet.CanAfford(level.EntryCost))
        {
            EntryCostRejected?.Invoke(level.EntryCost);
            return;
        }

        StartCoroutine(RunLevel(level));
    }

    IEnumerator RunLevel(LevelDef level)
    {
        IsLevelRunning = true;
        var wallet = GameBootstrap.Instance.Wallet;

        if (!wallet.TrySpend(level.EntryCost))
        {
            IsLevelRunning = false;
            yield break;
        }

        var hiddenHubRoots = HideActiveSceneRoots();

        yield return SceneManager.LoadSceneAsync(_levelSceneName, LoadSceneMode.Additive);
        var levelScene = SceneManager.GetSceneByName(_levelSceneName);
        var runner = FindRunner(levelScene);

        if (runner == null)
        {
            // Never keep the player's money for a level we failed to start.
            Debug.LogError($"No ILevelRunner found in scene '{_levelSceneName}'. Refunding entry cost.", this);
            wallet.Grant(level.EntryCost?.Costs);

            yield return UnloadAndRestore(levelScene, hiddenHubRoots);
            IsLevelRunning = false;
            yield break;
        }

        var finished = false;
        var result = default(LevelResult);
        runner.Run(level, r => { result = r; finished = true; });

        while (!finished) yield return null;

        var rewards = ResolveRewards(level, result.Outcome);
        wallet.Grant(rewards);

        if (result.Outcome == LevelOutcome.Win)
            GameBootstrap.Instance.RecordLevelCompleted();

        yield return UnloadAndRestore(levelScene, hiddenHubRoots);

        IsLevelRunning = false;
        LevelFinished?.Invoke(level, result, rewards);
    }

    static CurrencyAmount[] ResolveRewards(LevelDef level, LevelOutcome outcome)
    {
        switch (outcome)
        {
            case LevelOutcome.Win: return level.WinRewards;
            case LevelOutcome.Lose: return level.LoseRewards;
            default: return Array.Empty<CurrencyAmount>();   // quitting pays nothing
        }
    }

    /// <summary>
    /// Found through the interface in Core, so Meta needs no reference to the Solitaire assembly.
    /// </summary>
    static ILevelRunner FindRunner(Scene scene)
    {
        if (!scene.IsValid()) return null;

        foreach (var root in scene.GetRootGameObjects())
        {
            var runner = root.GetComponentInChildren<ILevelRunner>(includeInactive: true);
            if (runner != null) return runner;
        }
        return null;
    }

    /// <summary>
    /// Hides the hub while the level is on screen. Returns only what we actually
    /// switched off, so anything already hidden stays hidden on the way back.
    /// </summary>
    static List<GameObject> HideActiveSceneRoots()
    {
        var hidden = new List<GameObject>();

        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!root.activeSelf) continue;

            root.SetActive(false);
            hidden.Add(root);
        }
        return hidden;
    }

    static IEnumerator UnloadAndRestore(Scene levelScene, List<GameObject> hiddenHubRoots)
    {
        if (levelScene.IsValid() && levelScene.isLoaded)
            yield return SceneManager.UnloadSceneAsync(levelScene);

        foreach (var root in hiddenHubRoots)
            if (root != null) root.SetActive(true);
    }
}
