using System.Text.Json;
using CSweet.Agent.SDK;
using Xunit;

namespace CSweet.Agent.SoftwareProductManager.Tests;

public sealed class ProjectIncidentTests
{
    [Fact]
    public async Task IncidentIsHandledBeforeOrdinaryWorkflowWithoutConfiguredModel()
    {
        var self = Guid.NewGuid();
        var incident = new ManagementIncident(Guid.NewGuid(), Guid.NewGuid(), self, self, "Open", "Schema failure", null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(15), 3,
            "agent.payload_invalid", "Unknown", "Exact schema unavailable", "Repair", []);
        var calls = 0;
        var runtime = new AgentTestRuntime().RegisterCapability<ReadManagementIncidents, ManagementIncidentPage>(
            ProjectHealthCapabilities.Incidents, (_, _) => Task.FromResult(new ManagementIncidentPage([incident], null)));
        runtime.RegisterCapability<ReportManagementIncident, ManagementIncident>(ProjectHealthCapabilities.Report, (request, _) => { calls++; Assert.Equal(incident.Id, request.IncidentId); Assert.Equal(IncidentDispositions.Escalate, request.Disposition); return Task.FromResult(incident); });
        var context = runtime.CreateContext(identity: new AgentIdentity(self.ToString(), "Manager", null, null, null, [], null, null, null));
        var hint = JsonSerializer.SerializeToElement(new ProjectHealthHint(incident.WorkstreamId, incident.Id, 1), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await new ProductManagerAgent(Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductManagerAgent>.Instance, null!).HandleEventAsync(new(Guid.NewGuid(), Guid.NewGuid(), ProjectHealthEvents.IncidentChanged, hint, DateTimeOffset.UtcNow, "incident"), context, default);
        Assert.Equal(1, calls);
    }
}
