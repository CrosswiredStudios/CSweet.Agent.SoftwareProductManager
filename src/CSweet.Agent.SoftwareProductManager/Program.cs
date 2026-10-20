using CSweet.Agent.SDK;
using CSweet.Agent.SoftwareProductManager;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

var manifest = await AgentManifestLoader.LoadAsync("csweet-plugin.json", CancellationToken.None);
if (manifest.Id != ProductManagerProfile.AgentId || manifest.Version != ProductManagerProfile.Version)
    throw new InvalidOperationException("The Software Product Manager implementation identity does not match csweet-plugin.json.");

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase)) { Console.WriteLine($"{manifest.Id} {manifest.Version}: manifest identity verified"); return; }
if (args.Contains("--export-eval-contract", StringComparer.Ordinal))
{
    var tool = ProductManagerAgent.CreateResourceChangeApprovalTool((_, _, _, _, _, _, _, _) =>
        Task.FromResult(ResourceChangeApprovalToolResult.Failure("Synthetic evaluation; no effects.")));
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
    {
        systemPrompt = ProductManagerProfile.SystemPrompt,
        boundedHiringPrompt = ProductManagerAgent.BoundedHiringSystemPrompt,
        tool = new { type = "function", function = new { name = tool.Name, description = tool.Description, parameters = tool.JsonSchema } }
    }));
    return;
}
builder.AddCSweetAgent<ProductManagerAgent>();
builder.Services.AddSingleton<ProductManagerOrchestrator>();

var host = builder.Build();
host.Run();
