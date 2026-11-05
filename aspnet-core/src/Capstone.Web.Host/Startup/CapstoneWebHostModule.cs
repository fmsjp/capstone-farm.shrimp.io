using Abp.Modules;
using Abp.Reflection.Extensions;
using Capstone.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Capstone.Web.Host.Startup
{
    [DependsOn(
       typeof(CapstoneWebCoreModule))]
    public class CapstoneWebHostModule : AbpModule
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfigurationRoot _appConfiguration;

        public CapstoneWebHostModule(IWebHostEnvironment env)
        {
            _env = env;
            _appConfiguration = env.GetAppConfiguration();
        }

        public override void Initialize()
        {
            IocManager.RegisterAssemblyByConvention(typeof(CapstoneWebHostModule).GetAssembly());
        }
    }
}
