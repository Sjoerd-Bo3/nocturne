using Nocturne.Core.Constants;
using Nocturne.Core.Models;
using Nocturne.Core.Contracts.Repositories;

namespace Nocturne.Services.Demo.Services;

/// <summary>
/// Service for managing demo treatments in the database.
/// </summary>
public interface IDemoTreatmentService
{
    Task CreateTreatmentsAsync(
        IEnumerable<Treatment> treatments,
        CancellationToken cancellationToken = default
    );
    Task<long> DeleteAllDemoTreatmentsAsync(CancellationToken cancellationToken = default);
}

public class DemoTreatmentService : IDemoTreatmentService
{
    private readonly ITreatmentRepository _treatmentRepository;
    private readonly ILogger<DemoTreatmentService> _logger;

    public DemoTreatmentService(
        ITreatmentRepository treatmentRepository,
        ILogger<DemoTreatmentService> logger
    )
    {
        _treatmentRepository = treatmentRepository;
        _logger = logger;
    }

    public async Task CreateTreatmentsAsync(
        IEnumerable<Treatment> treatments,
        CancellationToken cancellationToken = default
    )
    {
        var treatmentList = treatments.ToList();
        if (!treatmentList.Any())
            return;

        // Ensure all treatments are tagged as demo data
        foreach (var treatment in treatmentList)
        {
            treatment.DataSource = DataSources.DemoService;
        }

        await _treatmentRepository.CreateTreatmentsAsync(treatmentList, cancellationToken);
        _logger.LogDebug("Created {Count} demo treatments", treatmentList.Count);
    }

    public async Task<long> DeleteAllDemoTreatmentsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var count = await _treatmentRepository.DeleteTreatmentsByDataSourceAsync(
            DataSources.DemoService,
            cancellationToken
        );
        _logger.LogInformation("Deleted {Count} demo treatments", count);
        return count;
    }
}
