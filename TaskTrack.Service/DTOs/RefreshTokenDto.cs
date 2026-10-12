using System.ComponentModel.DataAnnotations;

namespace TaskTrack.Service.DTOs;

public sealed class RefreshTokenDto
{
    [Required]
    [MaxLength(128)]
    public string RefreshToken { get; set; } = string.Empty;
}
