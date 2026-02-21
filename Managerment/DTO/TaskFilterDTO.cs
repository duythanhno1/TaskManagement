namespace Managerment.DTO
{
    public class TaskFilterDTO
    {
        public string? Keyword { get; set; }
        public string? Status { get; set; }
        public int? AssignedTo { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
