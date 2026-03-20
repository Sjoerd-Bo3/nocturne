using Microsoft.EntityFrameworkCore;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Mappers;

namespace Nocturne.API.Services;

/// <summary>
/// Domain service for heart rate record operations.
/// Inherits CRUD + broadcasting from SimpleEntityService.
/// </summary>
public class HeartRateService
    : SimpleEntityService<HeartRate, HeartRateEntity>,
        IHeartRateService
{
    public HeartRateService(
        NocturneDbContext dbContext,
        IDocumentProcessingService documentProcessingService,
        ISignalRBroadcastService signalRBroadcastService,
        ILogger<HeartRateService> logger
    )
        : base(dbContext, documentProcessingService, signalRBroadcastService, logger) { }

    protected override DbSet<HeartRateEntity> EntitySet => DbContext.HeartRates;
    protected override string CollectionName => "heartrate";
    protected override string EntityTypeName => "heart rate";

    protected override HeartRate ToDomainModel(HeartRateEntity entity) =>
        HeartRateMapper.ToDomainModel(entity);

    protected override HeartRateEntity ToEntity(HeartRate model) =>
        HeartRateMapper.ToEntity(model);

    protected override void UpdateEntity(HeartRateEntity entity, HeartRate model) =>
        HeartRateMapper.UpdateEntity(entity, model);

    protected override IOrderedQueryable<HeartRateEntity> OrderByMills(
        IQueryable<HeartRateEntity> query
    ) => query.OrderByDescending(h => h.Mills);

    protected override Task<HeartRateEntity?> FindByIdAsync(
        string id,
        CancellationToken cancellationToken
    ) =>
        Guid.TryParse(id, out var guid)
            ? DbContext.HeartRates.FirstOrDefaultAsync(h => h.Id == guid, cancellationToken)
            : DbContext.HeartRates.FirstOrDefaultAsync(
                h => h.OriginalId == id,
                cancellationToken
            );

    public Task<IEnumerable<HeartRate>> GetHeartRatesAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    ) => GetAllAsync(count, skip, cancellationToken);

    public Task<HeartRate?> GetHeartRateByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    ) => GetByIdAsync(id, cancellationToken);

    public Task<IEnumerable<HeartRate>> CreateHeartRatesAsync(
        IEnumerable<HeartRate> heartRates,
        CancellationToken cancellationToken = default
    ) => CreateManyAsync(heartRates, cancellationToken);

    public Task<HeartRate?> UpdateHeartRateAsync(
        string id,
        HeartRate heartRate,
        CancellationToken cancellationToken = default
    ) => UpdateOneAsync(id, heartRate, cancellationToken);

    public Task<bool> DeleteHeartRateAsync(
        string id,
        CancellationToken cancellationToken = default
    ) => DeleteOneAsync(id, cancellationToken);
}
