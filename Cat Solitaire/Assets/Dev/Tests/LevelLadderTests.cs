using NUnit.Framework;
using UnityEngine;

public class LevelLadderTests
{
    LevelDef _one, _two, _three;
    LevelSequenceDef _sequence;
    PlayerProfile _profile;
    LevelLadder _ladder;

    [SetUp]
    public void SetUp()
    {
        _one = NewLevel("one");
        _two = NewLevel("two");
        _three = NewLevel("three");

        _sequence = ScriptableObject.CreateInstance<LevelSequenceDef>();
        _sequence.Levels = new[] { _one, _two, _three };

        _profile = new PlayerProfile();
        _ladder = new LevelLadder(_profile, _sequence);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var o in new Object[] { _one, _two, _three, _sequence })
            Object.DestroyImmediate(o);
    }

    [Test]
    public void AFreshProfile_StartsOnTheFirstLevel()
    {
        Assert.AreSame(_one, _ladder.Current);
        Assert.AreEqual(1, _ladder.CurrentNumber);
        Assert.IsFalse(_ladder.IsRepeatingLastLevel);
    }

    [Test]
    public void EachWin_MovesToTheNextLevel()
    {
        _ladder.RecordWin();
        Assert.AreSame(_two, _ladder.Current);
        Assert.AreEqual(2, _ladder.CurrentNumber);

        _ladder.RecordWin();
        Assert.AreSame(_three, _ladder.Current);
    }

    [Test]
    public void PastTheEndOfTheSequence_TheLastLevelRepeats()
    {
        for (var i = 0; i < 10; i++) _ladder.RecordWin();

        Assert.AreSame(_three, _ladder.Current, "Running out of levels must not leave the player with nothing to play.");
        Assert.IsTrue(_ladder.IsRepeatingLastLevel);
        Assert.AreEqual(11, _ladder.CurrentNumber, "The number the player sees keeps climbing.");
    }

    [Test]
    public void TheCursorIsTheWinCount_SoItSurvivesAReorder()
    {
        _ladder.RecordWin();
        _sequence.Levels = new[] { _three, _two, _one };

        Assert.AreEqual(1, _profile.LevelsCompleted);
        Assert.AreSame(_two, _ladder.Current, "Second slot of the new order, not a stored reference to the old one.");
    }

    [Test]
    public void WithNoSequence_ThereIsNoLevelAndNothingThrows()
    {
        var empty = new LevelLadder(new PlayerProfile(), null);

        Assert.IsFalse(empty.HasLevels);
        Assert.IsNull(empty.Current);
        Assert.AreEqual(1, empty.CurrentNumber);
    }

    [Test]
    public void Changed_FiresOnEveryWin()
    {
        var fireCount = 0;
        _ladder.Changed += () => fireCount++;

        _ladder.RecordWin();
        _ladder.RecordWin();

        Assert.AreEqual(2, fireCount);
    }

    static LevelDef NewLevel(string id)
    {
        var level = ScriptableObject.CreateInstance<LevelDef>();
        level.Id = id;
        return level;
    }
}
