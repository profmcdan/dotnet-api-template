namespace CleanArchTemplate.Domain.Users;

public enum UserStatus
{
    /// <summary>Created by an invitation; cannot authenticate until the invitation is accepted.</summary>
    Invited = 0,

    /// <summary>Has credentials and may authenticate.</summary>
    Active = 1,

    /// <summary>Administratively disabled; retained for audit but cannot authenticate.</summary>
    Suspended = 2,
}
