using FluentValidation;
using Managerment.DTO;
using Managerment.Interfaces;

namespace Managerment.Validators
{
    public class TaskCreateValidator : AbstractValidator<TaskCreateDTO>
    {
        public TaskCreateValidator(ILocalizer l)
        {
            RuleFor(x => x.TaskName)
                .NotEmpty().WithMessage(l.Get("v.taskname_required"))
                .MaximumLength(200).WithMessage(l.Get("v.taskname_max"));

            RuleFor(x => x.Description)
                .MaximumLength(1000).WithMessage(l.Get("v.description_max"));

            RuleFor(x => x.AssignedTo)
                .GreaterThan(0).When(x => x.AssignedTo.HasValue)
                .WithMessage(l.Get("v.assigned_positive"));
        }
    }

    public class TaskUpdateValidator : AbstractValidator<TaskUpdateDTO>
    {
        private static readonly string[] ValidStatuses = { "Todo", "InProgress", "Done" };

        public TaskUpdateValidator(ILocalizer l)
        {
            RuleFor(x => x.TaskId)
                .GreaterThan(0).WithMessage(l.Get("v.taskid_positive"));

            RuleFor(x => x.TaskName)
                .MaximumLength(200).When(x => x.TaskName != null)
                .WithMessage(l.Get("v.taskname_max"));

            RuleFor(x => x.Status)
                .Must(s => ValidStatuses.Contains(s))
                .When(x => x.Status != null)
                .WithMessage(l.Get("v.status_invalid"));

            RuleFor(x => x.AssignedTo)
                .GreaterThan(0).When(x => x.AssignedTo.HasValue)
                .WithMessage(l.Get("v.assigned_positive"));
        }
    }

    public class TaskAssignValidator : AbstractValidator<TaskAssignDTO>
    {
        public TaskAssignValidator(ILocalizer l)
        {
            RuleFor(x => x.TaskId)
                .GreaterThan(0).WithMessage(l.Get("v.taskid_positive"));

            RuleFor(x => x.NewAssignedToUserId)
                .GreaterThan(0).WithMessage(l.Get("v.assigned_positive"));
        }
    }
}
