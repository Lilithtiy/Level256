using UnityEngine;

public static class PlayerName
{
    private const string Key = "playerName";
    public const int MaxLength = 16;

    public static string Load() => PlayerPrefs.GetString(Key, "");

    public static void Save(string name)
    {
        PlayerPrefs.SetString(Key, Sanitize(name));
        PlayerPrefs.Save();
    }

    public static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        name = name.Trim();
        return name.Length > MaxLength ? name.Substring(0, MaxLength) : name;
    }
}