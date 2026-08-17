using UnityEngine;

/// <summary>
/// One buyable prop in the hub. Immutable content — whether it is unlocked
/// lives in the PlayerProfile, never here.
/// </summary>
[CreateAssetMenu(menuName = "Cat/Hub Object")]
public class HubObjectDef : ScriptableObject
{
    public string Id;
    public string DisplayName;
    public Price Cost;
    public Sprite LockedSprite, UnlockedSprite;
}
