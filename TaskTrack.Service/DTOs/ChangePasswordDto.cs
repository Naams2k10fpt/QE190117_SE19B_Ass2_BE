using System.ComponentModel.DataAnnotations;

namespace TaskTrack.Service.DTOs;

public sealed class ChangePasswordDto
{
    [Required, MaxLength(128)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}
