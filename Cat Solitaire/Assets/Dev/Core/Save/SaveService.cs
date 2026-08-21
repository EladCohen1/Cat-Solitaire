using System.IO;
using UnityEngine;

/// <summary>
/// Reads and writes the one and only <see cref="PlayerProfile"/> as JSON.
/// Nothing else in the project touches the file system.
/// </summary>
public static class SaveService
{
    const string FileName = "profile.json";

    static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

    public static PlayerProfile Load()
    {
        if (!File.Exists(Path)) return new PlayerProfile();

        try
        {
            var json = File.ReadAllText(Path);
            return JsonUtility.FromJson<PlayerProfile>(json) ?? new PlayerProfile();
        }
        catch (System.Exception e)
        {
            // A corrupt save must never brick the game — start fresh and keep going.
            Debug.LogError($"Failed to load profile, starting a new one: {e.Message}");
            return new PlayerProfile();
        }
    }

    public static void Save(PlayerProfile profile)
    {
        try
        {
            File.WriteAllText(Path, JsonUtility.ToJson(profile, prettyPrint: true));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save profile: {e.Message}");
        }
    }

    public static void Delete()
    {
        if (File.Exists(Path)) File.Delete(Path);
    }
}
