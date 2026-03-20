using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Mappers;

namespace Nocturne.Infrastructure.Data.Tests;

[Trait("Category", "Unit")]
public class SoftDeleteQueryFilterTests : IDisposable
{
    private readonly DbConnection _connection;
    private readonly DbContextOptions<NocturneDbContext> _contextOptions;
    private readonly Guid _tenantId = Guid.CreateVersion7();

    public SoftDeleteQueryFilterTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _contextOptions = new DbContextOptionsBuilder<NocturneDbContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .Options;
        using var context = new NocturneDbContext(_contextOptions);
        context.Database.EnsureCreated();
    }

    private NocturneDbContext CreateContext()
    {
        var context = new NocturneDbContext(_contextOptions);
        context.TenantId = _tenantId;
        return context;
    }

    [Fact]
    public async Task GetEntries_ExcludesSoftDeletedRecords()
    {
        using var context = CreateContext();
        var activeEntry = new EntryEntity
        {
            Id = Guid.CreateVersion7(),
            Mills = 1000,
            Mgdl = 120,
            Type = "sgv",
            TenantId = _tenantId,
            DeletedAt = null,
        };
        var deletedEntry = new EntryEntity
        {
            Id = Guid.CreateVersion7(),
            Mills = 2000,
            Mgdl = 130,
            Type = "sgv",
            TenantId = _tenantId,
            DeletedAt = DateTime.UtcNow,
        };
        context.Entries.AddRange(activeEntry, deletedEntry);
        await context.SaveChangesAsync();

        var results = await context.Entries.ToListAsync();

        Assert.Single(results);
        Assert.Equal(activeEntry.Id, results[0].Id);
    }

    [Fact]
    public async Task GetEntries_IgnoreQueryFilters_IncludesSoftDeletedRecords()
    {
        using var context = CreateContext();
        var activeEntry = new EntryEntity
        {
            Id = Guid.CreateVersion7(),
            Mills = 1000,
            Mgdl = 120,
            Type = "sgv",
            TenantId = _tenantId,
            DeletedAt = null,
        };
        var deletedEntry = new EntryEntity
        {
            Id = Guid.CreateVersion7(),
            Mills = 2000,
            Mgdl = 130,
            Type = "sgv",
            TenantId = _tenantId,
            DeletedAt = DateTime.UtcNow,
        };
        context.Entries.AddRange(activeEntry, deletedEntry);
        await context.SaveChangesAsync();

        var results = await context.Entries.IgnoreQueryFilters().ToListAsync();

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetTreatments_ExcludesSoftDeletedRecords()
    {
        using var context = CreateContext();
        var activeTreatment = new TreatmentEntity
        {
            Id = Guid.CreateVersion7(),
            Mills = 1000,
            TenantId = _tenantId,
            DeletedAt = null,
        };
        var deletedTreatment = new TreatmentEntity
        {
            Id = Guid.CreateVersion7(),
            Mills = 2000,
            TenantId = _tenantId,
            DeletedAt = DateTime.UtcNow,
        };
        context.Treatments.AddRange(activeTreatment, deletedTreatment);
        await context.SaveChangesAsync();

        var results = await context.Treatments.ToListAsync();

        Assert.Single(results);
        Assert.Equal(activeTreatment.Id, results[0].Id);
    }

    [Fact]
    public void EntryMapper_ToDomainModel_SetsIsValidFalse_WhenDeletedAtIsSet()
    {
        var entity = new EntryEntity
        {
            Id = Guid.CreateVersion7(),
            Mills = 1000,
            Mgdl = 120,
            Type = "sgv",
            IsValid = true,
            DeletedAt = DateTime.UtcNow,
        };

        var model = EntryMapper.ToDomainModel(entity);

        Assert.False(model.IsValid);
    }

    [Fact]
    public void EntryMapper_ToDomainModel_SetsIsValidTrue_WhenDeletedAtIsNull()
    {
        var entity = new EntryEntity
        {
            Id = Guid.CreateVersion7(),
            Mills = 1000,
            Mgdl = 120,
            Type = "sgv",
            IsValid = null,
            DeletedAt = null,
        };

        var model = EntryMapper.ToDomainModel(entity);

        Assert.True(model.IsValid);
    }

    [Fact]
    public void TreatmentMapper_ToDomainModel_SetsIsValidFalse_WhenDeletedAtIsSet()
    {
        var entity = new TreatmentEntity
        {
            Id = Guid.CreateVersion7(),
            Mills = 1000,
            DeletedAt = DateTime.UtcNow,
        };
        entity.Aaps.IsValid = true;

        var model = TreatmentMapper.ToDomainModel(entity);

        Assert.False(model.IsValid);
    }

    public void Dispose() => _connection.Dispose();
}
