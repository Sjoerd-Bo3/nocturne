using Microsoft.EntityFrameworkCore;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Mappers.V4;

namespace Nocturne.Infrastructure.Data.Repositories.V4;

/// <summary>
/// Repository for managing device records in the database.
/// </summary>
public class DeviceRepository : IDeviceRepository
{
    private readonly NocturneDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public DeviceRepository(NocturneDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets a device by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The device, or null if not found.</returns>
    public async Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.Devices.FindAsync([id], ct);
        return entity is null ? null : DeviceMapper.ToDomainModel(entity);
    }

    /// <summary>
    /// Finds a device by its category, type, and serial number.
    /// </summary>
    /// <param name="category">The device category.</param>
    /// <param name="type">The device type.</param>
    /// <param name="serial">The device serial number.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The matching device, or null if not found.</returns>
    public async Task<Device?> FindByCategoryTypeAndSerialAsync(DeviceCategory category, string type, string serial, CancellationToken ct = default)
    {
        var categoryStr = category.ToString();
        var entity = await _context.Devices
            .FirstOrDefaultAsync(e => e.Category == categoryStr && e.Type == type && e.Serial == serial, ct);
        return entity is null ? null : DeviceMapper.ToDomainModel(entity);
    }

    /// <summary>
    /// Creates a new device record.
    /// </summary>
    /// <param name="model">The device to create.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The created device.</returns>
    public async Task<Device> CreateAsync(Device model, CancellationToken ct = default)
    {
        var entity = DeviceMapper.ToEntity(model);
        _context.Devices.Add(entity);
        await _context.SaveChangesAsync(ct);
        return DeviceMapper.ToDomainModel(entity);
    }

    /// <summary>
    /// Updates an existing device record.
    /// </summary>
    /// <param name="id">The unique identifier of the device to update.</param>
    /// <param name="model">The updated device data.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated device.</returns>
    public async Task<Device> UpdateAsync(Guid id, Device model, CancellationToken ct = default)
    {
        var entity =
            await _context.Devices.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"Device {id} not found");
        DeviceMapper.UpdateEntity(entity, model);
        await _context.SaveChangesAsync(ct);
        return DeviceMapper.ToDomainModel(entity);
    }
}
