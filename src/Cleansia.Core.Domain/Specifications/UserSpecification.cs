using System.Linq.Expressions;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class UserSpecification : BaseSpecification<string?>, ISpecification<User>
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public int[]? UserProfiles { get; set; }

    public int[]? AuthenticationTypes { get; set; }

    public Expression<Func<User, bool>> SatisfiedBy()
    {
        Specification<User> specification = new TrueSpecification<User>();

        if (!string.IsNullOrWhiteSpace(Id))
        {
            specification &= new DirectSpecification<User>(x => x.Id == Id);
        }

        if (IsActive.HasValue)
        {
            specification &= new DirectSpecification<User>(x => x.IsActive == IsActive.Value);
        }

        if (!string.IsNullOrEmpty(FirstName))
        {
            specification &= new DirectSpecification<User>(x => x.FirstName.Contains(FirstName));
        }

        if (!string.IsNullOrEmpty(LastName))
        {
            specification &= new DirectSpecification<User>(x => x.LastName.Contains(LastName));
        }

        if (!string.IsNullOrEmpty(PhoneNumber))
        {
            specification &= new DirectSpecification<User>(x => !string.IsNullOrEmpty(x.PhoneNumber) && x.PhoneNumber.Contains(PhoneNumber));
        }

        if (!string.IsNullOrEmpty(Email))
        {
            specification &= new DirectSpecification<User>(x => x.Email.Contains(Email));
        }

        if (UserProfiles is not null && UserProfiles.Any())
        {
            specification &= new DirectSpecification<User>(x => UserProfiles.Contains((int)x.Profile));
        }

        if (AuthenticationTypes is not null && AuthenticationTypes.Any())
        {
            specification &= new DirectSpecification<User>(x => AuthenticationTypes.Contains((int)x.AuthenticationType));
        }

        return specification.SatisfiedBy();
    }

    public static UserSpecification Create(string? id = null, bool? isActive = null, string? firstName = null,
        string? lastName = null, string? phoneNumber = null, string? email = null, int[]? userProfiles = null,
        int[]? authenticationTypes = null) =>
        new()
        {
            Id = id,
            IsActive = isActive,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
            Email = email,
            UserProfiles = userProfiles,
            AuthenticationTypes = authenticationTypes
        };
}
