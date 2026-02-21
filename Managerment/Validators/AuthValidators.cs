using FluentValidation;
using Managerment.DTO;
using Managerment.Interfaces;

namespace Managerment.Validators
{
    public class RegisterValidator : AbstractValidator<RegisterDTO>
    {
        public RegisterValidator(ILocalizer l)
        {
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage(l.Get("v.fullname_required"))
                .Length(2, 100).WithMessage(l.Get("v.fullname_length"));

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage(l.Get("v.email_required"))
                .EmailAddress().WithMessage(l.Get("v.email_invalid"));

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage(l.Get("v.password_required"))
                .MinimumLength(8).WithMessage(l.Get("v.password_min_length"))
                .Matches(@"[A-Z]").WithMessage(l.Get("v.password_uppercase"))
                .Matches(@"[0-9]").WithMessage(l.Get("v.password_number"))
                .Matches(@"[^a-zA-Z0-9]").WithMessage(l.Get("v.password_special"));

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage(l.Get("v.phone_required"))
                .Matches(@"^[\+]?[0-9]{9,15}$").WithMessage(l.Get("v.phone_invalid"));
        }
    }

    public class LoginValidator : AbstractValidator<LoginDTO>
    {
        public LoginValidator(ILocalizer l)
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage(l.Get("v.email_required"))
                .EmailAddress().WithMessage(l.Get("v.email_invalid"));

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage(l.Get("v.password_required"));
        }
    }
}
