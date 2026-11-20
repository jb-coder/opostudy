using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using OpoStudy.Mobile.Core.Offline;

namespace OpoStudy.Mobile.Core.Tests;

/// <summary>The offline collaborators of the ViewModels as substitutes (online by default), plus the shared state.</summary>
internal sealed class OfflineDoubles
{
    public OfflineDoubles()
    {
        Network.HasInternet.Returns(true);
        State = new OfflineState(Network);
        Synchronizer = Substitute.For<IOfflineSynchronizer>();
    }

    public INetworkStatus Network { get; } = Substitute.For<INetworkStatus>();

    public OfflineState State { get; }

    public IOfflineContentStore Content { get; } = Substitute.For<IOfflineContentStore>();

    public IOfflineQuizService Quizzes { get; } = Substitute.For<IOfflineQuizService>();

    public IOfflineSynchronizer Synchronizer { get; }

}
