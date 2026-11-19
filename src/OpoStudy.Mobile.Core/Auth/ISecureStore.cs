namespace OpoStudy.Mobile.Core.Auth;

/// <summary>Encrypted key/value storage (Android Keystore in the app).</summary>
public interface ISecureStore
{
    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value);

    void Remove(string key);
}