using UnityEngine;

/// <summary>
/// The order levels are played in. Pure content: the player's place in it is
/// <see cref="PlayerProfile.LevelsCompleted"/>, a count, so nothing here is ever
/// written to. Reordering the list changes which level someone sees next, but it
/// can never leave a save pointing at a level that no longer exists.
/// </summary>
[CreateAssetMenu(menuName = "Cat/Level Sequence")]
public class LevelSequenceDef : ScriptableObject
{
    public LevelDef[] Levels;
}
