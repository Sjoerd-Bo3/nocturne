using Microsoft.EntityFrameworkCore;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data;

namespace Nocturne.API.Services;

/// <summary>
/// Abstract base for simple entity CRUD services that use DbContext directly
/// with document processing and SignalR broadcasting.
/// Eliminates boilerplate for services like HeartRateService and StepCountService
/// that follow the same get/create/update/delete + broadcast pattern.
/// </summary>
public abstract class SimpleEntityService<TDomain, TEntity>
    where TDomain : class, IProcessableDocument
    where TEntity : class
{
    protected readonly NocturneDbContext DbContext;
    protected readonly IDocumentProcessingService DocumentProcessingService;
    protected readonly ISignalRBroadcastService SignalRBroadcastService;
    protected readonly ILogger Logger;

    protected SimpleEntityService(
        NocturneDbContext dbContext,
        IDocumentProcessingService documentProcessingService,
        ISignalRBroadcastService signalRBroadcastService,
        ILogger logger
    )
    {
        DbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        DocumentProcessingService =
            documentProcessingService
            ?? throw new ArgumentNullException(nameof(documentProcessingService));
        SignalRBroadcastService =
            signalRBroadcastService
            ?? throw new ArgumentNullException(nameof(signalRBroadcastService));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>The DbSet for this entity type</summary>
    protected abstract DbSet<TEntity> EntitySet { get; }

    /// <summary>The collection name for SignalR broadcasts (e.g., "heartrate")</summary>
    protected abstract string CollectionName { get; }

    /// <summary>The entity type name for log messages (e.g., "heart rate")</summary>
    protected abstract string EntityTypeName { get; }

    protected abstract TDomain ToDomainModel(TEntity entity);
    protected abstract TEntity ToEntity(TDomain model);
    protected abstract void UpdateEntity(TEntity entity, TDomain model);
    protected abstract IOrderedQueryable<TEntity> OrderByMills(IQueryable<TEntity> query);
    protected abstract Task<TEntity?> FindByIdAsync(
        string id,
        CancellationToken cancellationToken
    );

    protected async Task<IEnumerable<TDomain>> GetAllAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            Logger.LogDebug(
                "Getting {EntityType} records with count: {Count}, skip: {Skip}",
                EntityTypeName,
                count,
                skip
            );

            var entities = await OrderByMills(EntitySet)
                .Skip(skip)
                .Take(count)
                .ToListAsync(cancellationToken);

            return entities.Select(ToDomainModel);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting {EntityType} records", EntityTypeName);
            throw;
        }
    }

    protected async Task<TDomain?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            Logger.LogDebug("Getting {EntityType} record by ID: {Id}", EntityTypeName, id);

            var entity = await FindByIdAsync(id, cancellationToken);
            return entity is null ? null : ToDomainModel(entity);
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "Error getting {EntityType} record by ID: {Id}",
                EntityTypeName,
                id
            );
            throw;
        }
    }

    protected async Task<IEnumerable<TDomain>> CreateManyAsync(
        IEnumerable<TDomain> items,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var itemList = items.ToList();
            Logger.LogDebug(
                "Creating {Count} {EntityType} records",
                itemList.Count,
                EntityTypeName
            );

            var processed = DocumentProcessingService.ProcessDocuments(itemList).ToList();
            var entities = processed.Select(ToEntity).ToList();
            await EntitySet.AddRangeAsync(entities, cancellationToken);
            await DbContext.SaveChangesAsync(cancellationToken);

            var result = entities.Select(ToDomainModel).ToList();

            await SignalRBroadcastService.BroadcastStorageCreateAsync(
                CollectionName,
                new { collection = CollectionName, data = result, count = result.Count }
            );

            Logger.LogDebug(
                "Successfully created {Count} {EntityType} records",
                result.Count,
                EntityTypeName
            );
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error creating {EntityType} records", EntityTypeName);
            throw;
        }
    }

    protected async Task<TDomain?> UpdateOneAsync(
        string id,
        TDomain item,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            Logger.LogDebug("Updating {EntityType} record with ID: {Id}", EntityTypeName, id);

            var entity = await FindByIdAsync(id, cancellationToken);
            if (entity is null)
            {
                Logger.LogDebug(
                    "{EntityType} record with ID {Id} not found for update",
                    EntityTypeName,
                    id
                );
                return null;
            }

            UpdateEntity(entity, item);
            await DbContext.SaveChangesAsync(cancellationToken);

            var result = ToDomainModel(entity);

            await SignalRBroadcastService.BroadcastStorageUpdateAsync(
                CollectionName,
                new { collection = CollectionName, data = result, id }
            );

            Logger.LogDebug(
                "Successfully updated {EntityType} record with ID: {Id}",
                EntityTypeName,
                id
            );
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "Error updating {EntityType} record with ID: {Id}",
                EntityTypeName,
                id
            );
            throw;
        }
    }

    protected async Task<bool> DeleteOneAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            Logger.LogDebug("Deleting {EntityType} record with ID: {Id}", EntityTypeName, id);

            var entity = await FindByIdAsync(id, cancellationToken);
            if (entity is null)
            {
                Logger.LogDebug(
                    "{EntityType} record with ID {Id} not found for deletion",
                    EntityTypeName,
                    id
                );
                return false;
            }

            EntitySet.Remove(entity);
            await DbContext.SaveChangesAsync(cancellationToken);

            await SignalRBroadcastService.BroadcastStorageDeleteAsync(
                CollectionName,
                new { collection = CollectionName, id }
            );

            Logger.LogDebug(
                "Successfully deleted {EntityType} record with ID: {Id}",
                EntityTypeName,
                id
            );
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "Error deleting {EntityType} record with ID: {Id}",
                EntityTypeName,
                id
            );
            throw;
        }
    }
}
