using System;

namespace TaskTrack.Repo.Models;

public partial class RefreshToken
{
    public int RefreshTokenId { get; set; }

    public int AccountId { get; set; }

    public Guid SessionId { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTime CreatedDate { get; set; }

    public DateTime ExpiresDate { get; set; }

    public DateTime? RevokedDate { get; set; }

    public virtual SystemAccount Account { get; set; } = null!;
}