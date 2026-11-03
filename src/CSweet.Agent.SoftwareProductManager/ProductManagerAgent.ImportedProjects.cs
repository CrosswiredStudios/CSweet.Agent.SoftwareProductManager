using System.Text.Json;
using CSweet.Agent.SDK;
using CSweet.WorkManagement.Contracts;

namespace CSweet.Agent.SoftwareProductManager;

public sealed partial class ProductManagerAgent
{
    internal async Task ResumeImportedProjectSetupAsync(AgentRuntimeContext context, CancellationToken ct)
    {
        if (!Guid.TryParse(context.Identity?.EmployeeId, out var employee) || !Guid.TryParse(context.InstallationId, out var installation)) return;
        var snapshot = await context.Platform.ReadOrganizationSnapshotAsync(ct);
        var projects = snapshot.Workstreams.Where(x => x.AccountableManagerOrganizationUserId == employee &&
            x.Status == "Proposed" && x.ImportedProject is { ProfileStatus: "Accepted", Category: "Software" }).Take(2).ToArray();
        if (projects.Length != 1) return; // Dedicated management remains one project per agent.
        var project = projects[0];
        var roster = await ReadCompleteTeamRosterAsync(context, ct);
        var approvals = await context.Platform.ReadResourceChangesAsync(new ResourceChangeReadRequest(Statuses: ["Approved"]), ct);
        var approved = approvals.Requests.Where(x => x.RequesterInstallationId == installation && x.TeamId?.ToString("D") == roster.Team?.TeamId &&
            (x.WorkstreamId is null || x.WorkstreamId == project.Id)).OrderByDescending(x => x.DecidedAt ?? x.CreatedAt).FirstOrDefault();
        if (approved is null)
        {
            await EnsureStaffingCommitmentAsync(installation, context, ct);
            return;
        }
        if (roster.Team is null || !Guid.TryParse(roster.Team.TeamId, out var team)) return;
        var requiredRoles = new[] { ArchitectRoleCategory, DeveloperRoleCategory, QualityRoleCategory };
        var participants = new HashSet<Guid> { employee };
        foreach (var role in requiredRoles)
        {
            var person = roster.Team.Members.FirstOrDefault(x => x.IsAvailable && x.DeclaredRoleKeys.Contains(role) && Guid.TryParse(x.EmployeeId, out _));
            if (person is null) return;
            participants.Add(Guid.Parse(person.EmployeeId));
        }
        var purpose = project.ImportedProject!.Purpose ?? project.Outcome;
        var proposal = new WorkstreamPlanProposalV2Request(project.Name, purpose, ["Agree the next delivery goal with the CEO before starting work."],
            "Development", employee, team, [], requiredRoles, null, null, null, null,
            "Activate the existing imported project with its approved team, preserving repository and project identity.",
            $"imported-project-setup:{project.Id:N}:{project.ImportedProject.ProjectRevision}", "software-prototype.v1", 1,
            JsonSerializer.SerializeToElement(new { participantIds = participants }, IncrementalJsonOptions),
            new(null, null, requiredRoles, ["hiring", "merge"], [], null), [], [])
        { ExistingProjectId = project.Id, ExpectedProjectRevision = project.ImportedProject.ProjectRevision };
        await context.Platform.ProposeWorkstreamAsync(proposal, ct);
    }
}
