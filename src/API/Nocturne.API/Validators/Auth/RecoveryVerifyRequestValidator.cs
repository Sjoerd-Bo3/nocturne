using FluentValidation;
using Nocturne.API.Controllers;

namespace Nocturne.API.Validators.Auth;

public class RecoveryVerifyRequestValidator : AbstractValidator<RecoveryVerifyRequest>
{
    public RecoveryVerifyRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
    }
}
