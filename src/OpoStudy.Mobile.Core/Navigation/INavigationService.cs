namespace OpoStudy.Mobile.Core.Navigation;

/// <summary>Platform-independent navigation.</summary>
public interface INavigationService
{
    Task GoToAsync(string route, IReadOnlyDictionary<string, object>? parameters = null);
}
