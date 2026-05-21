using AFH.Adviser.Application.Abstractions;
using Entities = AFH.Adviser.Domain.Entities;
using AFH.Adviser.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;

namespace AFH.Adviser.Infrastructure.Persistence.Repositories;

public sealed class AdviserSourceRefreshCoordinator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Lock _sync = new();
    private Lazy<Task<IReadOnlyList<Entities.Adviser>>>? _allAdvisersRefresh;

    public AdviserSourceRefreshCoordinator(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public Task<IReadOnlyList<Entities.Adviser>> RefreshAllAsync(CancellationToken ct)
    {
        Lazy<Task<IReadOnlyList<Entities.Adviser>>> refresh;

        lock (_sync)
        {
            _allAdvisersRefresh ??= new Lazy<Task<IReadOnlyList<Entities.Adviser>>>(
                RefreshAllCoreAsync,
                LazyThreadSafetyMode.ExecutionAndPublication);
            refresh = _allAdvisersRefresh;
        }

        return AwaitRefreshAsync(refresh, ct);
    }

    private async Task<IReadOnlyList<Entities.Adviser>> AwaitRefreshAsync(
        Lazy<Task<IReadOnlyList<Entities.Adviser>>> refresh,
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

    private async Task<IReadOnlyList<Entities.Adviser>> RefreshAllCoreAsync()
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
