using Abp.Application.Services.Dto;
using Abp.AutoMapper;
using Capstone.Authorization.Users;

namespace Capstone.Sessions.Dto;

[AutoMapFrom(typeof(User))]
public class UserLoginInfoDto : EntityDto<long>
{
    public string Name { get; set; }

    public string Surname { get; set; }

    public string UserName { get; set; }

    public string EmailAddress { get; set; }

    public bool ShouldChangePasswordOnNextLogin { get; set; }

    public string RoleName { get; set; }

    public string RoleDisplayName { get; set; }
}
