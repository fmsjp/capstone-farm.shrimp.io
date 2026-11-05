using Abp.Dependency;
using Abp.Domain.Repositories;
using Capstone.Domain;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Production;

public class TreatmentWithdrawal : ITransientDependency
{
    private readonly IRepository<ProductionLog, Guid> _logs;
    private readonly IRepository<ChemicalApplicationDetail, Guid> _chemical;

    public TreatmentWithdrawal(IRepository<ProductionLog, Guid> logs, IRepository<ChemicalApplicationDetail, Guid> chemical)
    {
        _logs = logs;
        _chemical = chemical;
    }

    public async Task<DateOnly?> EarliestSafeHarvestDateAsync(Guid batchId, TimeZoneInfo zone)
    {
        var rows = await (from l in _logs.GetAll()
                          join c in _chemical.GetAll() on l.Id equals c.Id
                          where l.BatchId == batchId && c.WithdrawalDays > 0
                          select new { l.ObservedAtUtc, c.EndDate, c.WithdrawalDays }).ToListAsync();
        return rows.Select(r => ChemicalApplicationDetail.SafeHarvestDate(FarmClock.LocalDate(r.ObservedAtUtc, zone), r.EndDate, r.WithdrawalDays)).Max();
    }
}
