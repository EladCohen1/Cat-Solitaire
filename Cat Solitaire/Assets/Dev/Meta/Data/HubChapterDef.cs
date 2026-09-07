using UnityEngine;

/// <summary>
/// One hub "scene" the player works through: a set of buyable props plus how many
/// of them must be unlocked to complete it (the "4/8" goal).
/// </summary>
[CreateAssetMenu(menuName = "Cat/Hub Chapter")]
public class HubChapterDef : ScriptableObject
{
    public string Id;
    public string DisplayName;
    public HubObjectDef[] Objects;
    public int RequiredUnlocks;

    [Tooltip("The chest at the end of the progress bar. Paid out once, when the player claims it.")]
    public CurrencyAmount[] CompletionRewards;
}
