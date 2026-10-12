using System.ComponentModel.DataAnnotations;

namespace TaskTrack.Service.DTOs;

public sealed class UpdateProfileDto
{
    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;
}
