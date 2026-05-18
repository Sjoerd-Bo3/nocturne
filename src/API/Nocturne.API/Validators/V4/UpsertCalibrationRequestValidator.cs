using FluentValidation;
using Nocturne.API.Models.Requests.V4;

namespace Nocturne.API.Validators.V4;

/// <summary>
/// Validates <see cref="UpsertCalibrationRequest"/> for the V4 sensor calibration upsert endpoint.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>Timestamp must be a valid non-default <see cref="DateTimeOffset"/>.</description></item>
/// <item><description>Device, App, DataSource capped at 500 characters.</description></item>
/// </list>
/// </remarks>
/// <seealso cref="UpsertCalibrationRequest"/>
/// <seealso cref="Controllers.V4.Glucose.CalibrationController"/>
public class UpsertCalibrationRequestValidator : AbstractValidator<UpsertCalibrationRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpsertCalibrationRequestValidator"/> class
    /// and configures all validation rules for calibration upserts.
    /// </summary>
    public UpsertCalibrationRequestValidator()
    {
        RuleFor(x => x.Timestamp).NotEqual(default(DateTimeOffset)).WithMessage("Timestamp is required");
        RuleFor(x => x.Device).MaximumLength(500).When(x => x.Device is not null);
        RuleFor(x => x.App).MaximumLength(500).When(x => x.App is not null);
        RuleFor(x => x.DataSource).MaximumLength(500).When(x => x.DataSource is not null);
    }
}
