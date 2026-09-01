using System.ComponentModel.DataAnnotations;

namespace CleanArchTemplate.Infrastructure.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required(AllowEmptyStrings = false)]
    public string SmtpHost { get; set; } = "localhost";

    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 1025;

    public string? Username { get; set; }

    public string? Password { get; set; }

    /// <summary>None / Auto / StartTls / SslOnConnect. Local mail catchers need None.</summary>
    public string SecureSocketOptions { get; set; } = "None";

    [Required(AllowEmptyStrings = false)]
    [EmailAddress]
    public string FromAddress { get; set; } = "no-reply@example.com";

    public string FromDisplayName { get; set; } = "CleanArchTemplate";

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}
