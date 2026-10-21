using CSweet.Agent.SDK;
using CSweet.WorkManagement.Contracts;

namespace CSweet.Agent.SoftwareProductManager;

/// <summary>Preserves an existing code brief when adopting it into canonical planning.</summary>
internal static class LegacyBriefPlanning
{
    internal static async Task<WorkItem> AdoptAsync(Guid boardId, Guid itemId, long expectedRevision,
        AgentRuntimeContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!Guid.TryParse(context.Identity?.EmployeeId, out var manager))
            throw new InvalidOperationException("A current manager identity is required.");
        var board = await context.Platform.Work.ReadBoardAsync(boardId, ct);
        if (board.Board.IsArchived || board.Board.ManagerOrganizationUserId != manager ||
            board.Board.WorkstreamId is not { } projectId || board.Board.TeamId is not { } teamId)
            throw new InvalidOperationException("Adopt a brief on a current project board you manage.");
        var project = await context.Platform.ReadWorkstreamAsync(new(projectId), ct);
        if (project.AccountableManagerOrganizationUserId != manager || project.Status is not ("Active" or "Approved"))
            throw new InvalidOperationException("Current approved project management is required.");
        var item = await context.Platform.Work.ReadItemAsync(new(boardId, itemId), ct);
        if (item.Revision != expectedRevision || item.Status != WorkStatuses.Ready ||
            item.ExecutionMode != WorkItemExecutionModes.Executable || item.TypeKey == WorkItemTypeKeys.WorkflowTicketV1 ||
            item.SprintId is not { } sprintId || item.Delivery is not null)
            throw new InvalidOperationException("Read the current Ready implementation ticket in a Planned sprint before adoption; finalized work cannot be rebound.");
        if (item.Planning is not null)
            throw new InvalidOperationException("The ticket already has planning. Re-read that accepted record and continue delivery; adoption cannot replace it.");
        if ((await context.Platform.Work.ListSprintsAsync(boardId, ct)).SingleOrDefault(x => x.Id == sprintId)?.Status != "Planned")
            throw new InvalidOperationException("Adoption is limited to a Planned sprint.");
        if (item.Development is not { Requirements.Count: > 0, AcceptanceCriteria.Count: > 0 } brief ||
            item.AccountableOrganizationUserId is not { } owner)
            throw new InvalidOperationException("An existing complete developer brief and accountable owner are required.");
        if (brief.QualityGateColumnId is not null || brief.ReworkFindings is { Count: > 0 })
            throw new InvalidOperationException("Retain this brief's explicit quality gate or rework through its existing governed workflow; adoption cannot discard those bindings.");
        var roster = await context.Platform.ReadCompleteTeamRosterAsync(token: ct);
        if (roster is null || roster.TeamId != teamId.ToString() ||
            !roster.Members.Any(x => x.EmployeeId == manager.ToString() && x.AgentInstallationId?.ToString() == context.InstallationId && x.IsAvailable) ||
            !roster.Members.Any(x => x.EmployeeId == owner.ToString() && x.IsAvailable))
            throw new InvalidOperationException("The current manager, accountable owner and board must retain their available team identities.");
        var assignments = item.StageAssignments.Select(stage =>
        {
            if (stage.PrincipalKind != "AgentInstallation" || stage.OrganizationUserId is not null) return stage;
            var exact = roster.Members.Where(x => x.AgentInstallationId == stage.AgentInstallationId &&
                stage.AgentInstallationId.HasValue && x.IsAvailable).ToArray();
            if (exact.Length != 1 || !Guid.TryParse(exact[0].EmployeeId, out var employee))
                throw new InvalidOperationException("Resolve the exact retained stage installation in the current roster; adoption cannot select another principal.");
            return stage with { OrganizationUserId = employee };
        }).ToArray();
        SprintDeliveryPreparation.ValidateStageAssignments(item with { StageAssignments = assignments }, roster);
        var planning = new WorkItemPlanningSpecification(brief.Requirements, brief.AcceptanceCriteria, brief.Constraints);
        // The platform rechecks current revisions, manager authority and every retained principal.
        // A lost response must be reconciled through the now-current planning, never a blind replay.
        return await context.Platform.Work.RevisePlanningAsync(new(boardId, item.Id, item.Title, item.Description,
            item.ParentItemId, planning, item.Revision, item.PlanningRevision,
            $"pm-adopt-brief:{item.Id:N}:{item.Revision}:{item.PlanningRevision}")
        { AccountableOrganizationUserId = owner, StageAssignments = assignments }, ct);
    }
}
