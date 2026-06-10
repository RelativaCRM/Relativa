using NBomber.CSharp;

static string Env(string key, string fallback) =>
    Environment.GetEnvironmentVariable(key) is { Length: > 0 } v ? v : fallback;

static int EnvInt(string key, int fallback) =>
    int.TryParse(Environment.GetEnvironmentVariable(key), out var v) ? v : fallback;

static double EnvDouble(string key, double fallback) =>
    double.TryParse(Environment.GetEnvironmentVariable(key), out var v) ? v : fallback;

var rate = EnvInt("LOAD_RATE", 20);
var durationSec = EnvInt("LOAD_DURATION_SEC", 15);
var p95ThresholdMs = EnvDouble("LOAD_P95_MS", 800);
var maxFailRate = EnvDouble("LOAD_MAX_FAIL_RATE", 0.05);

var targetSpec = Env("LOAD_TARGETS", string.Join(
    ',',
    "gateway=http://localhost:8080/",
    "auth=http://localhost:8081/",
    "core=http://localhost:8082/",
    "graph=http://localhost:8083/",
    "audit=http://localhost:8086/"));

var targets = targetSpec
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(pair => pair.Split('=', 2))
    .Where(p => p.Length == 2)
    .ToDictionary(p => p[0], p => p[1]);

var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

var scenarios = targets.Select(target =>
    Scenario.Create($"availability_{target.Key}", async _ =>
    {
        try
        {
            using var response = await http.GetAsync(target.Value);
            return (int)response.StatusCode < 500
                ? Response.Ok(statusCode: ((int)response.StatusCode).ToString())
                : Response.Fail(statusCode: ((int)response.StatusCode).ToString(), message: "server error");
        }
        catch (Exception ex)
        {
            return Response.Fail(message: ex.Message);
        }
    })
    .WithoutWarmUp()
    .WithLoadSimulations(Simulation.Inject(
        rate: rate,
        interval: TimeSpan.FromSeconds(1),
        during: TimeSpan.FromSeconds(durationSec)))
).ToArray();

var stats = NBomberRunner
    .RegisterScenarios(scenarios)
    .WithReportFolder("load-reports")
    .Run();

var breached = false;
Console.WriteLine();
Console.WriteLine("=== Load thresholds ===");
Console.WriteLine($"p95 <= {p95ThresholdMs} ms, fail rate <= {maxFailRate:P0}");
foreach (var sc in stats.ScenarioStats)
{
    var p95 = sc.Ok.Latency.Percent95;
    var ok = sc.Ok.Request.Count;
    var fail = sc.Fail.Request.Count;
    var total = ok + fail;
    var failRate = total > 0 ? (double)fail / total : 1.0;

    var p95Ok = p95 <= p95ThresholdMs;
    var failOk = failRate <= maxFailRate;
    if (!p95Ok || !failOk) breached = true;

    Console.WriteLine(
        $"  {sc.ScenarioName,-26} ok={ok,-6} fail={fail,-5} p95={p95,7:F1}ms failRate={failRate,6:P1} " +
        $"[{(p95Ok ? "p95 OK" : "p95 FAIL")}, {(failOk ? "errors OK" : "errors FAIL")}]");
}

if (breached)
{
    Console.WriteLine("RESULT: FAIL — one or more thresholds breached.");
    return 1;
}

Console.WriteLine("RESULT: PASS — all thresholds met.");
return 0;
