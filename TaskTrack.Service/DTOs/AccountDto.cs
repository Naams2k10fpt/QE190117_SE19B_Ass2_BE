namespace TaskTrack.Service.DTOs;

public sealed record AccountDto(
    int AccountId,
    string FullName,
    string Email,
    short Role,
    DateTime CreatedDate);