using System.Text.Json;
using CSweet.Agent.SDK;
using CSweet.WorkManagement.Contracts;
namespace CSweet.Agent.SoftwareProductManager;

public sealed partial class ProductManagerAgent
{
    private async Task ResumeProjectSetupAsync(AgentRuntimeContext context, CancellationToken ct)
    {
        foreach (var intake in await context.Platform.Projects.ListAssistanceAsync(ct))
            await HandleProjectSetupIntakeAsync(intake.Id, context, ct);
    }
    private async Task<AgentCoordinationTurnResult> HandleProjectSetupAsync(AgentCoordinationArtifact artifact, AgentRuntimeContext context, CancellationToken ct)
    {
        var request = artifact.Payload.Deserialize<ProjectManagerAssistanceRequest>(IncrementalJsonOptions);
        if (request is null) return AgentCoordinationTurnResult.Blocked("The project setup request is missing.");
        return await HandleProjectSetupIntakeAsync(request.IntakeId, context, ct);
    }
    private async Task<AgentCoordinationTurnResult> HandleProjectSetupIntakeAsync(Guid intakeId, AgentRuntimeContext context, CancellationToken ct)
    {
        var intake = await context.Platform.Projects.ReadManagerSetupAsync(intakeId, ct);
        var roster = await ReadCompleteTeamRosterAsync(context, ct);
        if (!Guid.TryParse(context.InstallationId, out var installation)) return AgentCoordinationTurnResult.Blocked("The manager installation is unavailable.");
        var plans = await context.Platform.ReadResourceChangesAsync(new ResourceChangeReadRequest(Statuses: ["Approved"]), ct);
        var approved = plans.Requests.Where(x => x.RequesterInstallationId == installation && x.TeamId?.ToString("D") == roster.Team?.TeamId)
            .OrderByDescending(x => x.DecidedAt ?? x.CreatedAt).FirstOrDefault();
        if (approved is null)
        {
            await EnsureStaffingCommitmentAsync(installation, context, ct);
            return AgentCoordinationTurnResult.Blocked("I've retained the project request and queued the existing product-team approval process. Project setup will resume once staffing is approved and available.");
        }
        var requiredRoles = new[] { ArchitectRoleCategory, DeveloperRoleCategory, QualityRoleCategory };
        var selected = new HashSet<Guid> { intake.DeveloperId, intake.ManagerId!.Value };
        foreach (var role in requiredRoles)
        {
            var member = roster.Team?.Members.FirstOrDefault(x => x.IsAvailable && x.DeclaredRoleKeys.Contains(role) && Guid.TryParse(x.EmployeeId, out _));
            if (member is null)
                return AgentCoordinationTurnResult.Blocked($"The managed project needs approved staffing for {role}. Please complete the existing team staffing approval before project setup. The user can alternatively create a prototype project at {intake.SetupUrl}.");
            selected.Add(Guid.Parse(member.EmployeeId));
        }
        if (roster.Team is null || !Guid.TryParse(roster.Team.TeamId, out var team) || intake.TeamId.HasValue && team != intake.TeamId)
            return AgentCoordinationTurnResult.Blocked("The project manager and developer need a compatible approved team. An existing agent will not be moved between teams.");
        var proposal = new WorkstreamPlanProposalV2Request(intake.Name, intake.Goal, ["Deliver a tested, running review URL"], "Development",
            intake.ManagerId.Value, team, [], requiredRoles, null, null, null, null,
            "The user requested project-manager setup for this retained development request.", $"project-intake-plan:{intake.Id:N}:{intake.SetupChoiceMessageId:N}",
            "software-prototype.v1", 1, JsonSerializer.SerializeToElement(new { intakeId = intake.Id, setupChoiceMessageId = intake.SetupChoiceMessageId, participantIds = selected }, IncrementalJsonOptions),
            new(null, null, requiredRoles, ["hiring", "merge"], [], null), [], []);
        await context.Platform.ProposeWorkstreamAsync(proposal, ct);
        return AgentCoordinationTurnResult.Completed("I’ve submitted the project setup for approval with the selected team. Development will wait until the project and developer assignment are confirmed.");
    }
}
