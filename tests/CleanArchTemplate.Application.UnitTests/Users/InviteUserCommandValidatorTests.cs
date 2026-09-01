using CleanArchTemplate.Application.Users.Commands;
using CleanArchTemplate.Domain.Users;

namespace CleanArchTemplate.Application.UnitTests.Users;

public sealed class InviteUserCommandValidatorTests
{
    private readonly InviteUserCommandValidator _validator = new();

    [Fact]
    public void Accepts_a_well_formed_command() =>
        _validator.Validate(new InviteUserCommand("a@b.com", "Ada", [UserRoles.Member])).IsValid.ShouldBeTrue();

    [Fact]
    public void Rejects_an_empty_role_list() =>
        _validator.Validate(new InviteUserCommand("a@b.com", "Ada", [])).IsValid.ShouldBeFalse();

    [Fact]
    public void Rejects_an_unknown_role() =>
        _validator.Validate(new InviteUserCommand("a@b.com", "Ada", ["root"])).IsValid.ShouldBeFalse();

    [Fact]
    public void Rejects_a_blank_name() =>
        _validator.Validate(new InviteUserCommand("a@b.com", "   ", [UserRoles.Member])).IsValid.ShouldBeFalse();

    [Fact]
    public void Rejects_a_name_over_the_length_limit() =>
        _validator.Validate(new InviteUserCommand("a@b.com", new string('x', User.MaxNameLength + 1), [UserRoles.Member]))
            .IsValid.ShouldBeFalse();
}
