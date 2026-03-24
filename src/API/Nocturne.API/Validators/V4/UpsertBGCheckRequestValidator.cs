using FluentValidation;
using Nocturne.API.Models.Requests.V4;

namespace Nocturne.API.Validators.V4;

public class UpsertBGCheckRequestValidator : AbstractValidator<UpsertBGCheckRequest>
{
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
