using FluentValidation;
using Managerment.DTO;

namespace Managerment.Validators
{
    public class CreateGroupValidator : AbstractValidator<CreateGroupDTO>
    {
        public CreateGroupValidator()
        {
            RuleFor(x => x.GroupName)
                .NotEmpty().WithMessage("Group name is required.")
                .MaximumLength(100).WithMessage("Group name cannot exceed 100 characters.");

            RuleFor(x => x.MemberUserIds)
                .NotNull().WithMessage("Member list is required.")
                .Must(m => m != null && m.Count >= 2)
                .WithMessage("Group must have at least 2 members.");
        }
    }

    public class SendMessageValidator : AbstractValidator<SendMessageDTO>
    {
        public SendMessageValidator()
        {
            RuleFor(x => x.GroupId)
                .GreaterThan(0).WithMessage("Group ID must be a positive number.");

            RuleFor(x => x.Content)
                .NotEmpty().WithMessage("Message content is required.")
                .MaximumLength(5000).WithMessage("Message cannot exceed 5000 characters.");
        }
    }

    public class ReactMessageValidator : AbstractValidator<ReactMessageDTO>
    {
        private static readonly string[] AllowedReactions = { "👍", "❤️", "😂", "😮", "😢", "😡", "🎉", "🔥" };

        public ReactMessageValidator()
        {
            RuleFor(x => x.MessageId)
                .GreaterThan(0).WithMessage("Message ID must be a positive number.");

            RuleFor(x => x.ReactionType)
                .NotEmpty().WithMessage("Reaction type is required.")
                .Must(r => AllowedReactions.Contains(r))
                .WithMessage($"Reaction must be one of: {string.Join(", ", AllowedReactions)}");
        }
    }
}
