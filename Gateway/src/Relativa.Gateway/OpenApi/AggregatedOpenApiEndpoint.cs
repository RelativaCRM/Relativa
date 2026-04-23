using System.Text.Json;
using System.Text.Json.Nodes;

namespace Relativa.Gateway.OpenApi;

/// <summary>
/// Serves a merged OpenAPI document that aggregates specs from Auth and Core,
/// prefixing all paths with the gateway route prefix so Scalar sends every
/// request through the Gateway (JWT validation + X-User-Id injection included).
/// </summary>
public static class AggregatedOpenApiEndpoint
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static IEndpointRouteBuilder MapAggregatedOpenApi(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/openapi/aggregated.json", async (
            IHttpClientFactory factory,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var client = factory.CreateClient("openapi");

            // Read upstream base addresses from YARP cluster config (same values used for proxying)
            var coreAddr = (config["ReverseProxy:Clusters:core:Destinations:d1:Address"] ?? "http://127.0.0.1:8082/").TrimEnd('/');
            var authAddr = (config["ReverseProxy:Clusters:auth:Destinations:d1:Address"] ?? "http://127.0.0.1:8081/").TrimEnd('/');
            var gatewayBase = config["Gateway:PublicBaseUrl"] ?? "http://localhost:8080";

            var coreTask = TryFetchSpecAsync(client, $"{coreAddr}/openapi/v1.json", ct);
            var authTask = TryFetchSpecAsync(client, $"{authAddr}/openapi/v1.json", ct);
            await Task.WhenAll(coreTask, authTask);

            var sources = new List<ServiceSpec>
            {
                new("/core", "Core", await coreTask),
                new("/auth", "Auth", await authTask),
            };

            var json = BuildMergedSpec(sources, gatewayBase);
            return Results.Text(json, "application/json");
        })
        .AllowAnonymous();

        return routes;
    }

    private static async Task<JsonDocument?> TryFetchSpecAsync(HttpClient client, string url, CancellationToken ct)
    {
        try
        {
            var raw = await client.GetStringAsync(url, ct);
            return JsonDocument.Parse(raw);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildMergedSpec(List<ServiceSpec> sources, string gatewayBaseUrl)
    {
        var mergedPaths = new JsonObject();
        var mergedSchemas = new JsonObject();

        foreach (var source in sources)
        {
            if (source.Spec is null) continue;
            var root = source.Spec.RootElement;

            // Merge schemas with prefix to avoid name collisions between services
            if (root.TryGetProperty("components", out var components) &&
                components.TryGetProperty("schemas", out var schemas))
            {
                foreach (var schema in schemas.EnumerateObject())
                {
                    var prefixedName = $"{source.SchemaPrefix}_{schema.Name}";
                    var schemaNode = JsonNode.Parse(schema.Value.GetRawText())!;
                    RewriteRefs(schemaNode, source.SchemaPrefix);
                    mergedSchemas[prefixedName] = schemaNode;
                }
            }

            // Merge paths, prepending the gateway route prefix
            if (root.TryGetProperty("paths", out var paths))
            {
                foreach (var path in paths.EnumerateObject())
                {
                    var prefixedPath = source.PathPrefix + path.Name;
                    var pathNode = JsonNode.Parse(path.Value.GetRawText())!;
                    RewriteRefs(pathNode, source.SchemaPrefix);
                    PrefixOperationIds(pathNode, source.SchemaPrefix);
                    mergedPaths[prefixedPath] = pathNode;
                }
            }
        }

        InjectExamples(mergedPaths);

        var doc = new JsonObject
        {
            ["openapi"] = "3.1.0",
            ["info"] = new JsonObject
            {
                ["title"] = "Relativa API",
                ["version"] = "1.0",
                ["description"] = "Aggregated spec — all requests route through the Gateway (JWT required for protected endpoints)."
            },
            ["servers"] = new JsonArray
            {
                new JsonObject
                {
                    ["url"] = gatewayBaseUrl,
                    ["description"] = "API Gateway"
                }
            },
            ["paths"] = mergedPaths,
            ["components"] = new JsonObject { ["schemas"] = mergedSchemas }
        };

        return doc.ToJsonString(WriteOptions);
    }

    /// <summary>
    /// Recursively rewrites every local schema <c>$ref</c> to include the service prefix,
    /// e.g. <c>#/components/schemas/EntityTypeDto</c> → <c>#/components/schemas/Core_EntityTypeDto</c>.
    /// </summary>
    private static void RewriteRefs(JsonNode? node, string schemaPrefix)
    {
        if (node is JsonObject obj)
        {
            if (obj.ContainsKey("$ref") && obj["$ref"] is JsonValue refValue)
            {
                const string localPrefix = "#/components/schemas/";
                var refStr = refValue.GetValue<string>();
                if (refStr.StartsWith(localPrefix))
                {
                    var schemaName = refStr[localPrefix.Length..];
                    obj["$ref"] = $"{localPrefix}{schemaPrefix}_{schemaName}";
                }
            }
            else
            {
                foreach (var key in obj.Select(kv => kv.Key).ToList())
                    RewriteRefs(obj[key], schemaPrefix);
            }
        }
        else if (node is JsonArray arr)
        {
            for (var i = 0; i < arr.Count; i++)
                RewriteRefs(arr[i], schemaPrefix);
        }
    }

    /// <summary>
    /// Prefixes <c>operationId</c> values on each HTTP method to guarantee uniqueness
    /// across the merged document (e.g. <c>ListWorkspaces</c> → <c>Core_ListWorkspaces</c>).
    /// </summary>
    private static void PrefixOperationIds(JsonNode? pathNode, string schemaPrefix)
    {
        if (pathNode is not JsonObject pathObj) return;

        foreach (var method in new[] { "get", "post", "put", "patch", "delete", "head", "options" })
        {
            if (pathObj[method] is JsonObject op && op["operationId"] is JsonValue idVal)
                op["operationId"] = $"{schemaPrefix}_{idVal.GetValue<string>()}";
        }
    }

    /// <summary>
    /// Injects pre-filled <c>example</c> values into request body media-type objects
    /// for every operation that has a known example payload.  The operationIds here
    /// already carry the service prefix added by <see cref="PrefixOperationIds"/>.
    /// </summary>
    private static void InjectExamples(JsonObject mergedPaths)
    {
        var examples = BuildExamples();

        foreach (var (_, pathNode) in mergedPaths)
        {
            if (pathNode is not JsonObject pathObj) continue;

            foreach (var method in new[] { "get", "post", "put", "patch", "delete" })
            {
                if (pathObj[method] is not JsonObject op) continue;
                if (op["operationId"] is not JsonValue idVal) continue;

                var operationId = idVal.GetValue<string>();
                if (!examples.TryGetValue(operationId, out var example)) continue;

                // Ensure the path to requestBody.content.application/json exists
                if (op["requestBody"] is not JsonObject reqBody) continue;
                if (reqBody["content"] is not JsonObject content) continue;
                if (content["application/json"] is not JsonObject mediaType) continue;

                // Clone to avoid sharing nodes between multiple serialisations
                mediaType["example"] = JsonNode.Parse(example.ToJsonString())!;
            }
        }
    }

    private static Dictionary<string, JsonNode> BuildExamples() => new()
    {
        // ── Auth ────────────────────────────────────────────────────────────────
        ["Auth_Login"] = new JsonObject
        {
            ["email"]    = "admin@relativa.com",
            ["password"] = "Relativa2026!"
        },
        ["Auth_Register"] = new JsonObject
        {
            ["firstName"] = "Jane",
            ["lastName"]  = "Doe",
            ["email"]     = "jane.doe@example.com",
            ["password"]  = "SecurePass123!"
        },

        // ── Organizations ───────────────────────────────────────────────────────
        ["Core_CreateOrganization"] = new JsonObject
        {
            ["name"] = "ACME Corp"
        },
        ["Core_UpdateOrganization"] = new JsonObject
        {
            ["name"] = "ACME Corp (renamed)"
        },

        // ── Org invitations ─────────────────────────────────────────────────────
        ["Core_InviteToOrg"] = new JsonObject
        {
            ["email"] = "new.member@example.com"
        },
        ["Core_AcceptOrgInvitation"] = new JsonObject
        {
            ["token"] = "paste-org-invitation-token-here"
        },

        // ── Join requests ────────────────────────────────────────────────────────
        ["Core_SubmitJoinRequest"] = new JsonObject
        {
            ["message"] = "I'd like to join your organization."
        },
        ["Core_ReviewJoinRequest"] = new JsonObject
        {
            ["decision"] = "approved"
        },

        // ── Workspaces ───────────────────────────────────────────────────────────
        ["Core_CreateWorkspace"] = new JsonObject
        {
            ["name"]           = "Sales Q1 2026",
            ["organizationId"] = 1
        },
        ["Core_UpdateWorkspace"] = new JsonObject
        {
            ["name"] = "Sales Q1 2026 (renamed)"
        },

        // ── Workspace invitations ────────────────────────────────────────────────
        ["Core_InviteMember"] = new JsonObject
        {
            ["email"]  = "new.member@example.com",
            ["roleId"] = 4        // ws_member
        },
        ["Core_AcceptInvitation"] = new JsonObject
        {
            ["token"] = "paste-workspace-invitation-token-here"
        },

        // ── Members ──────────────────────────────────────────────────────────────
        ["Core_AddMember"] = new JsonObject
        {
            ["userId"] = 2,
            ["roleId"] = 4        // ws_member
        },
        ["Core_UpdateMemberRole"] = new JsonObject
        {
            ["roleId"] = 3        // ws_analyst
        },

        // ── Workspace roles ──────────────────────────────────────────────────────
        ["Core_CreateRole"] = new JsonObject
        {
            ["name"]          = "Custom Role",
            ["permissionIds"] = new JsonArray(15, 16)   // view_entities, view_analytics
        },
        ["Core_UpdateRole"] = new JsonObject
        {
            ["name"]          = "Custom Role (updated)",
            ["permissionIds"] = new JsonArray(15)
        },

        // ── Organization roles ───────────────────────────────────────────────────
        ["Core_CreateOrgRole"] = new JsonObject
        {
            ["name"]          = "Org Viewer",
            ["permissionIds"] = new JsonArray(1, 2)     // view_members, manage_members
        },
        ["Core_UpdateOrgRole"] = new JsonObject
        {
            ["name"]          = "Org Viewer (updated)",
            ["permissionIds"] = new JsonArray(1)
        },

        // ── Entities ─────────────────────────────────────────────────────────────
        ["Core_CreateEntity"] = new JsonObject
        {
            ["entityTypeId"] = 1,
            ["properties"] = new JsonArray
            {
                new JsonObject { ["propertyId"] = 1, ["value"] = "Jane"  },
                new JsonObject { ["propertyId"] = 2, ["value"] = "Smith" }
            }
        },
        ["Core_UpdateEntity"] = new JsonObject
        {
            ["properties"] = new JsonArray
            {
                new JsonObject { ["propertyId"] = 1, ["value"] = "Jane (updated)" },
                new JsonObject { ["propertyId"] = 2, ["value"] = "Smith"          }
            }
        },
    };

    private sealed record ServiceSpec(string PathPrefix, string SchemaPrefix, JsonDocument? Spec);
}
