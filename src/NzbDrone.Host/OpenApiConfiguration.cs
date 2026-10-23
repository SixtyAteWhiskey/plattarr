using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace NzbDrone.Host
{
    public static class OpenApiConfiguration
    {
        public static void Configure(SwaggerGenOptions c)
        {
            c.SwaggerDoc("v3", new OpenApiInfo
            {
                Version = "3.0.0",
                Title = "Plattarr",
                Description = "Plattarr API docs (Radarr v3 compatible)",
                License = new OpenApiLicense
                {
                    Name = "GPL-3.0",
                    Url = new Uri("https://github.com/Radarr/Radarr/blob/develop/LICENSE")
                }
            });

            var apiKeyHeader = new OpenApiSecurityScheme
            {
                Name = "X-Api-Key",
                Type = SecuritySchemeType.ApiKey,
                Scheme = "apiKey",
                Description = "Apikey passed as header",
                In = ParameterLocation.Header,
            };

            c.AddSecurityDefinition("X-Api-Key", apiKeyHeader);

            c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(apiKeyHeader.Name, document)] = new List<string>(),
            });

            var apikeyQuery = new OpenApiSecurityScheme
            {
                Name = "apikey",
                Type = SecuritySchemeType.ApiKey,
                Scheme = "apiKey",
                Description = "Apikey passed as query parameter",
                In = ParameterLocation.Query,
            };

            c.AddServer(new OpenApiServer
            {
                Url = "{protocol}://{hostpath}",
                Variables = new Dictionary<string, OpenApiServerVariable>
                {
                    { "protocol", new OpenApiServerVariable { Default = "http", Enum = new List<string> { "http", "https" } } },
                    { "hostpath", new OpenApiServerVariable { Default = "localhost:7880" } }
                }
            });

            c.AddSecurityDefinition("apikey", apikeyQuery);

            c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(apikeyQuery.Name, document)] = new List<string>(),
            });

            c.CustomSchemaIds(OpenApiSchemaIds.Get);

            c.DescribeAllParametersInCamelCase();
        }
    }
}
