using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Runtime.Session;
using Capstone.Domain;
using System;
using System.Threading.Tasks;

namespace Capstone.Farms;

public class FarmContext : ITransientDependency
{
    private readonly IRepository<FarmProfile, int> _profiles;
    private readonly IAbpSession _session;
    private FarmProfile _profile;
    private TimeZoneInfo _zone;

    public FarmContext(IRepository<FarmProfile, int> profiles, IAbpSession session)
    {
        _profiles = profiles;
        _session = session;
    }

    public int TenantId => _session.TenantId ?? throw FpmsErrors.Rule("This action is available only inside a farm account.");

    public async Task<FarmProfile> GetProfileAsync()
    {
        if (_profile == null)
        {
            var tenantId = TenantId;
            _profile = await _profiles.FirstOrDefaultAsync(p => p.Id == tenantId)
                       ?? new FarmProfile { Id = tenantId, TenantId = tenantId, FarmName = string.Empty };
        }

        return _profile;
    }

    public async Task<TimeZoneInfo> GetTimeZoneAsync()
    {
        if (_zone == null)
        {
            _zone = FarmClock.Resolve((await GetProfileAsync()).TimeZone);
        }

        return _zone;
    }

    public async Task<DateOnly> GetTodayAsync() => FarmClock.Today(await GetTimeZoneAsync());

    public async Task<DateOnly> ToLocalDateAsync(DateTime utc) => FarmClock.LocalDate(utc, await GetTimeZoneAsync());
}
