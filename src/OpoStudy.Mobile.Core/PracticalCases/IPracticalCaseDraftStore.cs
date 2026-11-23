namespace OpoStudy.Mobile.Core.PracticalCases;

/// <summary>Local drafts of practical case answers, kept per signed-in user.</summary>
public interface IPracticalCaseDraftStore
{
    /// <summary>The draft of the case, or <c>null</c> when there is none, it is corrupt or nobody is signed in.</summary>
    PracticalCaseDraft? Find(Guid practicalCaseId);

    /// <summary>Saves the draft. Without a signed-in user it does nothing.</summary>
    void Save(Guid practicalCaseId, PracticalCaseDraft draft);

    void Delete(Guid practicalCaseId);
}