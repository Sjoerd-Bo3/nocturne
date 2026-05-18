using FluentValidation;
using Nocturne.API.Models.Requests.V4;

namespace Nocturne.API.Validators.V4;

/// <summary>
/// Validates <see cref="UpsertBGCheckRequest"/> for the V4 blood glucose check upsert endpoint.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>Timestamp must be a valid non-default <see cref="DateTimeOffset"/>.</description></item>
/// <item><description>Device, App, DataSource capped at 500 characters.</description></item>
/// <item><description>Glucose must be between 0 and 10,000 (mg/dL range guard).</description></item>
/// <item><description>SyncIdentifier capped at 500 characters.</description></item>
/// </list>
/// </remarks>
/// <seealso cref="UpsertBGCheckRequest"/>
/// <seealso cref="Controllers.V4.Glucose.BGCheckController"/>
public class UpsertBGCheckRequestValidator : AbstractValidator<UpsertBGCheckRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpsertBGCheckRequestValidator"/> class
    /// and configures all validation rules for BG check upserts.
    /// </summary>
    public UpsertBGCheckRequestValidator()
    {
        RuleFor(x => x.Timestamp).NotEqual(default(DateTimeOffset)).WithMessage("Timestamp is required");
        RuleFor(x => x.Device).MaximumLength(500).When(x => x.Device is not null);
        RuleFor(x => x.App).MaximumLength(500).When(x => x.App is not null);
        RuleFor(x => x.DataSource).MaximumLength(500).When(x => x.DataSource is not null);
        RuleFor(x => x.Glucose).InclusiveBetween(0, 10000).WithMessage("Glucose must be between 0 and 10000");
        RuleFor(x => x.SyncIdentifier).MaximumLength(500).When(x => x.SyncIdentifier is not null);
    }
}
