using System.Globalization;
using System.Text.Json;

using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.Reminders;

namespace OpoStudy.Mobile.Core.PracticalCases;

/// <summary>
/// <see cref="IPracticalCaseDraftStore"/> over <see cref="IPreferencesStore"/>. The key includes the uid so another user
/// of the device never sees the draft. A corrupt value is discarded.
/// </summary>
public sealed class PreferencesPracticalCaseDraftStore(IPreferencesStore preferences, IAuthService auth) : IPracticalCaseDraftStore
{
    public PracticalCaseDraft? Find(Guid practicalCaseId)
    {
        var key = KeyFor(practicalCaseId);
        if (key is null)
        {
            return null;
        }

        var json = preferences.GetString(key);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            var draft = JsonSerializer.Deserialize(json, PracticalCaseDraftJsonContext.Default.PracticalCaseDraft);
            if (draft?.Answers is not null)
            {
                return draft;
            }
        }
        catch (JsonException)
        {
            // Falls through to the removal below.
        }

        preferences.Remove(key);
        return null;
    }

    public void Save(Guid practicalCaseId, PracticalCaseDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (KeyFor(practicalCaseId) is { } key)
        {
            preferences.SetString(key, JsonSerializer.Serialize(draft, PracticalCaseDraftJsonContext.Default.PracticalCaseDraft));
        }
    }

    public void Delete(Guid practicalCaseId)
    {
        if (KeyFor(practicalCaseId) is { } key)
        {
            preferences.Remove(key);
        }
    }

    private string? KeyFor(Guid practicalCaseId) =>
        auth.UserId is { Length: > 0 } uid ? string.Create(CultureInfo.InvariantCulture, $"practical-case-draft:{uid}:{practicalCaseId}") : null;
}