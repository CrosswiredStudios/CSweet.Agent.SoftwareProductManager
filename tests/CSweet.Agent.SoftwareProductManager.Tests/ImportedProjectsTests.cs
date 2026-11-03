using System.Text.Json;
using CSweet.Agent.SDK;
using CSweet.WorkManagement.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace CSweet.Agent.SoftwareProductManager.Tests;

public sealed class ImportedProjectsTests
{
    [Theory]
    [InlineData("Accepted", 1)]
    [InlineData("Review", 0)]
    public async Task SetupAdoptsOnlyTheExistingAssignedAcceptedSoftwareProject(string status, int expected)
    {
        var self = Guid.NewGuid(); var installation = Guid.NewGuid(); var team = Guid.NewGuid();
        var project = new WorkstreamSummary(Guid.NewGuid(), "Existing app", "Retain identity", "Proposed", "Onboarding", self, null, null, null)
        { ImportedProject = new(Guid.NewGuid(), 7, status, "Software", "Owner-approved app", null) };
        var runtime = new AgentTestRuntime(); var plans = new List<WorkstreamPlanProposalV2Request>();
        runtime.RegisterCapability<JsonElement, OrganizationSnapshotResponse>(PlatformCapabilities.OrganizationSnapshotRead,
            (_, _) => Task.FromResult(new OrganizationSnapshotResponse(Guid.NewGuid(), "Active", [], [], [], [project], [], DateTimeOffset.UtcNow)));
        var members = new[] { "software-architect", "software-developer", "software-qa" }.Select(role =>
            new AgentTeammate(Guid.NewGuid().ToString(), role, "Agent", role, role, "Report", "Available") { DeclaredRoleKeys = [role] }).ToArray();
        runtime.RegisterCapability<TeamRosterRequest, TeamRosterResponse>(PlatformCapabilities.TeamRosterRead,
            (_, _) => Task.FromResult(new TeamRosterResponse(new(team.ToString(), "approved", "Product", 1, self.ToString(), "PM", members, [], 3, false))));
        var approved = JsonSerializer.Deserialize<ResourceChangeRequestResponse>("{}")! with
        { RequesterInstallationId = installation, TeamId = team, WorkstreamId = project.Id, Status = "Approved" };
        runtime.RegisterCapability<ResourceChangeReadRequest, ResourceChangeReadResponse>(PlatformCapabilities.ResourceChangeRead, (_, _) => Task.FromResult(new ResourceChangeReadResponse([approved])));
        runtime.RegisterCapability<WorkstreamPlanProposalV2Request, MutationResponse>(PlatformCapabilities.WorkstreamPlanProposeV2, (r, _) =>
        { plans.Add(r); return Task.FromResult(new MutationResponse(true, 1, Guid.NewGuid(), "Proposed")); });
        var context = runtime.CreateContext(Guid.NewGuid().ToString(), installation.ToString(), new(self.ToString(), "PM", null, "Product Manager", null, [], null, null, null));
        var agent = new ProductManagerAgent(NullLogger<ProductManagerAgent>.Instance, null!);
        await agent.ResumeImportedProjectSetupAsync(context, default);
        Assert.Equal(expected, plans.Count);
        if (expected == 0) return;
        Assert.Equal(project.Id, plans[0].ExistingProjectId);
        Assert.Equal(7, plans[0].ExpectedProjectRevision);
        Assert.Equal(team, plans[0].InitialTeamId);
        Assert.Equal(self, plans[0].AccountableManagerOrganizationUserId);
        Assert.Contains("CEO", plans[0].SuccessCriteria[0]);
    }
}
