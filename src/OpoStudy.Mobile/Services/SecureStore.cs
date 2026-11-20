using OpoStudy.Mobile.Core.Auth;

namespace OpoStudy.Mobile.Services;

/// <summary><see cref="ISecureStore"/> backed by <see cref="SecureStorage"/> (Android Keystore).</summary>
internal sealed class SecureStore : ISecureStore
{
    public async Task<string?> GetAsync(string key)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(key);
        }
        catch (Exception)
        {
            // Keys become unreadable when the device is restored from a backup: start from scratch.
            SecureStorage.Default.RemoveAll();
            return null;
        }
    }

    public Task SetAsync(string key, string value) => SecureStorage.Default.SetAsync(key, value);

    public void Remove(string key) => SecureStorage.Default.Remove(key);
}