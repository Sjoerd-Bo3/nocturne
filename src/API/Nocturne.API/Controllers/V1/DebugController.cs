using Microsoft.AspNetCore.Mvc;
using Nocturne.Core.Models;
using Nocturne.Core.Contracts.Repositories;

namespace Nocturne.API.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
public class DebugController : ControllerBase
{
    private readonly IEntryRepository _entryRepository;
    private readonly ILogger<DebugController> _logger;

    public DebugController(IEntryRepository entryRepository, ILogger<DebugController> logger)
    {
        _entryRepository = entryRepository;
        _logger = logger;
    }

    [HttpGet("postgresql-test")]
    public async Task<IActionResult> TestPostgreSqlConnection()
    {
        try
        {
            _logger.LogInformation("Testing PostgreSQL connection");

            // Count documents
            var count = await _entryRepository.CountEntriesAsync();
            _logger.LogInformation("Entries table document count: {Count}", count);

            // Try to get recent entries
            var entries = await _entryRepository.GetEntriesAsync("sgv", 1, 0);
            var firstEntry = entries.FirstOrDefault();

            return Ok(
                new
                {
                    DatabaseType = "PostgreSQL",
                    TableName = "entries",
                    DocumentCount = count,
                    SampleEntry = firstEntry,
                    Status = "Success",
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing PostgreSQL: {Message}", ex.Message);
            return StatusCode(
                500,
                new
                {
                    Error = ex.Message,
                    InnerError = ex.InnerException?.Message,
                    Status = "Failed",
                }
            );
        }
    }

    [HttpGet("entries-direct")]
    public async Task<IActionResult> GetEntriesDirect()
    {
        try
        {
            var entries = await _entryRepository.GetEntriesAsync("sgv", 5, 0);
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting entries directly");
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}
