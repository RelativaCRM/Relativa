using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Core.Endpoints;
using Relativa.Core.Infrastructure.Data;
using Relativa.Core.Infrastructure.Repositories;
using Relativa.Core.Middleware;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File("logs/core-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddOpenApi();

    builder.Services.AddDbContext<RelativaDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<RelativaDbContext>();

    builder.Services.AddValidatorsFromAssemblyContaining<IWorkspaceService>();

    builder.Services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
    builder.Services.AddScoped<IUserRoleWorkspaceRepository, UserRoleWorkspaceRepository>();
    builder.Services.AddScoped<IWorkspaceInvitationRepository, WorkspaceInvitationRepository>();
    builder.Services.AddScoped<IWorkspaceRoleRepository, WorkspaceRoleRepository>();
    builder.Services.AddScoped<IPermissionRepository, PermissionRepository>();
    builder.Services.AddScoped<IUserRoleOrganizationRepository, UserRoleOrganizationRepository>();
    builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
    builder.Services.AddScoped<IOrganizationRoleRepository, OrganizationRoleRepository>();
    builder.Services.AddScoped<IJoinRequestRepository, JoinRequestRepository>();
    builder.Services.AddScoped<IOrgInvitationRepository, OrgInvitationRepository>();
    builder.Services.AddScoped<IEntityTypeRepository, EntityTypeRepository>();
    builder.Services.AddScoped<IEntityRepository, EntityRepository>();

    builder.Services.AddScoped<IEntityTypeService, EntityTypeService>();
    builder.Services.AddScoped<IEntityService, EntityService>();

    builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();
    builder.Services.AddScoped<IWorkspaceMemberService, WorkspaceMemberService>();
    builder.Services.AddScoped<IInvitationService, InvitationService>();
    builder.Services.AddScoped<IRoleService, RoleService>();
    builder.Services.AddScoped<IOrganizationService, OrganizationService>();
    builder.Services.AddScoped<IOrgRoleService, OrgRoleService>();
    builder.Services.AddScoped<IOrgInvitationService, OrgInvitationService>();
    builder.Services.AddScoped<IJoinRequestService, JoinRequestService>();

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    app.MapOpenApi();
    app.MapScalarApiReference();

    app.MapHealthChecks("/health");
    app.MapEntityTypeEndpoints();
    app.MapEntityEndpoints();
    app.MapWorkspaceEndpoints();
    app.MapMemberEndpoints();
    app.MapInvitationEndpoints();
    app.MapRoleEndpoints();
    app.MapOrganizationEndpoints();
    app.MapOrgMemberEndpoints();
    app.MapJoinRequestEndpoints();
    app.MapOrgInvitationEndpoints();
    app.MapOrgRoleEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Core service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
