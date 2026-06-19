using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Relativa.Authentication.Application.Interfaces;
using Relativa.Authentication.Application.Options;
using Relativa.Authentication.Application.Services;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Authentication.Endpoints;
using Relativa.Authentication.Infrastructure.Services;
using Relativa.Authentication.Infrastructure.Data;
using Relativa.Authentication.Infrastructure.Repositories;
using Relativa.Authentication.Infrastructure.Services.Audit;
using Relativa.Authentication.Infrastructure.Services;
using Relativa.Authentication.Middleware;
using Relativa.Messaging;
using Relativa.Persistence.Contracts;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File("logs/auth-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddOpenApi();

    builder.Services.AddDbContext<AuthDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
    builder.Services.Configure<RabbitMqPublishingOptions>(
        builder.Configuration.GetSection(RabbitMqPublishingOptions.ConfigurationSectionKey));

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AuthDbContext>();

    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

    builder.Services.AddSingleton<IValidateOptions<OAuthOptions>, OAuthOptionsValidator>();
    builder.Services.AddOptions<OAuthOptions>()
        .Bind(builder.Configuration.GetSection(OAuthOptions.SectionKey))
        .ValidateOnStart();

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            var config = builder.Configuration;
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = config["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = config["Jwt:Audience"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(config["Jwt:SecretKey"]!)),
                ValidateLifetime = true
            };
        });

    builder.Services.AddAuthorization();

    builder.Services.AddValidatorsFromAssemblyContaining<IAuthService>();

    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IUserSettingsRepository, UserSettingsRepository>();
    builder.Services.AddScoped<ITokenService, JwtTokenService>();
    builder.Services.AddSingleton<IExternalIdentityVerifier, OpenIdConnectIdentityVerifier>();
    builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
    builder.Services.AddScoped<IUserProvisioningService, UserProvisioningService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IUserSettingsService, UserSettingsService>();
    builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
    builder.Services.AddScoped<ITwoFactorService, TwoFactorService>();
    builder.Services.AddScoped<IEmailAddressService, EmailAddressService>();
    builder.Services.AddSingleton<ITotpProvider, OtpNetTotpProvider>();
    builder.Services.Configure<TwoFactorOptions>(builder.Configuration.GetSection(TwoFactorOptions.SectionKey));
    builder.Services.AddScoped<ISupportService, SupportService>();
    builder.Services.AddSingleton<IEmailLocalizer, JsonEmailLocalizer>();
    builder.Services.AddSingleton<IEmailRateLimiter, MemoryEmailRateLimiter>();
    builder.Services.Configure<EmailVerificationOptions>(builder.Configuration.GetSection(EmailVerificationOptions.SectionKey));
    builder.Services.Configure<SupportOptions>(builder.Configuration.GetSection(SupportOptions.SectionKey));
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
    builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
    builder.Services.Configure<SmsOptions>(builder.Configuration.GetSection(SmsOptions.SectionKey));
    if (string.Equals(builder.Configuration["Sms:Provider"], "http", StringComparison.OrdinalIgnoreCase))
    {
        builder.Services.AddScoped<ISmsSender, HttpSmsSender>();
    }
    else
    {
        builder.Services.AddScoped<ISmsSender, SmtpSmsSender>();
    }
    builder.Services.AddScoped<IOutboxWriter, OutboxWriter>();
    builder.Services.AddHostedService<AuditOutboxDispatcher>();

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    app.MapOpenApi();
    app.MapScalarApiReference();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/health");
    app.MapAuthEndpoints();
    app.MapSupportEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Authentication service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
