using AFH.Location.Application.Abstractions;
using AFH.Location.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;

namespace AFH.Location.Infrastructure.Persistence.Repositories;

public sealed class AdviserSourceRefreshCoordinator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Lock _sync = new();
    private Lazy<Task<IReadOnlyList<Adviser>>>? _allAdvisersRefresh;

    public AdviserSourceRefreshCoordinator(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public Task<IReadOnlyList<Adviser>> RefreshAllAsync(CancellationToken ct)
    {
        Lazy<Task<IReadOnlyList<Adviser>>> refresh;

        lock (_sync)
        {
            _allAdvisersRefresh ??= new Lazy<Task<IReadOnlyList<Adviser>>>(
                RefreshAllCoreAsync,
                LazyThreadSafetyMode.ExecutionAndPublication);
            refresh = _allAdvisersRefresh;
        }

        return AwaitRefreshAsync(refresh, ct);
    }

    private async Task<IReadOnlyList<Adviser>> AwaitRefreshAsync(
        Lazy<Task<IReadOnlyList<Adviser>>> refresh,
        CancellationToken ct)
    {
        try
        {
            return await refresh.Value.WaitAsync(ct);
        }
        finally
        {
            if (refresh.IsValueCreated && refresh.Value.IsCompleted)
            {
                lock (_sync)
                {
                    if (ReferenceEquals(_allAdvisersRefresh, refresh) &&
                        refresh.IsValueCreated &&
                        refresh.Value.IsCompleted)
                    {
                        _allAdvisersRefresh = null;
                    }
                }
            }
        }
    }

    private async Task<IReadOnlyList<Adviser>> RefreshAllCoreAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var sourceRepository = scope.ServiceProvider.GetRequiredService<IAdviserSourceRepository>();
        var cacheRepository = scope.ServiceProvider.GetRequiredService<IAdviserReferenceCacheRepository>();

        var live = await sourceRepository.GetAllAsync(null, CancellationToken.None);
        if (live.Count > 0)
            await cacheRepository.UpsertAsync(live, DateTime.UtcNow, CancellationToken.None);

        return live;
    }
}
