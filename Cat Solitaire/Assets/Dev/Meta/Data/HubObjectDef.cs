using UnityEngine;

[CreateAssetMenu(menuName = "Cat/Hub Object")]
public class HubObjectDef : ScriptableObject
{
    public string Id;
    public string DisplayName;
    public Price Cost;
    public Sprite LockedSprite, UnlockedSprite;
}

[CreateAssetMenu(menuName = "Cat/Hub Chapter")]
public class HubChapterDef : ScriptableObject
{
    public string Id, DisplayName;
    public HubObjectDef[] Objects;
    public int RequiredUnlocks;        
}
