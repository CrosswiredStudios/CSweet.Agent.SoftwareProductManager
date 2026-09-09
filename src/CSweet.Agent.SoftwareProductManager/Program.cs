using CSweet.Agent.SDK;
using CSweet.Agent.SoftwareProductManager;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

var manifest = await AgentManifestLoader.LoadAsync("csweet-plugin.json", CancellationToken.None);
if (manifest.Id != ProductManagerProfile.AgentId || manifest.Version != ProductManagerProfile.Version)
    throw new InvalidOperationException("The Software Product Manager implementation identity does not match csweet-plugin.json.");

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase)) { Console.WriteLine($"{manifest.Id} {manifest.Version}: manifest identity verified"); return; }
builder.AddCSweetAgent<ProductManagerAgent>();
builder.Services.AddSingleton<ProductManagerOrchestrator>();

var host = builder.Build();
host.Run();
