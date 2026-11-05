using Abp.Dependency;
using System;

namespace Capstone.Timing;

public class AppTimes : ISingletonDependency
{
    public DateTime StartupTime { get; set; }
}
