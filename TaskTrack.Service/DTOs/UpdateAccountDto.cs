using System.ComponentModel.DataAnnotations;

namespace TaskTrack.Service.DTOs;

public sealed class UpdateAccountDto : IValidatableObject
{
    [MaxLength(100)]
    public string? FullName { get; set; }

    [Range(0, 1)]
    public short? Role { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FullName is null && Role is null)
            yield return new ValidationResult("FullName or Role is required.");
        if (FullName is not null && string.IsNullOrWhiteSpace(FullName))
            yield return new ValidationResult("FullName must not be empty.", new[] { nameof(FullName) });
    }
}
