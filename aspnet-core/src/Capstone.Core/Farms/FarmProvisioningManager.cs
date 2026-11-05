using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Domain.Services;
using Abp.IdentityFramework;
using Abp.MultiTenancy;
using Abp.UI;
using Capstone.Harvests;
using Capstone.Ponds;
using Capstone.Authorization.Roles;
using Capstone.Authorization.Users;
using Capstone.Compliance;
using Capstone.Domain;
using Capstone.Editions;
using Capstone.MultiTenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Capstone.Farms;

public class FarmRegistration
{
    public string FarmCode { get; set; }
    public string FarmName { get; set; }
    public string AddressLine1 { get; set; }
    public string AddressLine2 { get; set; }
    public string CityMunicipality { get; set; }
    public string Province { get; set; }
    public string PostalCode { get; set; }
    public string Country { get; set; }
    public decimal? TotalAreaHectares { get; set; }
    public string ContactName { get; set; }
    public string ContactPhone { get; set; }
    public string ContactEmail { get; set; }

    public string OwnerName { get; set; }
    public string OwnerSurname { get; set; }
    public string OwnerUserName { get; set; }
    public string OwnerEmailAddress { get; set; }
    public string OwnerPassword { get; set; }
}

public class FarmProvisioningManager : DomainService
{
    public static readonly Regex FarmCodeRegex = new("^[a-zA-Z][a-zA-Z0-9_-]{1,63}$");

    private readonly TenantManager _tenantManager;
    private readonly EditionManager _editionManager;
    private readonly RoleManager _roleManager;
    private readonly UserManager _userManager;
    private readonly IPermissionManager _permissionManager;
    private readonly IRepository<FarmProfile, int> _profiles;
    private readonly IRepository<DocumentCategory, Guid> _documentCategories;
    private readonly IRepository<DocumentPreset, Guid> _documentPresets;
    private readonly IRepository<PondNamePrefix, Guid> _pondPrefixes;
    private readonly IRepository<SizeGrade, Guid> _sizeGrades;

    public FarmProvisioningManager(
        TenantManager tenantManager,
        EditionManager editionManager,
        RoleManager roleManager,
        UserManager userManager,
        IPermissionManager permissionManager,
        IRepository<FarmProfile, int> profiles,
        IRepository<DocumentCategory, Guid> documentCategories,
        IRepository<DocumentPreset, Guid> documentPresets,
        IRepository<PondNamePrefix, Guid> pondPrefixes,
        IRepository<SizeGrade, Guid> sizeGrades)
    {
        _tenantManager = tenantManager;
        _editionManager = editionManager;
        _roleManager = roleManager;
        _userManager = userManager;
        _permissionManager = permissionManager;
        _profiles = profiles;
        _documentCategories = documentCategories;
        _documentPresets = documentPresets;
        _pondPrefixes = pondPrefixes;
        _sizeGrades = sizeGrades;
    }

    public async Task<(Tenant Tenant, User Owner)> CreateFarmAsync(FarmRegistration input)
    {
        var code = (input.FarmCode ?? string.Empty).Trim();
        if (!FarmCodeRegex.IsMatch(code))
        {
            throw new UserFriendlyException("Farm code must start with a letter and use 2–64 letters, digits, '-' or '_'.");
        }

        if (await _tenantManager.FindByTenancyNameAsync(code) != null)
        {
            throw new UserFriendlyException($"The farm code '{code}' is already taken.");
        }

        FarmAddress.Validate(input.PostalCode, input.Country);

        var tenant = new Tenant(code, input.FarmName.Trim()) { IsActive = true };
        var defaultEdition = await _editionManager.FindByNameAsync(EditionManager.DefaultEditionName);
        if (defaultEdition != null)
        {
            tenant.EditionId = defaultEdition.Id;
        }

        try
        {
            await _tenantManager.CreateAsync(tenant);
            await CurrentUnitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (DuplicateKey.Is(ex))
        {
            throw new UserFriendlyException($"The farm code '{code}' was just taken by another farm. Choose a different code.");
        }

        User owner;
        using (CurrentUnitOfWork.SetTenantId(tenant.Id))
        {
            CheckErrors(await _roleManager.CreateStaticRoles(tenant.Id));
            await CurrentUnitOfWork.SaveChangesAsync();
            await GrantStaticRolePermissionsAsync();

            await _profiles.InsertAsync(new FarmProfile
            {
                Id = tenant.Id,
                TenantId = tenant.Id,
                FarmName = input.FarmName.Trim(),
                AddressLine1 = FarmAddress.Clean(input.AddressLine1),
                AddressLine2 = FarmAddress.Clean(input.AddressLine2),
                CityMunicipality = FarmAddress.Clean(input.CityMunicipality),
                Province = FarmAddress.Clean(input.Province),
                PostalCode = FarmAddress.Clean(input.PostalCode),
                Country = FarmAddress.Clean(input.Country) ?? FarmProfile.DefaultCountry,
                TotalAreaHectares = input.TotalAreaHectares,
                ContactName = input.ContactName,
                ContactPhone = input.ContactPhone,
                ContactEmail = input.ContactEmail,
                TimeZone = FarmClock.DefaultTimeZone,
                DocumentExpiryAlertDays = 30,
            });

            foreach (var c in FarmDefaults.DocumentCategories)
            {
                var categoryId = await _documentCategories.InsertAndGetIdAsync(new DocumentCategory
                {
                    TenantId = tenant.Id, Name = c.Name, RequiresExpiryDate = c.RequiresExpiryDate,
                });
                var order = 0;
                foreach (var p in c.Presets)
                {
                    await _documentPresets.InsertAsync(new DocumentPreset
                    {
                        TenantId = tenant.Id, CategoryId = categoryId, Name = p.Name, DefaultIssuingAgency = p.DefaultIssuingAgency,
                        IsRequired = p.IsRequired, SortOrder = ++order,
                    });
                }
            }

            var prefixOrder = 0;
            foreach (var x in FarmDefaults.PondNamePrefixes)
            {
                await _pondPrefixes.InsertAsync(new PondNamePrefix { TenantId = tenant.Id, Name = x.Name, ShortCode = x.ShortCode, SortOrder = ++prefixOrder });
            }

            var gradeOrder = 0;
            foreach (var g in FarmDefaults.SizeGrades)
            {
                await _sizeGrades.InsertAsync(new SizeGrade { TenantId = tenant.Id, Label = g.Label, MinCountPerKg = g.MinCountPerKg, MaxCountPerKg = g.MaxCountPerKg, SortOrder = ++gradeOrder });
            }

            await _sizeGrades.InsertAsync(new SizeGrade { TenantId = tenant.Id, Label = SizeGrade.UngradedLabel, SortOrder = ++gradeOrder });

            owner = await CreateFarmUserAsync(tenant.Id, input.OwnerName, input.OwnerSurname, input.OwnerUserName,
                input.OwnerEmailAddress, input.OwnerPassword, StaticRoleNames.Tenants.FarmOwner, mustChangePassword: false);
        }

        return (tenant, owner);
    }

    public async Task<User> CreateFarmUserAsync(int tenantId, string name, string surname, string userName, string email,
        string password, string roleName, bool mustChangePassword)
    {
        EnsureFarmRole(roleName);
        var user = new User
        {
            TenantId = tenantId,
            Name = name?.Trim(),
            Surname = surname?.Trim(),
            UserName = userName?.Trim(),
            EmailAddress = email?.Trim(),
            IsActive = true,
            IsEmailConfirmed = true,
            ShouldChangePasswordOnNextLogin = mustChangePassword,
            IsLockoutEnabled = true,
        };
        user.SetNormalizedNames();

        await _userManager.InitializeOptionsAsync(tenantId);
        try
        {
            CheckErrors(await _userManager.CreateAsync(user, password));
            await CurrentUnitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (DuplicateKey.Is(ex))
        {
            throw new UserFriendlyException($"The username '{user.UserName}' was just taken in this farm. Choose a different username.");
        }
        CheckErrors(await _userManager.SetRolesAsync(user, new[] { roleName }));
        await CurrentUnitOfWork.SaveChangesAsync();
        return user;
    }

    public async Task GrantStaticRolePermissionsAsync()
    {
        foreach (var roleName in new[] { StaticRoleNames.Tenants.FarmOwner, StaticRoleNames.Tenants.FarmTechnician })
        {
            var role = _roleManager.Roles.Single(r => r.Name == roleName);
            var permissions = FarmRolePermissions.For(roleName)
                .Select(n => _permissionManager.GetPermissionOrNull(n))
                .Where(p => p != null)
                .ToList();
            await _roleManager.SetGrantedPermissionsAsync(role, permissions);
        }
    }

    public static void EnsureFarmRole(string roleName)
    {
        if (roleName != StaticRoleNames.Tenants.FarmOwner && roleName != StaticRoleNames.Tenants.FarmTechnician)
        {
            throw new UserFriendlyException("Role must be Farm Owner or Farm Technician.");
        }
    }

    private void CheckErrors(IdentityResult result) => result.CheckErrors(LocalizationManager);
}
