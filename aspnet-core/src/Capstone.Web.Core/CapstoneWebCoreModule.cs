using Abp.AspNetCore;
using Abp.AspNetCore.Configuration;
using Abp.AspNetCore.SignalR;
using Abp.Modules;
using Abp.Reflection.Extensions;
using Abp.Zero.Configuration;
using Capstone.Authentication.JwtBearer;
using Capstone.Configuration;
using Capstone.EntityFrameworkCore;
using Capstone.EntityFrameworkCore.Seed;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IO;

namespace Capstone
{
    [DependsOn(
         typeof(CapstoneApplicationModule),
         typeof(CapstoneEntityFrameworkModule),
         typeof(AbpAspNetCoreModule)
        , typeof(AbpAspNetCoreSignalRModule)
     )]
    public class CapstoneWebCoreModule : AbpModule
    {
        private readonly IConfigurationRoot _appConfiguration;

        public CapstoneWebCoreModule(IWebHostEnvironment env)
        {
            _appConfiguration = env.GetAppConfiguration();
        }

        public override void PreInitialize()
        {
            Configuration.DefaultNameOrConnectionString = _appConfiguration.GetConnectionString(
                CapstoneConsts.ConnectionStringName
            );

            Configuration.Modules.Zero().LanguageManagement.EnableDbLocalization();

            Configuration.Modules.AbpAspNetCore()
                 .CreateControllersForAppServices(
                     typeof(CapstoneApplicationModule).GetAssembly()
                 );

            ConfigureSeed();
            ConfigureTokenAuth();
        }

        private void ConfigureSeed()
        {
            SeedOptions.SystemAdminInitialPassword = _appConfiguration["SystemAdmin:InitialPassword"];
        }

        private void ConfigureTokenAuth()
        {
            IocManager.Register<TokenAuthConfiguration>();
            var tokenAuthConfig = IocManager.Resolve<TokenAuthConfiguration>();

            tokenAuthConfig.SecurityKey = JwtSecurityKey.Create(_appConfiguration);
            tokenAuthConfig.Issuer = _appConfiguration["Authentication:JwtBearer:Issuer"];
            tokenAuthConfig.Audience = _appConfiguration["Authentication:JwtBearer:Audience"];
            tokenAuthConfig.SigningCredentials = new SigningCredentials(tokenAuthConfig.SecurityKey, SecurityAlgorithms.HmacSha256);
            var minutes = _appConfiguration.GetValue<int?>("Authentication:JwtBearer:ExpirationMinutes") ?? 240;
            tokenAuthConfig.Expiration = TimeSpan.FromMinutes(minutes);
        }

        public override void Initialize()
        {
            IocManager.RegisterAssemblyByConvention(typeof(CapstoneWebCoreModule).GetAssembly());
        }

        public override void PostInitialize()
        {
            IocManager.Resolve<ApplicationPartManager>()
                .AddApplicationPartsIfNotAddedBefore(typeof(CapstoneWebCoreModule).Assembly);
        }
    }
}
