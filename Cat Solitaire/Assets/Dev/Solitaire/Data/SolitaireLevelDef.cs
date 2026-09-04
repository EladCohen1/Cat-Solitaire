using UnityEngine;

/// <summary>
/// The rules half of a level, dropped into <see cref="LevelDef.RuleData"/>. The
/// meta game never reads any of this — it only knows a level was won or lost.
/// </summary>
[CreateAssetMenu(menuName = "Cat/Solitaire Rules")]
public class SolitaireLevelDef : ScriptableObject
{
    public SolitaireBoardLayout Layout;

    [Tooltip("0 deals a fresh board every attempt. Any other value always deals the same board.")]
    public int Seed;

    [Tooltip("Score the player must also reach to win. 0 means clearing the board is enough on its own.")]
    public int TargetScore;
}
