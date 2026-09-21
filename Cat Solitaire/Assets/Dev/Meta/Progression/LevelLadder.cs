using System;
using UnityEngine;

/// <summary>
/// Which level the player plays next.
///
/// The cursor is simply how many levels they have won, so the ladder keeps no state
/// of its own and cannot drift out of step with the save. Past the end of the
/// sequence the last level repeats: running out of authored content should be a flat
/// difficulty curve, not a dead Play button.
/// </summary>
public class LevelLadder
{
    readonly PlayerProfile _profile;
    readonly LevelSequenceDef _sequence;

    /// <summary>Raised when the current level moves on, for the play button to re-read.</summary>
    public event Action Changed;

    public LevelLadder(PlayerProfile profile, LevelSequenceDef sequence)
    {
        _profile = profile;
        _sequence = sequence;
    }

    public bool HasLevels => _sequence != null && _sequence.Levels != null && _sequence.Levels.Length > 0;

    /// <summary>Position in the sequence, held at the last authored level once past the end.</summary>
    public int CurrentIndex =>
        HasLevels ? Mathf.Clamp(_profile.LevelsCompleted, 0, _sequence.Levels.Length - 1) : 0;

    /// <summary>What the player calls it — "Level 7". Keeps counting past the last authored level.</summary>
    public int CurrentNumber => _profile.LevelsCompleted + 1;

    public LevelDef Current => HasLevels ? _sequence.Levels[CurrentIndex] : null;

    /// <summary>True once the sequence has run out and the last level is being replayed.</summary>
    public bool IsRepeatingLastLevel => HasLevels && _profile.LevelsCompleted >= _sequence.Levels.Length;

    /// <summary>
    /// Called from <see cref="GameBootstrap"/> when a level is won — the one thing
    /// that moves the cursor. Losing and quitting leave the player where they are.
    /// </summary>
    public void RecordWin()
    {
        _profile.LevelsCompleted++;
        Changed?.Invoke();
    }
}
