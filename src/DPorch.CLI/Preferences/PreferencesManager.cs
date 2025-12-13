using System.Text.Json;

namespace DPorch.CLI.Preferences;

sealed class PreferencesManager(string? prefsFilePath = null)
{
    const string PrefsDirectoryName = "DPorch";
    const string PrefsFileName = "settings.json";

    static readonly JsonSerializerOptions JsonOptions = new()
    { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    UserPreferences? _prefs;

    /// <summary>
    ///     Gets the path to the preferences file.
    /// </summary>
    public string PreferencesFilePath { get; } = prefsFilePath ?? GetDefaultPreferencesFilePath();

    /// <summary>
    ///     Gets cached <see cref="UserPreferences" /> instance that contains values loaded from a preferences file.
    /// </summary>
    /// <remarks>
    ///     If <see cref="UserPreferences" /> is not loaded, <see cref="LoadPreferences" /> is called to attempt to load
    ///     it. <see cref="IsPreferencesFile" /> is called to check if the file exists before attempting to load it.
    /// </remarks>
    public UserPreferences UserPreferences =>
        _prefs ?? throw new NullReferenceException("User preferences have not been loaded yet");

    static string GetDefaultPreferencesFilePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        return Path.Combine(appDataPath, PrefsDirectoryName, PrefsFileName);
    }

    /// <summary>
    ///     Checks if the preferences file exists at the path provided to the constructor.
    /// </summary>
    /// <returns> True if the preferences file exists, false otherwise. </returns>
    public bool IsPreferencesFile()
    {
        return File.Exists(PreferencesFilePath);
    }

    /// <summary>
    ///     Loads and returns the <see cref="UserPreferences" /> instance from the preferences file.
    /// </summary>
    /// <returns> The <see cref="UserPreferences" /> instance loaded from the preferences file. </returns>
    /// <exception cref="NullReferenceException">The deserialize function returned null because the
    /// config was empty or malformed</exception>
    public UserPreferences LoadPreferences()
    {
        if (!IsPreferencesFile())
            throw new FileNotFoundException("Preferences file not found at provided path", PreferencesFilePath);
        
        var json = File.ReadAllText(PreferencesFilePath);

        return JsonSerializer.Deserialize<UserPreferences>(json, JsonOptions) ??
               throw new NullReferenceException("Preferences file is empty or malformed.");
    }

    /// <summary>
    ///     Gets the existing preferences or creates a new default instance if no file exists.
    /// </summary>
    /// <returns> The existing or new <see cref="UserPreferences" /> instance. </returns>
    public UserPreferences GetOrCreatePreferences()
    {
        if (_prefs != null)
            return _prefs;

        if (IsPreferencesFile())
        {
            _prefs = LoadPreferences();

            return _prefs;
        }

        _prefs = new UserPreferences();

        return _prefs;
    }

    /// <summary>
    ///     Saves the provided preferences to the preferences file.
    /// </summary>
    /// <param name="prefs"> The preferences to save. </param>
    /// <exception cref="Exception"> Thrown when saving fails. </exception>
    public void SavePreferences(UserPreferences prefs)
    {
        try
        {
            var directory = Path.GetDirectoryName(PreferencesFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(prefs, JsonOptions);
            File.WriteAllText(PreferencesFilePath, json);
            _prefs = prefs;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to save preferences file: {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Uses the default preference file path to determine if the user is new (i.e., no preferences file exists).
    /// </summary>
    public static bool IsNewUser()
    {
        return !new PreferencesManager().IsPreferencesFile();
    }
}