using Microsoft.Extensions.DependencyInjection;

using OpoStudy.Infrastructure.Progress;

using Shouldly;

namespace OpoStudy.Infrastructure.Tests;

public sealed class ProgressTrackingRegistrationTests
{
    [Fact]
    public void AddProgressTracking_CalledTwice_RegistersTheTrackerOnce()
    {
        var services = new ServiceCollection();

        services.AddProgressTracking();
        services.AddProgressTracking();

        services.Count(d => d.ServiceType == typeof(ProgressTracker)).ShouldBe(1);
    }
}
