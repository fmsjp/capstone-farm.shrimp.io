using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Text;

namespace Capstone.Authentication.JwtBearer
{
    public static class JwtSecurityKey
    {
        public const int MinLength = 32;

        public static SymmetricSecurityKey Create(IConfiguration configuration)
        {
            var key = configuration["Authentication:JwtBearer:SecurityKey"];
            if (string.IsNullOrWhiteSpace(key) || key.Length < MinLength)
            {
                throw new InvalidOperationException(
                    $"Authentication:JwtBearer:SecurityKey must be set ({MinLength}+ chars) via user-secrets or environment variable.");
            }

            return new SymmetricSecurityKey(Encoding.ASCII.GetBytes(key));
        }
    }
}
