#if (useShellFeatures)
using CShells.AspNetCore.Configuration;
using CShells.AspNetCore.Extensions;
using CShells.DependencyInjection;
using Elsa.ShellFeatures;
using Elsa.Workflows.Api.ShellFeatures;
using Elsa.Workflows.Management.ShellFeatures;
using Elsa.Workflows.Runtime.Distributed.ShellFeatures;
using Elsa.Workflows.Runtime.ShellFeatures;
using Elsa.Workflows.ShellFeatures;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.AddShells(shells => shells
    .WithHostAssemblies()
    .WithConfigurationProvider(configuration)
    .WithWebRouting(options => options.EnablePathRouting = true)
    .WithAuthenticationAndAuthorization()
    .ConfigureAllShells(shell =>
    {
        shell.WithFeatures(
            typeof(ElsaFeature),
            typeof(WorkflowManagementFeature),
            typeof(WorkflowRuntimeFeature),
            typeof(WorkflowsFeature),
            typeof(DistributedRuntimeFeature),
            typeof(WorkflowsApiFeature));
    }));

builder.Services.AddHealthChecks();
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.MapHealthChecks("/");
app.MapShells();
app.UseAuthentication();
app.UseAuthorization();
app.Run();
#else
using Elsa.Extensions;
using Elsa.Http.Options;
using Elsa.Persistence.EFCore.Extensions;
using Elsa.Persistence.EFCore.Modules.Management;
using Elsa.Persistence.EFCore.Modules.Runtime;
using Elsa.Workflows.Api;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var services = builder.Services;
var identitySection = configuration.GetSection("Identity");
var identityTokenSection = identitySection.GetSection("Tokens");

services.AddElsa(elsa =>
{
    elsa
        .UseIdentity(identity =>
        {
            identity.TokenOptions += options => identityTokenSection.Bind(options);
            identity.UseConfigurationBasedUserProvider(options => identitySection.Bind(options));
            identity.UseConfigurationBasedApplicationProvider(options => identitySection.Bind(options));
            identity.UseConfigurationBasedRoleProvider(options => identitySection.Bind(options));
        })
        .UseDefaultAuthentication()
        .UseWorkflows()
        .UseWorkflowManagement(management => management.UseEntityFrameworkCore(ef => ef.UseSqlite()))
        .UseWorkflowRuntime(runtime => runtime.UseEntityFrameworkCore(ef => ef.UseSqlite()))
        .UseWorkflowsApi()
        .UseHttp(http => http.ConfigureHttpOptions = options => configuration.GetSection("Http").Bind(options))
        .UseScheduling()
        .UseJavaScript()
        .UseCSharp()
        .UseLiquid();
});

services.AddControllers();
services.AddCors(cors => cors.AddDefaultPolicy(policy => policy
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowAnyOrigin()
    .WithExposedHeaders("*")));
services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseCors();
app.MapHealthChecks("/");

var apiEndpointOptions = app.Services.GetRequiredService<IOptions<ApiEndpointOptions>>().Value;
var routePrefix = apiEndpointOptions.RoutePrefix;

app.MapWorkflowsApi(routePrefix);
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseJsonSerializationErrorHandler();
app.UseWorkflows();
app.MapControllers();

if (app.Environment.IsDevelopment())
    app.UseSwaggerUI();

await app.RunAsync();
#endif
