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

async Task<Response<object>> SendExpecting2xx(string label, HttpMethod method, string url, object? body)
{
    try
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: json);
        using var response = await http.SendAsync(request);
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

object? entityCreateBody = null;
(int relId, int targetA, int targetB)? reassignSeed = null;
try
{
    using var typesResp = await http.GetAsync($"{gateway}/core/api/v1/entity-types");
    if (typesResp.IsSuccessStatusCode)
    {
        var entityTypes = await typesResp.Content.ReadFromJsonAsync<JsonElement>(json);
        entityCreateBody = BuildCreateBody(entityTypes);
        reassignSeed = await SeedReassignAsync(http, gateway, workspaceId, entityTypes, json);
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Write-path discovery skipped: {ex.Message}");
}

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

businessScenarios.Add(Tune(Scenario.Create("auth_login", async _ =>
    await SendExpecting2xx("auth_login", HttpMethod.Post, $"{gateway}/auth/api/v1/auth/login",
        new { email = adminEmail, password = adminPassword })), Math.Max(1, rate / 2)));

businessScenarios.Add(Tune(Scenario.Create("workspace_create", async _ =>
    await SendExpecting2xx("workspace_create", HttpMethod.Post, $"{gateway}/core/api/v1/workspaces",
        new { name = $"LoadWS {Guid.NewGuid():N}", organizationId = orgId })), Math.Max(1, rate / 2)));

if (entityCreateBody is not null)
{
    businessScenarios.Add(Tune(Scenario.Create("entity_create", async _ =>
        await SendExpecting2xx("entity_create", HttpMethod.Post,
            $"{gateway}/core/api/v1/workspaces/{workspaceId}/entities/", entityCreateBody)),
        Math.Max(1, rate / 2)));
}
else
{
    Console.WriteLine("entity_create scenario skipped: no entity type creatable without required links was found.");
}

if (reassignSeed is { } rs)
{
    var reassignToggle = 0;
    businessScenarios.Add(Tune(Scenario.Create("entity_relationship_reassign", async _ =>
    {
        var target = Interlocked.Increment(ref reassignToggle) % 2 == 0 ? rs.targetA : rs.targetB;
        return await SendExpecting2xx("entity_relationship_reassign", HttpMethod.Put,
            $"{gateway}/core/api/v1/workspaces/{workspaceId}/entity-relationships/{rs.relId}",
            new { newSourceEntityId = (int?)null, newTargetEntityId = (int?)target });
    }), Math.Max(1, rate / 3)));
}
else
{
    Console.WriteLine("entity_relationship_reassign scenario skipped: no reassignable relationship could be seeded.");
}

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

static bool HasRequiredOutgoing(JsonElement type)
{
    if (!type.TryGetProperty("outgoingRelationships", out var rels) || rels.ValueKind != JsonValueKind.Array)
        return false;
    foreach (var r in rels.EnumerateArray())
        if (r.TryGetProperty("isRequired", out var req) && req.GetBoolean())
            return true;
    return false;
}

static string SampleValue(JsonElement prop)
{
    if (prop.TryGetProperty("allowedValues", out var av) && av.ValueKind == JsonValueKind.Array && av.GetArrayLength() > 0)
        return av[0].GetProperty("value").GetString() ?? "load";
    var dataType = prop.TryGetProperty("dataType", out var d) ? d.GetString() : "String";
    return dataType switch
    {
        "Int" => "1",
        "Decimal" => "1",
        "Bool" => "true",
        "Date" => "2026-01-01",
        _ => "load",
    };
}

static List<object> RequiredProps(JsonElement type)
{
    var list = new List<object>();
    if (!type.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Array)
        return list;
    foreach (var p in props.EnumerateArray())
        if (p.TryGetProperty("isRequired", out var req) && req.GetBoolean())
            list.Add(new { propertyId = p.GetProperty("propertyId").GetInt32(), value = SampleValue(p) });
    return list;
}

static object? BuildCreateBody(JsonElement types)
{
    if (types.ValueKind != JsonValueKind.Array)
        return null;
    foreach (var t in types.EnumerateArray())
    {
        if (HasRequiredOutgoing(t))
            continue;
        return new
        {
            entityTypeId = t.GetProperty("id").GetInt32(),
            properties = RequiredProps(t),
            links = Array.Empty<object>(),
        };
    }
    return null;
}

static async Task<int?> CreateEntityAsync(
    HttpClient http, string gateway, int workspaceId, JsonElement type, JsonSerializerOptions json)
{
    using var resp = await http.PostAsJsonAsync(
        $"{gateway}/core/api/v1/workspaces/{workspaceId}/entities/",
        new { entityTypeId = type.GetProperty("id").GetInt32(), properties = RequiredProps(type), links = Array.Empty<object>() },
        json);
    if (!resp.IsSuccessStatusCode)
        return null;
    var body = await resp.Content.ReadFromJsonAsync<JsonElement>(json);
    return body.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number ? id.GetInt32() : null;
}

static async Task<(int relId, int targetA, int targetB)?> SeedReassignAsync(
    HttpClient http, string gateway, int workspaceId, JsonElement types, JsonSerializerOptions json)
{
    if (types.ValueKind != JsonValueKind.Array)
        return null;
    foreach (var src in types.EnumerateArray())
    {
        if (HasRequiredOutgoing(src)
            || !src.TryGetProperty("outgoingRelationships", out var rels)
            || rels.ValueKind != JsonValueKind.Array || rels.GetArrayLength() == 0)
            continue;
        var rel = rels[0];
        var relTypeId = rel.GetProperty("relationshipTypeId").GetInt32();
        var targetTypeId = rel.GetProperty("targetEntityTypeId").GetInt32();

        JsonElement targetType = default;
        var found = false;
        foreach (var t in types.EnumerateArray())
            if (t.GetProperty("id").GetInt32() == targetTypeId) { targetType = t; found = true; break; }
        if (!found || HasRequiredOutgoing(targetType))
            continue;

        var source = await CreateEntityAsync(http, gateway, workspaceId, src, json);
        var a = await CreateEntityAsync(http, gateway, workspaceId, targetType, json);
        var b = await CreateEntityAsync(http, gateway, workspaceId, targetType, json);
        if (source is null || a is null || b is null)
            continue;

        using var relResp = await http.PostAsJsonAsync(
            $"{gateway}/core/api/v1/workspaces/{workspaceId}/entity-relationships",
            new { sourceEntityId = source.Value, targetEntityId = a.Value, relationshipTypeId = relTypeId },
            json);
        if (!relResp.IsSuccessStatusCode)
            continue;
        var rb = await relResp.Content.ReadFromJsonAsync<JsonElement>(json);
        var relId = rb.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number
            ? idEl.GetInt32()
            : rb.TryGetProperty("relationshipId", out var rid) && rid.ValueKind == JsonValueKind.Number
                ? rid.GetInt32()
                : 0;
        if (relId == 0)
            continue;
        return (relId, a.Value, b.Value);
    }
    return null;
}
