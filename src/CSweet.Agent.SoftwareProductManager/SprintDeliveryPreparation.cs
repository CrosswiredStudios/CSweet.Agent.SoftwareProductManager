using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CSweet.Agent.SDK;
using CSweet.WorkManagement.Contracts;

namespace CSweet.Agent.SoftwareProductManager;

/// <summary>Prepare only the selected sprint, preserving its accepted scope and exact principals.</summary>
internal static class SprintDeliveryPreparation
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal sealed record Receipt(Guid PlanId, Guid WorkflowTicketId, string Fingerprint);
    internal sealed record Result(Guid PlanId, Guid WorkflowTicketId, string Status, string Branch,
        string Summary);

    internal static async Task<Result> PrepareAsync(Guid boardId, Guid sprintId,
        AgentRuntimeContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!Guid.TryParse(context.Identity?.EmployeeId, out var manager) ||
            !Guid.TryParse(context.InstallationId, out var installation))
            throw new InvalidOperationException("A current Product Manager identity is required.");
        var board = await context.Platform.Work.ReadBoardAsync(boardId, ct);
        if (board.Board.IsArchived || board.Board.ManagerOrganizationUserId != manager ||
            board.Board.WorkstreamId is not { } projectId || board.Board.TeamId is not { } teamId)
            throw new InvalidOperationException("Select a current project board that you manage with an assigned team.");
        var project = await context.Platform.ReadWorkstreamAsync(new(projectId), ct);
        if (project.AccountableManagerOrganizationUserId != manager || project.Status is not ("Active" or "Approved"))
            throw new InvalidOperationException("The project must be approved and currently managed by this employee.");
        var sprint = (await context.Platform.Work.ListSprintsAsync(boardId, ct)).SingleOrDefault(x => x.Id == sprintId);
        if (sprint?.Status != "Planned")
            throw new InvalidOperationException("Prepare a Planned sprint; active or completed executions cannot be rebound.");
        var tasks = board.Items.Where(x => x.SprintId == sprintId && x.ExecutionMode == WorkItemExecutionModes.Executable)
            .OrderBy(x => x.Id).ToArray();
        if (tasks.Length is 0 or > 100 || tasks.Any(x => x.TypeKey == WorkItemTypeKeys.WorkflowTicketV1))
            throw new InvalidOperationException("Select one to 100 executable implementation tickets, without workflow tickets.");
        if (tasks.Any(x => x.Status != WorkStatuses.Ready || x.Planning is not { Requirements.Count: > 0, AcceptanceCriteria.Count: > 0 } ||
            x.AccountableOrganizationUserId is null || x.StageAssignments.Count == 0))
            throw new InvalidOperationException("Every selected ticket needs accepted planning, a Ready state, an accountable owner and exact stage assignments.");
        if (tasks.Any(x => x.Planning!.DeliveryKind == "Artifact" || x.Delivery?.DeliveryKind == "Artifact"))
            throw new InvalidOperationException("This operation prepares code delivery; preserve artifact tickets for their declared workflow.");
        var roster = await context.Platform.ReadCompleteTeamRosterAsync(token: ct);
        if (roster is null || roster.TeamId != teamId.ToString() ||
            !roster.Members.Any(x => x.EmployeeId == manager.ToString() && x.AgentInstallationId == installation && x.IsAvailable))
            throw new InvalidOperationException("The current manager and selected board must belong to the same available team roster.");
        foreach (var task in tasks) ValidateStageAssignments(task, roster);
        var options = await context.Platform.SourceControl.ListTeamRepositoryOptionsAsync(new(teamId), ct);
        var bindings = tasks.Where(x => x.Delivery is not null).Select(x => x.Delivery!.RepositoryId).Distinct().ToArray();
        var repository = bindings.Length == 1 ? options.SingleOrDefault(x => x.RepositoryId == bindings[0]) :
            bindings.Length == 0 && options.Count == 1 ? options[0] : null;
        if (repository is null || string.IsNullOrWhiteSpace(repository.DefaultBranch))
            throw new InvalidOperationException("Select an unambiguous approved team repository and base branch before delivery preparation.");
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            boardId, sprintId, manager, repository.RepositoryId, repository.DefaultBranch,
            tasks = tasks.Select(x => new { x.Id, x.PlanningRevision, x.Planning, x.AccountableOrganizationUserId,
                stages = x.StageAssignments.OrderBy(a => a.StageKey, StringComparer.Ordinal) })
        }, Json))));
        var key = "product-sprint-delivery:" + sprintId.ToString("N");
        var state = await context.Platform.ReadOperatingStateAsync<Receipt>(key, ct);
        if (state is not null && state.Payload.Fingerprint != fingerprint)
            throw new InvalidOperationException("The retained sprint scope or staffing changed. Reconcile its existing plan explicitly; preparation cannot replace it.");
        var branch = "csweet/sprint/" + sprintId.ToString("N") + "/" + fingerprint[..24];
        var allPlans = await context.Platform.Work.ReadDeliveryPlansAsync(new(projectId), ct);
        // Recover a lost configure/state response through the same immutable, fingerprinted binding.
        var recoverable = allPlans.Where(p => p.ManagerOrganizationUserId == manager && p.Branches.Count == 1 &&
            p.Branches[0].RepositoryId == repository.RepositoryId && p.Branches[0].SourceBranch == branch &&
            p.Branches[0].TargetBranch == branch && p.Scopes.Count == 1 &&
            p.Scopes[0].ChildIds.Order().SequenceEqual(tasks.Select(t => t.Id).Order())).SingleOrDefault();
        if (allPlans.Any(p => p.Status is not ("Completed" or "Cancelled") &&
            p.Scopes.Any(s => s.ChildIds.Intersect(tasks.Select(t => t.Id)).Any()) &&
            p.Id != state?.Payload.PlanId && p.Id != recoverable?.Id))
            throw new InvalidOperationException("A selected ticket already belongs to another delivery plan. Reconcile that plan first.");
        WorkDeliveryPlanResponse plan;
        Guid workflowId;
        if (state is null)
        {
            if (tasks.Any(x => x.Delivery?.DeliveryPlanId is { } priorPlan && priorPlan != recoverable?.Id))
                throw new InvalidOperationException("Existing delivery bindings require their retained plan; they cannot be replaced.");
            var step = new WorkDeliveryTicketStep("confirm-evidence", "completion",
                new("confirm-evidence", "AgentInstallation", manager, installation),
                "Confirm predecessor implementation tickets completed their declared execution. This mechanical step grants no product acceptance, promotion or deployment.");
            var planning = new WorkItemPlanningSpecification(
                ["Retain the selected sprint's accepted requirements, independent review and quality evidence."],
                ["Every declared implementation ticket completed its current governed execution."])
            {
                DependencyItemIds = tasks.Select(x => x.Id).ToArray(),
                Workflow = new([step]) { EvidenceItemIds = tasks.Select(x => x.Id).ToArray() }
            };
            var ticket = await context.Platform.Work.CreateItemAsync(new(boardId,
                (sprint.Name.Length > 110 ? sprint.Name[..110] : sprint.Name) + " — evidence",
                "Wait for the explicitly selected implementation tickets; preserve their independent reviews. This ticket does not authorize deployment or default-branch promotion.",
                "Task", "Medium", null, null, null, "pm-sprint-evidence:" + sprintId.ToString("N") + ":" + fingerprint[..16])
            { TypeKey = WorkItemTypeKeys.WorkflowTicketV1, Planning = planning, AccountableOrganizationUserId = manager }, ct);
            workflowId = ticket.Id;
            plan = await context.Platform.Work.ConfigureDeliveryPlanAsync(new(projectId,
                (sprint.Name.Length > 140 ? sprint.Name[..140] : sprint.Name), manager, [],
                [new(repository.RepositoryId, WorkExecutionScopes.Ticket, workflowId, branch, branch)
                    { TargetBaseBranch = repository.DefaultBranch }], [], "pm-sprint-plan:" + sprintId.ToString("N") + ":" + fingerprint[..16])
            {
                TicketWorkflows = [new(workflowId, [step]) { EvidenceItemIds = tasks.Select(x => x.Id).ToArray() }],
                TaskAssignments = tasks.Select(x => new WorkDeliveryTaskAssignment(x.Id, x.StageAssignments)).ToArray()
            }, ct);
            await context.Platform.WriteOperatingStateAsync(new WriteAgentOperatingStateRequest<Receipt>(key,
                "product-manager.sprint-delivery.v1", 1, "Prepared", new Dictionary<string, string>(), [], fingerprint, [],
                Guid.NewGuid(), new(plan.Id, workflowId, fingerprint), null, "pm-sprint-receipt:" + plan.Id.ToString("N")), ct);
        }
        else
        {
            workflowId = state.Payload.WorkflowTicketId;
            plan = allPlans.SingleOrDefault(x => x.Id == state.Payload.PlanId)
                ?? throw new InvalidOperationException("The retained delivery plan is unavailable; restore access instead of creating a replacement.");
        }
        if (state is not null && recoverable?.Id != plan.Id)
            throw new InvalidOperationException("The current delivery plan no longer matches the retained manager, repository and selected sprint inputs.");
        if (plan.Status == "Active") return Prepared();
        if (plan.Status != "Draft") throw new InvalidOperationException("The retained plan is stopped or completed; use its governed control or amendment flow.");
        foreach (var original in tasks)
        {
            var current = await context.Platform.Work.ReadItemAsync(new(boardId, original.Id), ct);
            if (current.PlanningRevision != original.PlanningRevision || current.SprintId != sprintId ||
                current.Status != WorkStatuses.Ready || current.AccountableOrganizationUserId != original.AccountableOrganizationUserId ||
                JsonSerializer.Serialize(current.StageAssignments.OrderBy(x => x.StageKey), Json) != JsonSerializer.Serialize(original.StageAssignments.OrderBy(x => x.StageKey), Json))
                throw new InvalidOperationException("A selected ticket changed during preparation. Re-read current scope before continuing.");
            if (current.Delivery?.DeliveryPlanId == plan.Id) continue;
            if (current.Delivery?.DeliveryPlanId is not null) throw new InvalidOperationException("A selected task acquired another delivery binding.");
            var accepted = current.Planning!;
            await context.Platform.Work.FinalizeItemDeliveryAsync(new(boardId, current.Id,
                new(repository.RepositoryId, accepted.Requirements, accepted.AcceptanceCriteria, accepted.Constraints)
                { BaseBranch = branch, DeliveryPlanId = plan.Id, DependencyItemIds = accepted.DependencyItemIds },
                current.AccountableOrganizationUserId!.Value, current.StageAssignments, current.Revision,
                $"pm-sprint-finalize:{plan.Id:N}:{current.Id:N}:{current.Revision}"), ct);
        }
        plan = (await context.Platform.Work.ReadDeliveryPlansAsync(new(projectId, plan.Id), ct)).Single();
        if (plan.Status == "Draft")
            plan = await context.Platform.Work.ControlDeliveryPlanAsync(new(plan.Id, plan.Revision, "activate",
                $"pm-sprint-activate:{plan.Id:N}:{plan.Revision}"), ct);
        if (plan.Status != "Active") throw new InvalidOperationException("The platform did not activate the prepared delivery plan.");
        return Prepared();

        Result Prepared() => new(plan.Id, workflowId, plan.Status, branch,
            "Delivery is prepared on its isolated sprint branch. The sprint remains Planned; its current preflight and explicit start are still required. No default-branch promotion or deployment is authorized.");
    }

    internal static void ValidateStageAssignments(WorkItem task, AgentTeamContext roster)
    {
        static bool PlatformIntegration(WorkStageAssignment stage) => stage.PrincipalKind == "PlatformAction" &&
            stage.StageKey == "task-integration" && stage.PlatformAction == HierarchicalWorkflows.TaskIntegrationAction &&
            stage.OrganizationUserId is null && stage.AgentInstallationId is null;
        if (task.StageAssignments.Any(x => x.PrincipalKind is not ("Human" or "AgentInstallation") && !PlatformIntegration(x)) ||
            task.StageAssignments.GroupBy(x => x.StageKey).Any(x => x.Count() != 1))
            throw new InvalidOperationException("Retain one exact supported principal per assigned stage.");
        var author = task.StageAssignments.SingleOrDefault(x => x.StageKey is "development" or "specialist-execution");
        if (author?.OrganizationUserId is null || !task.StageAssignments.Any(x => x.StageKey == "technical-review") ||
            !task.StageAssignments.Any(x => x.StageKey == "quality"))
            throw new InvalidOperationException("Code preparation requires an exact author and the existing independent technical and quality reviewers.");
        foreach (var principal in task.StageAssignments.Where(x => !PlatformIntegration(x)))
        {
            var member = roster.Members.SingleOrDefault(x => x.EmployeeId == principal.OrganizationUserId?.ToString() && x.IsAvailable);
            if (member is null || (principal.PrincipalKind == "Human"
                    ? member.EmployeeType != "Human" || principal.AgentInstallationId is not null
                    : member.AgentInstallationId != principal.AgentInstallationId || !principal.AgentInstallationId.HasValue ||
                      !member.EffectiveCapabilities.Contains(WorkManagementCapabilityNames.ExecutionRun)))
                throw new InvalidOperationException("Retain an exact available team employee and installation for every assigned stage; resolve staffing before preparing a plan.");
            if (principal.StageKey is "technical-review" or "quality" && principal.OrganizationUserId == author.OrganizationUserId)
                throw new InvalidOperationException("The implementation author cannot perform independent technical review or quality review.");
        }
    }
}
