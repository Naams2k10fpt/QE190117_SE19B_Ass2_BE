namespace TaskTrack.Service.DTOs;

public sealed record TaskTagDto(
    int TagId,
    string TagName,
    string? Color);

public sealed record TaskDetailDto(
    int TaskId,
    string Title,
    string? Description,
    short Status,
    short Priority,
    DateOnly? DueDate,
    int ProjectId,
    string ProjectName,
    bool IsActive,
    DateTime CreatedDate,
    DateTime? ModifiedDate,
    List<TaskTagDto> Tags,
    int? CreatedById,
    int? UpdatedById);
