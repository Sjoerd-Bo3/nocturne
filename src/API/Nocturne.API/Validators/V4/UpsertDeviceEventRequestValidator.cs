using FluentValidation;
using Nocturne.API.Models.Requests.V4;

namespace Nocturne.API.Validators.V4;

public class UpsertDeviceEventRequestValidator : AbstractValidator<UpsertDeviceEventRequest>
{
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
