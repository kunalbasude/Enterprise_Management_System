using Microsoft.OpenApi.Models;
using System.Reflection;

namespace EnterpriseManagement.Api.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Enterprise Management API",
                Version = "v1",
                Description =
                    "Enterprise management system: users, departments, employees, projects, " +
                    "tasks and audit logs. Authenticate with POST /api/auth/login, then paste " +
                    "the returned accessToken into the Authorize dialog."
            });

            // Surfaces the XML doc comments written throughout the codebase as
            // endpoint and model descriptions in the UI.
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            // Declares the bearer scheme so the UI shows an Authorize button.
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description =
                    "JWT bearer token. Paste ONLY the token value; Swagger adds the " +
                    "'Bearer ' prefix itself."
            });

            // Applies that scheme to every operation, so the Authorize button
            // actually attaches the header to requests.
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }
}
