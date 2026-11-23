using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.ViewModels;

namespace OpoStudy.Mobile.Core.Tests.PracticalCases;

public sealed class PracticalCasesGradingsLinkTests
{
    [Fact]
    public async Task OpenGradingsCommand_NavigatesToTheHistory()
    {
        var navigation = Substitute.For<INavigationService>();
        var sut = new PracticalCasesViewModel(
            Substitute.For<IOpoStudyApi>(),
            navigation,
            new FakeDraftStore(),
            new OfflineDoubles().State,
            NullLogger<PracticalCasesViewModel>.Instance);

        await sut.OpenGradingsCommand.ExecuteAsync(null);

        await navigation.Received(1).GoToAsync(Routes.Gradings);
    }
}