using FluentValidation;
using Nocturne.API.Models.Requests.V4;

namespace Nocturne.API.Validators.V4;

/// <summary>
/// Validates <see cref="UpsertDeviceEventRequest"/> for the V4 device event upsert endpoint.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>Timestamp must be a valid non-default <see cref="DateTimeOffset"/>.</description></item>
/// <item><description>Device, App, DataSource capped at 500 characters.</description></item>
/// <item><description>EventType must be a valid <see cref="Core.Models.DeviceEventType"/> enum value.</description></item>
/// <item><description>Notes capped at 10,000 characters; SyncIdentifier at 500.</description></item>
/// </list>
/// </remarks>
/// <seealso cref="UpsertDeviceEventRequest"/>
/// <seealso cref="Controllers.V4.Devices.DeviceEventController"/>
public class UpsertDeviceEventRequestValidator : AbstractValidator<UpsertDeviceEventRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpsertDeviceEventRequestValidator"/> class
    /// and configures all validation rules for device event upserts.
    /// </summary>
    public UpsertDeviceEventRequestValidator()
    {
        RuleFor(x => x.Timestamp).NotEqual(default(DateTimeOffset)).WithMessage("Timestamp is required");
        RuleFor(x => x.Device).MaximumLength(500).When(x => x.Device is not null);
        RuleFor(x => x.App).MaximumLength(500).When(x => x.App is not null);
        RuleFor(x => x.DataSource).MaximumLength(500).When(x => x.DataSource is not null);
        RuleFor(x => x.EventType).IsInEnum().WithMessage("EventType must be a valid device event type");
        RuleFor(x => x.Notes).MaximumLength(10000).When(x => x.Notes is not null);
        RuleFor(x => x.SyncIdentifier).MaximumLength(500).When(x => x.SyncIdentifier is not null);
    }
}
