using FluentValidation;
using Managerment.DTO;

namespace Managerment.Validators
{
    public class TaskCreateValidator : AbstractValidator<TaskCreateDTO>
    {
        public TaskCreateValidator()
        {
            RuleFor(x => x.TaskName)
                .NotEmpty().WithMessage("Task name is required.")
                .MaximumLength(200).WithMessage("Task name cannot exceed 200 characters.");

            RuleFor(x => x.Description)
                .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters.");

            RuleFor(x => x.AssignedTo)
                .GreaterThan(0).When(x => x.AssignedTo.HasValue)
                .WithMessage("Assigned user ID must be a positive number.");
        }
    }

    public class TaskUpdateValidator : AbstractValidator<TaskUpdateDTO>
    {
        private static readonly string[] ValidStatuses = { "Todo", "InProgress", "Done" };

        public TaskUpdateValidator()
        {
            RuleFor(x => x.TaskId)
                .GreaterThan(0).WithMessage("Task ID must be a positive number.");

            RuleFor(x => x.TaskName)
                .MaximumLength(200).When(x => x.TaskName != null)
                .WithMessage("Task name cannot exceed 200 characters.");

            RuleFor(x => x.Status)
                .Must(s => ValidStatuses.Contains(s))
                .When(x => x.Status != null)
                .WithMessage("Status must be one of: Todo, InProgress, Done.");

            RuleFor(x => x.AssignedTo)
                .GreaterThan(0).When(x => x.AssignedTo.HasValue)
                .WithMessage("Assigned user ID must be a positive number.");
        }
    }

    public class TaskAssignValidator : AbstractValidator<TaskAssignDTO>
    {
        public TaskAssignValidator()
        {
            RuleFor(x => x.TaskId)
                .GreaterThan(0).WithMessage("Task ID must be a positive number.");

            RuleFor(x => x.NewAssignedToUserId)
                .GreaterThan(0).WithMessage("New assigned user ID must be a positive number.");
        }
    }
}
