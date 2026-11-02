using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Cat/Level")]
public class LevelDef : ScriptableObject
{
    public string Id;
    public Price EntryCost;
    public CurrencyAmount[] WinRewards;
    public CurrencyAmount[] LoseRewards;
    public ScriptableObject RuleData;   // his deck layout — Meta never reads it

    [Tooltip("The reward bar shown before the level. Empty means one tier, at the entry cost above.")]
    public WagerLadderDef Wagers;
}

public enum LevelOutcome { Win, Lose, Quit }
public struct LevelResult { public LevelOutcome Outcome; public int Score; }

/// <summary>
/// What a level may charge for and pay out while it is being played, for things
/// bought mid-round — taking a move back, a wildcard, a bonus for a good run.
///
/// It exists so the minigame can reach the player's money without reaching the
/// meta game: the level is handed one of these and never learns what is behind it.
/// A test passes its own, and nothing has to stand a wallet up to run.
/// </summary>
public interface ILevelPurse
{
    bool CanAfford(Price price);

    /// <summary>Takes the money, or leaves it alone and says no.</summary>
    bool TrySpend(Price price);

    void Grant(CurrencyAmount[] rewards);
}

public interface ILevelRunner
{
    /// <param name="purse">The player's money, for anything bought mid-round.</param>
    void Run(LevelDef def, ILevelPurse purse, Action<LevelResult> onComplete);
}
