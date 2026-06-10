using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NBomber.Contracts;
using NBomber.CSharp;

static string Env(string key, string fallback) =>
    Environment.GetEnvironmentVariable(key) is { Length: > 0 } v ? v : fallback;

static int EnvInt(string key, int fallback) =>
    int.TryParse(Environment.GetEnvironmentVariable(key), out var v) ? v : fallback;

static double EnvDouble(string key, double fallback) =>
    double.TryParse(Environment.GetEnvironmentVariable(key), out var v) ? v : fallback;

var gateway = Env("LOAD_GATEWAY", "http://localhost:8080").TrimEnd('/');
var adminEmail = Env("LOAD_ADMIN_EMAIL", "admin@relativa.com");
var adminPassword = Env("LOAD_ADMIN_PASSWORD", "Demo1234!");
var rate = EnvInt("LOAD_RATE", 10);
var durationSec = EnvInt("LOAD_DURATION_SEC", 20);
var warmupSec = EnvInt("LOAD_WARMUP_SEC", 3);
var soakSec = EnvInt("LOAD_SOAK_SEC", 120);
var spikeMultiplier = Math.Max(2, EnvInt("LOAD_SPIKE_MULTIPLIER", 5));
var profile = Env("LOAD_PROFILE", "smoke").ToLowerInvariant();
var p95ThresholdMs = EnvDouble("LOAD_P95_MS", 1500);
var maxFailRate = EnvDouble("LOAD_MAX_FAIL_RATE", 0.05);

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

string token;
int orgId;
int workspaceId;
using (var boot = new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
{
    try
    {
        token = await LoginAsync(boot, gateway, adminEmail, adminPassword, json);
        boot.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        orgId = await FirstOrgIdAsync(boot, gateway, json);
        workspaceId = await DiscoverOrCreateWorkspaceAsync(boot, gateway, orgId, json);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Load test setup failed: {ex.Message}");
        Console.Error.WriteLine(
            "Ensure the stack is reachable and the admin account and an organization are seeded " +
            "(configure LOAD_GATEWAY, LOAD_ADMIN_EMAIL, LOAD_ADMIN_PASSWORD).");
        return 2;
    }
}

Console.WriteLine(
    $"Authenticated as {adminEmail} via {gateway}; orgId={orgId}, workspaceId={workspaceId}; " +
    $"profile={profile}, rate={rate}/s, duration={durationSec}s.");

var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

var rnd = new Random();
string[] riskLevels = ["", "high", "medium", "low"];
string RiskParam()
{
    var level = riskLevels[rnd.Next(riskLevels.Length)];
    return level.Length == 0 ? string.Empty : $"&riskLevel={level}";
}

LoadSimulation[] SimulationsAt(int r) => profile switch
{
    "ramp" =>
    [
        Simulation.RampingInject(r, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(durationSec)),
        Simulation.Inject(r, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(durationSec)),
    ],
    "soak" =>
    [
        Simulation.Inject(r, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(soakSec)),
    ],
    "spike" =>
    [
        Simulation.Inject(r, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(durationSec)),
        Simulation.Inject(r * spikeMultiplier, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(Math.Max(5, durationSec / 2))),
        Simulation.Inject(r, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(durationSec)),
    ],
    _ =>
    [
        Simulation.Inject(r, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(durationSec)),
    ],
};

ScenarioProps Tune(ScenarioProps sc, int r)
{
    var warmed = warmupSec > 0
        ? sc.WithWarmUpDuration(TimeSpan.FromSeconds(warmupSec))
        : sc.WithoutWarmUp();
    return warmed.WithLoadSimulations(SimulationsAt(r));
}

async Task<Response<object>> GetExpecting2xx(string label, string url)
{
    try
    {
        using var response = await http.GetAsync(url);
        return response.IsSuccessStatusCode
            ? Response.Ok(statusCode: ((int)response.StatusCode).ToString())
            : Response.Fail(statusCode: ((int)response.StatusCode).ToString(), message: $"{label}: non-2xx");
    }
    catch (Exception ex)
    {
        return Response.Fail(message: $"{label}: {ex.Message}");
    }
}

ScenarioProps AuthedGet(string name, Func<string> url) =>
    Tune(Scenario.Create(name, async _ => await GetExpecting2xx(name, url())), rate);

var journey = Tune(Scenario.Create("user_journey", async ctx =>
{
    await Step.Run("org_summary", ctx, () =>
        GetExpecting2xx("org_summary", $"{gateway}/graph/api/v1/dashboard/summary?organizationId={orgId}"));
    await Step.Run("graph_build", ctx, () =>
        GetExpecting2xx("graph_build", $"{gateway}/graph/api/v1/graph?organizationId={orgId}"));
    await Step.Run("entity_list", ctx, () =>
        GetExpecting2xx("entity_list", $"{gateway}/core/api/v1/workspaces/{workspaceId}/entities/?take=50"));
    return Response.Ok();
}), Math.Max(1, rate / 3));

var businessScenarios = new List<ScenarioProps>
{
    AuthedGet("graph_build", () => $"{gateway}/graph/api/v1/graph?organizationId={orgId}{RiskParam()}"),
    AuthedGet("org_dashboard_summary", () => $"{gateway}/graph/api/v1/dashboard/summary?organizationId={orgId}"),
    AuthedGet("org_dashboard_pipeline", () => $"{gateway}/graph/api/v1/dashboard/pipeline?organizationId={orgId}"),
    AuthedGet("org_dashboard_risk", () => $"{gateway}/graph/api/v1/dashboard/risk-distribution?organizationId={orgId}"),
    AuthedGet("org_dashboard_trends", () => $"{gateway}/graph/api/v1/dashboard/trends?organizationId={orgId}"),
    AuthedGet("org_dashboard_top_entities", () => $"{gateway}/graph/api/v1/dashboard/top-entities?organizationId={orgId}"),
    AuthedGet("workspace_dashboard_summary", () => $"{gateway}/graph/api/v1/dashboard/workspace/{workspaceId}/summary"),
    AuthedGet("workspace_dashboard_risk", () => $"{gateway}/graph/api/v1/dashboard/workspace/{workspaceId}/risk-distribution"),
    AuthedGet("entity_list", () => $"{gateway}/core/api/v1/workspaces/{workspaceId}/entities/?take=50"),
    AuthedGet("organizations_list", () => $"{gateway}/core/api/v1/organizations/"),
    AuthedGet("workspaces_list", () => $"{gateway}/core/api/v1/workspaces/?organizationId={orgId}"),
    AuthedGet("audit_log", () => $"{gateway}/audit/audit-log?entity_type=organization&organization_id={orgId}"),
    journey,
};

var stats = NBomberRunner
    .RegisterScenarios(businessScenarios.ToArray())
    .WithReportFolder("load-reports")
    .Run();

var breached = false;
Console.WriteLine();
Console.WriteLine("=== Load thresholds ===");
Console.WriteLine($"p95 <= {p95ThresholdMs} ms, fail rate <= {maxFailRate:P0}");
foreach (var sc in stats.ScenarioStats)
{
    var p95 = sc.Ok.Latency.Percent95;
    var p99 = sc.Ok.Latency.Percent99;
    var ok = sc.Ok.Request.Count;
    var fail = sc.Fail.Request.Count;
    var total = ok + fail;
    var failRate = total > 0 ? (double)fail / total : 1.0;

    var p95Ok = p95 <= p95ThresholdMs;
    var failOk = failRate <= maxFailRate;
    if (!p95Ok || !failOk) breached = true;

    Console.WriteLine(
        $"  {sc.ScenarioName,-30} ok={ok,-6} fail={fail,-5} p95={p95,7:F1}ms p99={p99,7:F1}ms " +
        $"failRate={failRate,6:P1} [{(p95Ok ? "p95 OK" : "p95 FAIL")}, {(failOk ? "errors OK" : "errors FAIL")}]");
}

if (breached)
{
    Console.WriteLine("RESULT: FAIL — one or more thresholds breached.");
    return 1;
}

Console.WriteLine("RESULT: PASS — all thresholds met.");
return 0;

static async Task<string> LoginAsync(HttpClient http, string gateway, string email, string password, JsonSerializerOptions json)
{
    using var response = await http.PostAsJsonAsync($"{gateway}/auth/api/v1/auth/login", new { email, password }, json);
    if (!response.IsSuccessStatusCode)
        throw new InvalidOperationException($"login returned {(int)response.StatusCode}");
    var body = await response.Content.ReadFromJsonAsync<JsonElement>(json);
    if (body.TryGetProperty("accessToken", out var t) && t.GetString() is { Length: > 0 } accessToken)
        return accessToken;
    throw new InvalidOperationException("login succeeded but no accessToken was returned (two-factor enabled?)");
}

static async Task<int> FirstOrgIdAsync(HttpClient http, string gateway, JsonSerializerOptions json)
{
    using var response = await http.GetAsync($"{gateway}/core/api/v1/organizations/");
    response.EnsureSuccessStatusCode();
    var body = await response.Content.ReadFromJsonAsync<JsonElement>(json);
    if (body.ValueKind == JsonValueKind.Array && body.GetArrayLength() > 0)
        return body[0].GetProperty("id").GetInt32();
    throw new InvalidOperationException("the load-test account belongs to no organization");
}

static async Task<int> DiscoverOrCreateWorkspaceAsync(HttpClient http, string gateway, int orgId, JsonSerializerOptions json)
{
    using (var response = await http.GetAsync($"{gateway}/core/api/v1/workspaces/?organizationId={orgId}"))
    {
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(json);
        if (body.ValueKind == JsonValueKind.Array && body.GetArrayLength() > 0)
            return body[0].GetProperty("id").GetInt32();
    }

    using var created = await http.PostAsJsonAsync(
        $"{gateway}/core/api/v1/workspaces",
        new { name = $"LoadTest {DateTime.UtcNow:yyyyMMddHHmmss}", organizationId = orgId },
        json);
    created.EnsureSuccessStatusCode();
    var workspace = await created.Content.ReadFromJsonAsync<JsonElement>(json);
    return workspace.GetProperty("id").GetInt32();
}
