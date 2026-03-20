using Microsoft.EntityFrameworkCore;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Mappers;

namespace Nocturne.API.Services;

/// <summary>
/// Domain service for step count record operations.
/// Inherits CRUD + broadcasting from SimpleEntityService.
/// </summary>
public class StepCountService
    : SimpleEntityService<StepCount, StepCountEntity>,
        IStepCountService
{
    public StepCountService(
        NocturneDbContext dbContext,
        IDocumentProcessingService documentProcessingService,
        ISignalRBroadcastService signalRBroadcastService,
        ILogger<StepCountService> logger
    )
        : base(dbContext, documentProcessingService, signalRBroadcastService, logger) { }

    protected override DbSet<StepCountEntity> EntitySet => DbContext.StepCounts;
    protected override string CollectionName => "stepcount";
    protected override string EntityTypeName => "step count";

    protected override StepCount ToDomainModel(StepCountEntity entity) =>
        StepCountMapper.ToDomainModel(entity);

    protected override StepCountEntity ToEntity(StepCount model) =>
        StepCountMapper.ToEntity(model);

    protected override void UpdateEntity(StepCountEntity entity, StepCount model) =>
        StepCountMapper.UpdateEntity(entity, model);

    protected override IOrderedQueryable<StepCountEntity> OrderByMills(
        IQueryable<StepCountEntity> query
    ) => query.OrderByDescending(s => s.Mills);

    protected override Task<StepCountEntity?> FindByIdAsync(
        string id,
        CancellationToken cancellationToken
    ) =>
        Guid.TryParse(id, out var guid)
            ? DbContext.StepCounts.FirstOrDefaultAsync(s => s.Id == guid, cancellationToken)
            : DbContext.StepCounts.FirstOrDefaultAsync(
                s => s.OriginalId == id,
                cancellationToken
            );

    public Task<IEnumerable<StepCount>> GetStepCountsAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    ) => GetAllAsync(count, skip, cancellationToken);

    public Task<StepCount?> GetStepCountByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    ) => GetByIdAsync(id, cancellationToken);

    public Task<IEnumerable<StepCount>> CreateStepCountsAsync(
        IEnumerable<StepCount> stepCounts,
        CancellationToken cancellationToken = default
    ) => CreateManyAsync(stepCounts, cancellationToken);

    public Task<StepCount?> UpdateStepCountAsync(
        string id,
        StepCount stepCount,
        CancellationToken cancellationToken = default
    ) => UpdateOneAsync(id, stepCount, cancellationToken);

    public Task<bool> DeleteStepCountAsync(
        string id,
        CancellationToken cancellationToken = default
    ) => DeleteOneAsync(id, cancellationToken);
}
