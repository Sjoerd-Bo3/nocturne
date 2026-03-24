using FluentValidation;
using Nocturne.API.Models.Requests.V4;

namespace Nocturne.API.Validators.V4;

public class UpsertNoteRequestValidator : AbstractValidator<UpsertNoteRequest>
{
    public UpsertNoteRequestValidator()
    {
        RuleFor(x => x.Timestamp).NotEqual(default(DateTimeOffset)).WithMessage("Timestamp is required");
        RuleFor(x => x.Device).MaximumLength(500).When(x => x.Device is not null);
        RuleFor(x => x.App).MaximumLength(500).When(x => x.App is not null);
        RuleFor(x => x.DataSource).MaximumLength(500).When(x => x.DataSource is not null);
        RuleFor(x => x.Text).MaximumLength(10000).When(x => x.Text is not null);
        RuleFor(x => x.EventType).MaximumLength(200).When(x => x.EventType is not null);
        RuleFor(x => x.SyncIdentifier).MaximumLength(500).When(x => x.SyncIdentifier is not null);
    }
}
