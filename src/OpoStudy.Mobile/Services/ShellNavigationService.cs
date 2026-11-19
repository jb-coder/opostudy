using OpoStudy.Mobile.Core.Navigation;

namespace OpoStudy.Mobile.Services;

/// <summary><see cref="INavigationService"/> backed by Shell routing.</summary>
public sealed class ShellNavigationService : INavigationService
{
    public Task GoToAsync(string route, IReadOnlyDictionary<string, object>? parameters = null)
    {
        var shell = Shell.Current ?? throw new InvalidOperationException("Shell is not initialized.");

        return parameters is null
            ? shell.GoToAsync(route)
            : shell.GoToAsync(route, new Dictionary<string, object>(parameters));
    }
}