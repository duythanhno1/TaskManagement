using FluentValidation;
using Managerment.DTO;
using Managerment.Interfaces;

namespace Managerment.Validators
{
    public class CreateGroupValidator : AbstractValidator<CreateGroupDTO>
    {
        public CreateGroupValidator(ILocalizer l)
        {
            RuleFor(x => x.GroupName)
                .NotEmpty().WithMessage(l.Get("v.groupname_required"))
                .MaximumLength(100).WithMessage(l.Get("v.groupname_max"));

            RuleFor(x => x.MemberUserIds)
                .NotNull().WithMessage(l.Get("v.members_required"))
                .Must(m => m != null && m.Count >= 2)
                .WithMessage(l.Get("v.members_min"));
        }
    }

    public class SendMessageValidator : AbstractValidator<SendMessageDTO>
    {
        public SendMessageValidator(ILocalizer l)
        {
            RuleFor(x => x.GroupId)
                .GreaterThan(0).WithMessage(l.Get("v.groupid_positive"));

            RuleFor(x => x.Content)
                .NotEmpty().WithMessage(l.Get("v.content_required"))
                .MaximumLength(5000).WithMessage(l.Get("v.content_max"));
        }
    }

    public class ReactMessageValidator : AbstractValidator<ReactMessageDTO>
    {
        private static readonly string[] AllowedReactions = { "👍", "❤️", "😂", "😮", "😢", "😡", "🎉", "🔥" };

        public ReactMessageValidator(ILocalizer l)
        {
            RuleFor(x => x.MessageId)
                .GreaterThan(0).WithMessage(l.Get("v.messageid_positive"));

            RuleFor(x => x.ReactionType)
                .NotEmpty().WithMessage(l.Get("v.reaction_required"))
                .Must(r => AllowedReactions.Contains(r))
                .WithMessage(l.Get("v.reaction_invalid"));
        }
    }
}
