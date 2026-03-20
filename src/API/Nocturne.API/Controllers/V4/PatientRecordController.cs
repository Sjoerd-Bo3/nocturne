using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Attributes;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Controllers.V4;

/// <summary>
/// Controller for managing patient record data: records, devices, and insulins
/// </summary>
[ApiController]
[Route("api/v4/patient-record")]
[Authorize]
[Produces("application/json")]
[Tags("V4 Patient Record")]
public class PatientRecordController : ControllerBase
{
    private readonly IPatientRecordRepository _recordRepo;
    private readonly IPatientDeviceRepository _deviceRepo;
    private readonly IPatientInsulinRepository _insulinRepo;

    public PatientRecordController(
        IPatientRecordRepository recordRepo,
        IPatientDeviceRepository deviceRepo,
        IPatientInsulinRepository insulinRepo)
    {
        _recordRepo = recordRepo;
        _deviceRepo = deviceRepo;
        _insulinRepo = insulinRepo;
    }

    #region Patient Record

    /// <summary>
    /// Get or create the patient record
    /// </summary>
    [HttpGet]
    [RemoteQuery]
    [ProducesResponseType(typeof(PatientRecord), StatusCodes.Status200OK)]
    public async Task<ActionResult<PatientRecord>> GetPatientRecord(CancellationToken cancellationToken = default)
    {
        var record = await _recordRepo.GetOrCreateAsync(cancellationToken);
        return Ok(record);
    }

    /// <summary>
    /// Update the patient record
    /// </summary>
    [HttpPut]
    [RemoteCommand(Invalidates = ["GetPatientRecord"])]
    [ProducesResponseType(typeof(PatientRecord), StatusCodes.Status200OK)]
    public async Task<ActionResult<PatientRecord>> UpdatePatientRecord(
        [FromBody] PatientRecord model,
        CancellationToken cancellationToken = default)
    {
        var updated = await _recordRepo.UpdateAsync(model, cancellationToken);
        return Ok(updated);
    }

    #endregion

    #region Devices

    /// <summary>
    /// Get all patient devices
    /// </summary>
    [HttpGet("devices")]
    [RemoteQuery]
    [ProducesResponseType(typeof(IEnumerable<PatientDevice>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PatientDevice>>> GetDevices(CancellationToken cancellationToken = default)
    {
        var devices = await _deviceRepo.GetAllAsync(cancellationToken);
        return Ok(devices);
    }

    /// <summary>
    /// Create a new patient device
    /// </summary>
    [HttpPost("devices")]
    [RemoteCommand(Invalidates = ["GetDevices"])]
    [ProducesResponseType(typeof(PatientDevice), StatusCodes.Status201Created)]
    public async Task<ActionResult<PatientDevice>> CreateDevice(
        [FromBody] PatientDevice model,
        CancellationToken cancellationToken = default)
    {
        var created = await _deviceRepo.CreateAsync(model, cancellationToken);
        return CreatedAtAction(nameof(GetDevices), created);
    }

    /// <summary>
    /// Update a patient device
    /// </summary>
    [HttpPut("devices/{id:guid}")]
    [RemoteCommand(Invalidates = ["GetDevices"])]
    [ProducesResponseType(typeof(PatientDevice), StatusCodes.Status200OK)]
    public async Task<ActionResult<PatientDevice>> UpdateDevice(
        Guid id,
        [FromBody] PatientDevice model,
        CancellationToken cancellationToken = default)
    {
        var updated = await _deviceRepo.UpdateAsync(id, model, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Delete a patient device
    /// </summary>
    [HttpDelete("devices/{id:guid}")]
    [RemoteCommand(Invalidates = ["GetDevices"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteDevice(Guid id, CancellationToken cancellationToken = default)
    {
        await _deviceRepo.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Insulins

    /// <summary>
    /// Get all patient insulins
    /// </summary>
    [HttpGet("insulins")]
    [RemoteQuery]
    [ProducesResponseType(typeof(IEnumerable<PatientInsulin>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PatientInsulin>>> GetInsulins(CancellationToken cancellationToken = default)
    {
        var insulins = await _insulinRepo.GetAllAsync(cancellationToken);
        return Ok(insulins);
    }

    /// <summary>
    /// Create a new patient insulin
    /// </summary>
    [HttpPost("insulins")]
    [RemoteCommand(Invalidates = ["GetInsulins"])]
    [ProducesResponseType(typeof(PatientInsulin), StatusCodes.Status201Created)]
    public async Task<ActionResult<PatientInsulin>> CreateInsulin(
        [FromBody] PatientInsulin model,
        CancellationToken cancellationToken = default)
    {
        var created = await _insulinRepo.CreateAsync(model, cancellationToken);
        return CreatedAtAction(nameof(GetInsulins), created);
    }

    /// <summary>
    /// Update a patient insulin
    /// </summary>
    [HttpPut("insulins/{id:guid}")]
    [RemoteCommand(Invalidates = ["GetInsulins"])]
    [ProducesResponseType(typeof(PatientInsulin), StatusCodes.Status200OK)]
    public async Task<ActionResult<PatientInsulin>> UpdateInsulin(
        Guid id,
        [FromBody] PatientInsulin model,
        CancellationToken cancellationToken = default)
    {
        var updated = await _insulinRepo.UpdateAsync(id, model, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Delete a patient insulin
    /// </summary>
    [HttpDelete("insulins/{id:guid}")]
    [RemoteCommand(Invalidates = ["GetInsulins"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteInsulin(Guid id, CancellationToken cancellationToken = default)
    {
        await _insulinRepo.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    #endregion
}
