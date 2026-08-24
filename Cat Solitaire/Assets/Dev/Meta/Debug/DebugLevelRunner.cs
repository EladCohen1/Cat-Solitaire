using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Stand-in for the real solitaire game so the full hub → level → hub loop can be
/// played and balanced before the card game exists. Wire the three Report* methods
/// to buttons. Delete this once the real <see cref="ILevelRunner"/> ships.
/// </summary>
public class DebugLevelRunner : MonoBehaviour, ILevelRunner
{
    [SerializeField] TMP_Text _levelLabel;
    [SerializeField] int _winScore = 1000;

    Action<LevelResult> _onComplete;

    public void Run(LevelDef def, Action<LevelResult> onComplete)
    {
        _onComplete = onComplete;

        if (_levelLabel != null)
            _levelLabel.text = $"DEBUG LEVEL\n{(string.IsNullOrEmpty(def.Id) ? def.name : def.Id)}";
    }

    public void ReportWin() => Finish(LevelOutcome.Win);
    public void ReportLose() => Finish(LevelOutcome.Lose);
    public void ReportQuit() => Finish(LevelOutcome.Quit);

    void Finish(LevelOutcome outcome)
    {
        // Clear the callback first: a double-tap must not report twice,
        // or the player gets paid twice for one level.
        var callback = _onComplete;
        if (callback == null) return;
        _onComplete = null;

        callback(new LevelResult
        {
            Outcome = outcome,
            Score = outcome == LevelOutcome.Win ? _winScore : 0
        });
    }
}
