using UnityEngine;

public static class PlayerName
{
    // PlayerPrefs key and maximum allowed name length
    private const string Key = "playerName";
    public const int MaxLength = 16;

    // Load the saved name, or return an empty string if none is saved
    public static string Load() => PlayerPrefs.GetString(Key, "");

    public static void Save(string name)
    {
        // Sanitize names before saving so stored values follow the same rules
        PlayerPrefs.SetString(Key, Sanitize(name));
        PlayerPrefs.Save();
    }

    // Trim whitespace and limit the name to the supported length
    public static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        name = name.Trim();
        return name.Length > MaxLength ? name.Substring(0, MaxLength) : name;
    }
}