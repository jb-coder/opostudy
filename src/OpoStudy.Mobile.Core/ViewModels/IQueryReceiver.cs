namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>Implemented by ViewModels that receive navigation parameters (the page forwards Shell query attributes).</summary>
public interface IQueryReceiver
{
    void ApplyQuery(IReadOnlyDictionary<string, object> query);
}