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
}

public enum LevelOutcome { Win, Lose, Quit }
public struct LevelResult { public LevelOutcome Outcome; public int Score; }

public interface ILevelRunner
{
    void Run(LevelDef def, Action<LevelResult> onComplete);
}
