using System.Text.Json;
using CSweet.Agent.SDK;
using CSweet.WorkManagement.Contracts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CSweet.Agent.SoftwareProductManager.Tests;

public sealed class SprintDeliveryPreparationTests
{
    [Fact]
    public async Task Current_platform_eligibility_denial_cannot_be_replaced_by_retained_roster_membership()
    {
        var f = new Fixture(); var original = f.Items[0];
        f.Items[0] = original with { Planning = null, Development = new(f.Repositories[0].RepositoryId, "certified",
            original.Planning!.Requirements, original.Planning.AcceptanceCriteria, original.Planning.Constraints) };
        f.Runtime.RegisterCapability<ReviseWorkItemPlanningRequest, WorkItem>(WorkItemCapabilities.RevisePlanning,
            (_, _) => throw new PlatformCapabilityException(WorkItemCapabilities.RevisePlanning,
                PlatformCapabilityErrorCode.Denied, "The retained installation is not currently eligible."));
        var result = await ProductManagerAgent.RunDeliveryToolAsync(() => LegacyBriefPlanning.AdoptAsync(
            f.Board.Id, original.Id, original.Revision, f.Context, default));
        Assert.False(result.Succeeded); Assert.Equal("Denied", result.ErrorCode); Assert.Null(result.Value);
        Assert.Null(f.Items[0].Planning); Assert.Equal(0, f.PlanningWrites); Assert.Equal(0, f.ConfiguredPlans);
    }

    [Fact]
    public async Task Delivery_tool_returns_actionable_denials_without_a_success_receipt_and_propagates_cancellation()
    {
        var f = new Fixture();
        var tool = ProductManagerAgent.DeliveryTools(f.Context).Cast<AIFunction>().Single(x => x.Name == "adopt_developer_brief");
        var output = Assert.IsType<JsonElement>(await tool.InvokeAsync(new AIFunctionArguments
        {
            ["boardId"] = f.Board.Id.ToString(), ["itemId"] = f.Items[0].Id.ToString(), ["expectedRevision"] = 0L
        }));
        var blocked = output.Deserialize<ProductManagerAgent.DeliveryToolResult<WorkItem>>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.False(blocked.Succeeded); Assert.Null(blocked.Value);
        Assert.Equal("Precondition", blocked.ErrorCode); Assert.Contains("current Ready", blocked.Error);
        Assert.Equal(0, f.PlanningWrites);
        var denied = await ProductManagerAgent.RunDeliveryToolAsync<WorkItem>(() => throw new PlatformCapabilityException(
            WorkItemCapabilities.RevisePlanning, PlatformCapabilityErrorCode.Denied, "Current assignment no longer authorizes planning."));
        Assert.False(denied.Succeeded); Assert.Equal("Denied", denied.ErrorCode); Assert.Null(denied.Value);
        await Assert.ThrowsAsync<OperationCanceledException>(() => ProductManagerAgent.RunDeliveryToolAsync<WorkItem>(() => throw new OperationCanceledException()));
    }

    [Theory]
    [InlineData("action")]
    [InlineData("stage")]
    [InlineData("employee")]
    [InlineData("installation")]
    [InlineData("duplicate")]
    public async Task Unknown_or_impersonated_platform_principal_cannot_pass_delivery_validation(string scenario)
    {
        var f = new Fixture(); var item = f.Items[0];
        var platform = item.StageAssignments.Single(x => x.PrincipalKind == "PlatformAction");
        var changed = scenario switch
        {
            "action" => platform with { PlatformAction = "arbitrary.execute" },
            "stage" => platform with { StageKey = "technical-review" },
            "employee" => platform with { OrganizationUserId = f.Reviewer },
            "installation" => platform with { AgentInstallationId = f.Installation },
            _ => platform
        };
        f.Items[0] = item with { StageAssignments = scenario == "duplicate" ? item.StageAssignments.Append(platform).ToArray() :
            item.StageAssignments.Select(x => x == platform ? changed : x).ToArray() };
        await Assert.ThrowsAsync<InvalidOperationException>(f.Prepare);
        Assert.Equal(0, f.CreatedTickets); Assert.Equal(0, f.ConfiguredPlans); Assert.Equal(0, f.Activations);
    }

    [Fact]
    public async Task Chat_tools_exclude_platform_hidden_operations_and_keep_exact_visible_binding_checks()
    {
        var runtime = new AgentTestRuntime();
        foreach (var capability in ProductManagerAgent.ProductModelCapabilities)
            runtime.RegisterCapability<JsonElement, JsonElement>(capability, (r, _) => Task.FromResult(r), modelVisible: true);
        foreach (var capability in new[] { WorkItemCapabilities.RevisePlanning, WorkItemCapabilities.FinalizeDelivery,
            CommunicationCapabilities.ChatRead, CommunicationCapabilities.CoordinationRead,
            PlatformCapabilities.ProjectAssignmentRead, PlatformCapabilities.WorkstreamRead })
            runtime.RegisterCapability<JsonElement, JsonElement>(capability, (r, _) => Task.FromResult(r), modelVisible: false);
        var tools = await ProductManagerAgent.ProductModelToolsAsync(runtime.CreateContext(), default);
        Assert.Equal(ProductManagerAgent.ProductModelCapabilities.Length, tools.Count);
        // Reproduce the former setup failure rather than weakening platform visibility.
        await Assert.ThrowsAsync<PlatformCapabilityException>(() => runtime.CreateContext().GetModelToolsAsync(
            ProductManagerAgent.ProductModelCapabilities.Concat([WorkItemCapabilities.RevisePlanning]).ToArray()));
        runtime.RegisterCapability<JsonElement, JsonElement>(WorkItemCapabilities.Read, (r, _) => Task.FromResult(r), modelVisible: false);
        await Assert.ThrowsAsync<PlatformCapabilityException>(() => ProductManagerAgent.ProductModelToolsAsync(runtime.CreateContext(), default));
    }

    [Fact]
    public async Task Adopts_only_the_retained_brief_and_exact_stage_principals_then_prepares_delivery()
    {
        var f = new Fixture(); var original = f.Items[0];
        var brief = new SoftwareDevelopmentBrief(f.Repositories[0].RepositoryId, "software-development-polyglot-v1",
            original.Planning!.Requirements, original.Planning.AcceptanceCriteria, original.Planning.Constraints);
        f.Items[0] = original with { Planning = null, Development = brief,
            StageAssignments = original.StageAssignments.Select(x => x.PrincipalKind == "AgentInstallation" ? x with { OrganizationUserId = null } : x).ToArray() };
        var tool = ProductManagerAgent.DeliveryTools(f.Context).Cast<AIFunction>().Single(x => x.Name == "adopt_developer_brief");
        var output = Assert.IsType<JsonElement>(await tool.InvokeAsync(new AIFunctionArguments
        {
            ["boardId"] = f.Board.Id.ToString(), ["itemId"] = original.Id.ToString(),
            ["expectedRevision"] = original.Revision
        }));
        var receipt = output.Deserialize<ProductManagerAgent.DeliveryToolResult<WorkItem>>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.True(receipt.Succeeded, receipt.Error);
        var result = Assert.IsType<WorkItem>(receipt.Value);
        Assert.Equal(brief.Requirements, result.Planning!.Requirements);
        Assert.Equal(brief.AcceptanceCriteria, result.Planning.AcceptanceCriteria);
        Assert.Equal(brief.Constraints, result.Planning.Constraints);
        Assert.Equal(original.AccountableOrganizationUserId, result.AccountableOrganizationUserId);
        Assert.Equal(original.StageAssignments, result.StageAssignments);
        Assert.Equal(1, f.PlanningWrites);
        await Assert.ThrowsAsync<InvalidOperationException>(() => LegacyBriefPlanning.AdoptAsync(f.Board.Id, original.Id, result.Revision, f.Context, default));
        Assert.Equal(1, f.PlanningWrites);
        Assert.Equal("Active", (await f.Prepare()).Status); Assert.Equal(0, f.Starts);
    }

    [Theory]
    [InlineData("revision")]
    [InlineData("manager")]
    [InlineData("sprint")]
    [InlineData("brief")]
    [InlineData("quality-gate")]
    [InlineData("principal")]
    [InlineData("owner")]
    public async Task Adoption_rejects_stale_or_incomplete_bindings_before_any_write(string scenario)
    {
        var f = new Fixture(); var original = f.Items[0];
        f.Items[0] = original with { Planning = null, Development = new(f.Repositories[0].RepositoryId, "certified",
            ["Retain scope"], ["Independent review passes"]) };
        var revision = original.Revision;
        switch (scenario)
        {
            case "revision": revision--; break;
            case "manager": f.Board = f.Board with { ManagerOrganizationUserId = Guid.NewGuid() }; break;
            case "sprint": f.Sprint = f.Sprint with { Status = "Active" }; break;
            case "brief": f.Items[0] = f.Items[0] with { Development = null }; break;
            case "quality-gate": f.Items[0] = f.Items[0] with { Development = f.Items[0].Development! with { QualityGateColumnId = Guid.NewGuid() } }; break;
            case "principal": f.Items[0] = f.Items[0] with { StageAssignments = original.StageAssignments.Select(x => x.PrincipalKind == "AgentInstallation" ? x with { AgentInstallationId = Guid.NewGuid(), OrganizationUserId = null } : x).ToArray() }; break;
            case "owner": f.Items[0] = f.Items[0] with { AccountableOrganizationUserId = Guid.NewGuid() }; break;
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => LegacyBriefPlanning.AdoptAsync(f.Board.Id, original.Id, revision, f.Context, default));
        Assert.Equal(0, f.PlanningWrites); Assert.Equal(0, f.ConfiguredPlans); Assert.Equal(0, f.Starts);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(PlatformCapabilities.ProjectAssignmentRead)]
    [InlineData(PlatformCapabilities.WorkstreamRead)]
    public async Task Packaged_grants_enable_existing_project_delivery_and_revocation_fails_before_mutation(string? revokedCapability)
    {
        var f = new Fixture(revokedCapability);
        async Task Run()
        {
            var assignment = await f.Context.Platform.ReadProjectAssignmentAsync();
            Assert.NotNull(assignment);
            var identity = f.Context.Identity! with { AssignedProject = assignment };
            var context = f.Runtime.CreateContext(Guid.NewGuid().ToString(), f.Installation.ToString(), identity);
            var agent = new ProductManagerAgent(NullLogger<ProductManagerAgent>.Instance,
                new ProductManagerOrchestrator(NullLogger<ProductManagerOrchestrator>.Instance));
            var incoming = new CommunicationMessageReceivedEvent(Guid.NewGuid(), Guid.NewGuid().ToString(), f.Reviewer.ToString(),
                "Prepare the existing sprint.", null);
            Assert.False(await agent.TryWakeStaffingCommitmentAsync(incoming, context, default));
            var prepared = await SprintDeliveryPreparation.PrepareAsync(f.Board.Id, f.Sprint.Id, context, default);
            Assert.Equal("Active", prepared.Status);
        }
        if (revokedCapability is null)
            await Run();
        else
        {
            var error = await Assert.ThrowsAsync<PlatformCapabilityException>(Run);
            Assert.Equal(PlatformCapabilityErrorCode.Denied, error.Code);
            Assert.Equal(revokedCapability, error.Capability);
            Assert.Equal(0, f.CreatedTickets); Assert.Equal(0, f.ConfiguredPlans); Assert.Equal(0, f.Activations);
        }
        Assert.Equal(0, f.Starts);
    }

    [Fact]
    public async Task Prepares_only_the_selected_sprint_and_retains_its_exact_reviewers_without_start_or_promotion()
    {
        var f = new Fixture();
        var result = await f.Prepare();
        Assert.Equal("Active", result.Status);
        Assert.Equal(2, f.Finalizations.Count);
        Assert.DoesNotContain(f.Finalizations, x => x.ItemId == f.Outside.Id);
        Assert.All(f.Finalizations, r => {
            var original = f.Items.Single(x => x.Id == r.ItemId);
            Assert.Equal(original.Planning!.AcceptanceCriteria, r.Delivery.AcceptanceCriteria);
            Assert.Equal(original.StageAssignments, r.StageAssignments);
            Assert.Equal(result.PlanId, r.Delivery.DeliveryPlanId);
            Assert.NotEqual("main", r.Delivery.BaseBranch);
        });
        var plan = Assert.Single(f.Plans);
        var declaration = Assert.Single(plan.Scopes).TicketWorkflow!;
        Assert.False(declaration.PromoteOnCompletion);
        Assert.Equal(f.Items.Where(x => x.SprintId == f.Sprint.Id).Select(x => x.Id).Order(), declaration.EvidenceItemIds.Order());
        Assert.Equal(1, f.CreatedTickets); Assert.Equal(1, f.ConfiguredPlans); Assert.Equal(1, f.Activations);
        await f.Prepare(); // Active receipt replay never replaces or reactivates the plan.
        Assert.Equal(2, f.Finalizations.Count); Assert.Equal(1, f.Activations);
        Assert.Equal(0, f.Starts);
    }

    [Theory]
    [InlineData("state")]
    [InlineData("state-after")]
    [InlineData("configure")]
    [InlineData("finalize")]
    [InlineData("activate")]
    public async Task Restarts_after_lost_effect_responses_without_duplicate_tickets_plans_or_finalization(string boundary)
    {
        var f = new Fixture { LoseResponse = boundary };
        await Assert.ThrowsAsync<InvalidOperationException>(f.Prepare);
        var result = await f.Prepare();
        Assert.Equal("Active", result.Status);
        Assert.Equal(1, f.CreatedTickets); Assert.Equal(1, f.ConfiguredPlans);
        Assert.Equal(2, f.Finalizations.Count); Assert.Equal(1, f.Activations);
    }

    [Theory]
    [InlineData("board-manager")]
    [InlineData("project-manager")]
    [InlineData("project-state")]
    [InlineData("sprint-state")]
    [InlineData("planning")]
    [InlineData("task-state")]
    [InlineData("owner")]
    [InlineData("artifact")]
    [InlineData("repository-choice")]
    [InlineData("existing-plan")]
    [InlineData("missing-employee")]
    [InlineData("self-review")]
    [InlineData("revoked-member")]
    public async Task Missing_authority_scope_or_repository_choice_blocks_before_any_mutation(string scenario)
    {
        var f = new Fixture();
        switch (scenario)
        {
            case "board-manager": f.Board = f.Board with { ManagerOrganizationUserId = Guid.NewGuid() }; break;
            case "project-manager": f.Project = f.Project with { AccountableManagerOrganizationUserId = Guid.NewGuid() }; break;
            case "project-state": f.Project = f.Project with { Status = "Proposed" }; break;
            case "sprint-state": f.Sprint = f.Sprint with { Status = "Active" }; break;
            case "planning": f.Items[0] = f.Items[0] with { Planning = null }; break;
            case "task-state": f.Items[0] = f.Items[0] with { Status = "Blocked" }; break;
            case "owner": f.Items[0] = f.Items[0] with { AccountableOrganizationUserId = null }; break;
            case "artifact": f.Items[0] = f.Items[0] with { Planning = f.Items[0].Planning! with { DeliveryKind = "Artifact" } }; break;
            case "repository-choice": f.Repositories.Add(f.Repositories[0] with { RepositoryId = Guid.NewGuid() }); break;
            case "existing-plan": f.Items[0] = f.Items[0] with { Delivery = new(f.Repositories[0].RepositoryId, ["R"], ["A"]) { DeliveryPlanId = Guid.NewGuid() } }; break;
            case "missing-employee": f.Items[0] = f.Items[0] with { StageAssignments = f.Items[0].StageAssignments.Select(x => x.StageKey == "development" ? x with { OrganizationUserId = null } : x).ToArray() }; break;
            case "self-review": f.Items[0] = f.Items[0] with { StageAssignments = f.Items[0].StageAssignments.Select(x => x.StageKey == "quality" ? x with { OrganizationUserId = f.Items[0].StageAssignments[0].OrganizationUserId, PrincipalKind = "AgentInstallation", AgentInstallationId = f.Items[0].StageAssignments[0].AgentInstallationId } : x).ToArray() }; break;
            case "revoked-member": f.Roster = f.Roster with { Members = f.Roster.Members.Where(x => x.EmployeeId != f.Items[0].StageAssignments[0].OrganizationUserId.ToString()).ToArray() }; break;
        }
        await Assert.ThrowsAsync<InvalidOperationException>(f.Prepare);
        Assert.Equal(0, f.CreatedTickets); Assert.Equal(0, f.ConfiguredPlans); Assert.Empty(f.Finalizations); Assert.Equal(0, f.Activations);
    }

    [Fact]
    public async Task Changed_accepted_scope_retains_the_draft_and_requires_explicit_reconciliation()
    {
        var f = new Fixture { LoseResponse = "finalize" };
        await Assert.ThrowsAsync<InvalidOperationException>(f.Prepare);
        f.Items[1] = f.Items[1] with { PlanningRevision = 2, Planning = new(["Changed requirement"], ["Changed criterion"]) };
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(f.Prepare);
        Assert.Contains("changed", failure.Message);
        Assert.Equal("Draft", Assert.Single(f.Plans).Status); Assert.Equal(1, f.ConfiguredPlans); Assert.Equal(0, f.Activations);
    }

    [Fact]
    public async Task Denied_finalization_retains_the_draft_and_never_activates_or_starts()
    {
        var f = new Fixture { DenyFinalize = true };
        await Assert.ThrowsAsync<PlatformCapabilityException>(f.Prepare);
        Assert.Equal("Draft", Assert.Single(f.Plans).Status); Assert.Equal(0, f.Activations);
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    [InlineData("Paused")]
    public async Task Old_readiness_callback_cannot_start_a_successor_sprint(string status)
    {
        var f = new Fixture();
        var successor = f.Sprint with { Id = Guid.NewGuid(), Sequence = 2 };
        f.Grant<WorkBoardReference, IReadOnlyList<WorkSprint>>(WorkSprintCapabilities.Read,
            (_, _) => Task.FromResult<IReadOnlyList<WorkSprint>>([f.Sprint with { Status = status }, successor]));
        var item = Empty<PersonalTodoItem>() with { Id = Guid.NewGuid(), CorrelationId = $"product-sprint-readiness:{f.Board.Id:N}:{f.Sprint.Id:N}" };
        var agent = new ProductManagerAgent(NullLogger<ProductManagerAgent>.Instance, new(NullLogger<ProductManagerOrchestrator>.Instance));
        var result = await agent.HandlePersonalTodoAsync(item, f.Context, default);
        Assert.Contains("already " + status, (string)typeof(PersonalTodoResult).GetProperty("Content", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(result)!);
        Assert.Equal(0, f.Starts);
    }

    [Fact]
    public void Preparation_tools_do_not_expose_arbitrary_delivery_acceptance_or_transport()
    {
        var names = ProductManagerAgent.DeliveryTools(new Fixture().Context).Cast<AIFunction>().Select(x => x.Name).ToArray();
        Assert.Equal(["adopt_developer_brief", "prepare_sprint_delivery", "read_project_delivery", "read_ticket_discussion"], names);
    }

    [Fact]
    public async Task Discussion_tool_repeats_current_board_management_and_pagination_checks()
    {
        var f = new Fixture(); var reads = 0;
        f.Grant<ReadWorkItemCommentsRequest, WorkItemCommentPage>(WorkItemCapabilities.ReadComments, (r, _) => {
            reads++; Assert.Equal(20, r.PageSize); Assert.Equal(f.Items[0].Id, r.ItemId);
            return Task.FromResult(new WorkItemCommentPage([], r.Page, r.PageSize, false, 1)); });
        var tool = ProductManagerAgent.DeliveryTools(f.Context).Cast<AIFunction>().Single(x => x.Name == "read_ticket_discussion");
        var args = new AIFunctionArguments { ["boardId"] = f.Board.Id, ["itemId"] = f.Items[0].Id, ["page"] = 1 };
        await tool.InvokeAsync(args); Assert.Equal(1, reads);
        f.Board = f.Board with { ManagerOrganizationUserId = Guid.NewGuid() };
        await Assert.ThrowsAnyAsync<Exception>(async () => await tool.InvokeAsync(args)); Assert.Equal(1, reads);
        args["page"] = 51;
        await Assert.ThrowsAnyAsync<Exception>(async () => await tool.InvokeAsync(args)); Assert.Equal(1, reads);
    }

    [Theory]
    [InlineData("current", true)]
    [InlineData("board-manager", false)]
    [InlineData("project-manager", false)]
    [InlineData("project-binding", false)]
    [InlineData("archived", false)]
    public async Task Existing_assignment_is_reconciled_without_replacing_its_team_or_creating_another_board(string scenario, bool expected)
    {
        var f = new Fixture();
        var identity = f.Context.Identity! with { AssignedProject = new(f.Project.Id, "Existing project", f.Board.TeamId, f.Board.Id, "manager", 1, DateTimeOffset.UtcNow) };
        var context = f.Runtime.CreateContext(Guid.NewGuid().ToString(), f.Installation.ToString(), identity);
        if (scenario == "board-manager") f.Board = f.Board with { ManagerOrganizationUserId = Guid.NewGuid() };
        if (scenario == "project-manager") f.Project = f.Project with { AccountableManagerOrganizationUserId = Guid.NewGuid() };
        if (scenario == "project-binding") f.Board = f.Board with { WorkstreamId = Guid.NewGuid() };
        if (scenario == "archived") f.Board = f.Board with { IsArchived = true };
        Assert.True(await ProductManagerAgent.ReconcileAssignedProjectAsync(context, default));
        Assert.Equal(expected ? 1 : 0, f.PlanReads);
        Assert.Equal(0, f.CreatedTickets); Assert.Equal(0, f.ConfiguredPlans); Assert.Equal(0, f.Starts);
    }

    [Fact]
    public async Task Cancelled_preparation_cannot_begin_a_mutation()
    {
        var f = new Fixture(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SprintDeliveryPreparation.PrepareAsync(f.Board.Id, f.Sprint.Id, f.Context, cancellation.Token));
        Assert.Equal(0, f.CreatedTickets); Assert.Empty(f.Plans);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retained_project_chat_is_not_intercepted_by_initial_staffing(bool staleManagement)
    {
        var f = new Fixture();
        var identity = f.Context.Identity! with { AssignedProject = new(f.Project.Id, "Existing project", f.Board.TeamId, f.Board.Id, "manager", 1, DateTimeOffset.UtcNow) };
        if (staleManagement) f.Board = f.Board with { ManagerOrganizationUserId = Guid.NewGuid() };
        var context = f.Runtime.CreateContext(Guid.NewGuid().ToString(), f.Installation.ToString(), identity);
        var agent = new ProductManagerAgent(Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductManagerAgent>.Instance,
            new ProductManagerOrchestrator(Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductManagerOrchestrator>.Instance));
        var incoming = new CommunicationMessageReceivedEvent(Guid.NewGuid(), Guid.NewGuid().ToString(), f.Reviewer.ToString(),
            "Prepare only the existing sprint. Do not propose a replacement team.",
            new Dictionary<string, string> {
                [CommunicationMessageContextKeys.SenderEmployeeType] = "Human",
                [CommunicationMessageContextKeys.SenderOrganizationUserId] = f.Reviewer.ToString() });
        // No organization, staffing, personal-queue or requeue capability is registered.
        // Even a stale assignment must reach current authorization checks rather than initial hiring.
        Assert.False(await agent.TryWakeStaffingCommitmentAsync(incoming, context, default));
        Assert.Equal(0, f.CreatedTickets); Assert.Equal(0, f.ConfiguredPlans);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Old_initial_staffing_callback_retires_after_project_assignment(bool staleManagement)
    {
        var f = new Fixture();
        var identity = f.Context.Identity! with { AssignedProject = new(f.Project.Id, "Existing project", f.Board.TeamId, f.Board.Id, "manager", 1, DateTimeOffset.UtcNow) };
        if (staleManagement) f.Board = f.Board with { ManagerOrganizationUserId = Guid.NewGuid() };
        var context = f.Runtime.CreateContext(Guid.NewGuid().ToString(), f.Installation.ToString(), identity);
        var agent = new ProductManagerAgent(Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductManagerAgent>.Instance,
            new ProductManagerOrchestrator(Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductManagerOrchestrator>.Instance));
        var item = Empty<PersonalTodoItem>() with { Id = Guid.NewGuid(), CorrelationId = $"product-team-staffing:{f.Installation:N}" };
        var result = await agent.HandlePersonalTodoAsync(item, context, default);
        Assert.Equal(PersonalTodoResult.Completed(
            "Initial team design is superseded by the retained project assignment. Current management checks were reported; no replacement team was proposed."), result);
        Assert.Equal(staleManagement ? 0 : 1, f.PlanReads);
        Assert.Equal(0, f.CreatedTickets); Assert.Equal(0, f.ConfiguredPlans); Assert.Equal(0, f.Starts);
        // The normal personal-task result closes the obsolete commitment. No staffing mutation is registered.
    }

    private static T Empty<T>() => JsonSerializer.Deserialize<T>("{}")!;

    private sealed class Fixture
    {
        internal AgentTestRuntime Runtime = new();
        internal Guid Manager = Guid.NewGuid(), Installation = Guid.NewGuid(), Reviewer = Guid.NewGuid();
        internal AgentTeamContext Roster;
        internal WorkBoardSummary Board;
        internal WorkstreamDetail Project;
        internal WorkSprint Sprint;
        internal List<WorkItem> Items = [];
        internal WorkItem Outside;
        internal List<TeamRepositoryOption> Repositories = [];
        internal List<WorkDeliveryPlanResponse> Plans = [];
        internal List<FinalizeWorkItemDeliveryRequest> Finalizations = [];
        internal Dictionary<string, AgentOperatingStateResponse> States = [];
        internal AgentRuntimeContext Context;
        internal int CreatedTickets, ConfiguredPlans, Activations, Starts, PlanReads, PlanningWrites;
        internal string? LoseResponse;
        internal bool DenyFinalize;
        private readonly Dictionary<string, WorkItem> _tickets = [];
        private readonly HashSet<string> _requested;
        internal void Grant<TRequest, TResponse>(string capability, Func<TRequest, CancellationToken, Task<TResponse>> handler)
        {
            if (_requested.Contains(capability)) Runtime.RegisterCapability(capability, handler);
        }

        internal Fixture(string? revokedCapability = null)
        {
            var manifestDirectory = new DirectoryInfo(AppContext.BaseDirectory);
            while (manifestDirectory is not null && !File.Exists(Path.Combine(manifestDirectory.FullName, "csweet-plugin.json"))) manifestDirectory = manifestDirectory.Parent;
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(manifestDirectory!.FullName, "csweet-plugin.json")));
            _requested = manifest.RootElement.GetProperty("requires").EnumerateArray().Select(x => x.GetProperty("name").GetString()!).ToHashSet();
            if (revokedCapability is not null) _requested.Remove(revokedCapability);
            Board = new(Guid.NewGuid(), "PM", "Sprint", false, false, 1, [])
            { ManagerOrganizationUserId = Manager, TeamId = Guid.NewGuid(), WorkstreamId = Guid.NewGuid() };
            Project = Empty<WorkstreamDetail>() with { Id = Board.WorkstreamId!.Value, AccountableManagerOrganizationUserId = Manager, Status = "Active" };
            Sprint = Empty<WorkSprint>() with { Id = Guid.NewGuid(), BoardId = Board.Id, Name = "Bounded sprint", Status = "Planned", Revision = 1, Sequence = 1 };
            WorkItem Item(Guid? sprint) => Empty<WorkItem>() with { Id = Guid.NewGuid(), SprintId = sprint,
                Title = "Current task", Status = "Ready", Kind = "Task", TypeKey = "software.task.v1", ExecutionMode = "Executable",
                Revision = 1, PlanningRevision = 1, AccountableOrganizationUserId = Manager,
                Planning = new(["Retained requirement"], ["Retained criterion"], ["No deployment"]),
                StageAssignments = [new("development", "AgentInstallation", Guid.NewGuid(), Guid.NewGuid()),
                    new("technical-review", "Human", Reviewer), new("quality", "Human", Reviewer),
                    new("task-integration", "PlatformAction", PlatformAction: "source-control.task.integrate.v1")] };
            Items.AddRange([Item(Sprint.Id), Item(Sprint.Id)]); Outside = Item(null); Items.Add(Outside);
            var members = Items.SelectMany(x => x.StageAssignments).Where(x => x.PrincipalKind == "AgentInstallation")
                .Select(x => new AgentTeammate(x.OrganizationUserId!.Value.ToString(), "Developer", "Agent", null, null, "Report", "Online")
                    // The live roster exposes platform grants, not the agent's provided callbacks.
                    { AgentInstallationId = x.AgentInstallationId, EffectiveCapabilities = [WorkItemCapabilities.Read, WorkItemCapabilities.Comment] }).ToList();
            members.Add(new(Reviewer.ToString(), "Reviewer", "Human", null, null, "Peer", "Online"));
            members.Add(new(Manager.ToString(), "Maya", "Agent", null, null, "Self", "Online") { AgentInstallationId = Installation });
            Roster = new(Board.TeamId!.Value.ToString(), "team", "Team", 1, Manager.ToString(), "Maya", members, [], members.Count, false);
            Repositories.Add(new(Guid.NewGuid(), "Source", "InternalGit", "source", "main", "PullRequest"));
            Context = Runtime.CreateContext(Guid.NewGuid().ToString(), Installation.ToString(), new(Manager.ToString(), "Maya", null, "Product Manager", null, [], null, Guid.NewGuid().ToString(), "Owner"));
            Grant<JsonElement, JsonElement>(WorkItemCapabilities.Read, (r, _) => Task.FromResult(
                r.TryGetProperty("itemId", out var id) ? JsonSerializer.SerializeToElement(Items.Single(x => x.Id == id.GetGuid())) :
                JsonSerializer.SerializeToElement(new WorkBoardDetail(Board, [], Items))));
            Grant<object, ProjectAssignmentResponse>(PlatformCapabilities.ProjectAssignmentRead, (_, _) => Task.FromResult(new ProjectAssignmentResponse(
                new(Project.Id, "Existing project", Board.TeamId, Board.Id, "Manager", 1, DateTimeOffset.UtcNow))));
            Grant<ReadWorkstreamRequest, WorkstreamDetail>(PlatformCapabilities.WorkstreamRead, (_, _) => Task.FromResult(Project));
            Grant<TeamRosterRequest, TeamRosterResponse>(PlatformCapabilities.TeamRosterRead, (_, _) => Task.FromResult(new TeamRosterResponse(Roster)));
            Grant<WorkBoardReference, IReadOnlyList<WorkSprint>>(WorkSprintCapabilities.Read, (_, _) => Task.FromResult<IReadOnlyList<WorkSprint>>([Sprint]));
            Grant<TeamRepositoryOptionsRequest, IReadOnlyList<TeamRepositoryOption>>(SourceControlCapabilities.TeamRepositoryOptions, (_, _) => Task.FromResult<IReadOnlyList<TeamRepositoryOption>>(Repositories));
            Grant<ReadWorkDeliveryPlansRequest, IReadOnlyList<WorkDeliveryPlanResponse>>(WorkDeliveryCapabilities.Read,
                (r, _) => { PlanReads++; return Task.FromResult<IReadOnlyList<WorkDeliveryPlanResponse>>(Plans.Where(x => r.PlanId is null || x.Id == r.PlanId).ToArray()); });
            Grant<AgentOperatingStateReadRequest, AgentOperatingStateReadResponse>(PlatformCapabilities.AgentOperatingStateRead,
                (r, _) => Task.FromResult(new AgentOperatingStateReadResponse(States.GetValueOrDefault(r.StateKey))));
            Grant<AgentOperatingStateWriteRequest, AgentOperatingStateResponse>(PlatformCapabilities.AgentOperatingStateWrite, (r, _) => {
                Lose("state");
                var state = new AgentOperatingStateResponse(Guid.NewGuid(), r.StateKey, r.SchemaId, r.SchemaVersion, r.Status,
                    r.SourceRevisions, r.ConditionCodes, r.DecisionFingerprint, r.OpenCommitmentCorrelations, r.AttentionReviewId, r.Payload, (r.ExpectedRevision ?? 0) + 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
                States[r.StateKey] = state; Lose("state-after"); return Task.FromResult(state);
            });
            Grant<CreateWorkItemRequest, WorkItem>(WorkItemCapabilities.Create, (r, _) => {
                if (!_tickets.TryGetValue(r.IdempotencyKey, out var ticket)) {
                    CreatedTickets++; _tickets[r.IdempotencyKey] = ticket = Empty<WorkItem>() with { Id = Guid.NewGuid(), Planning = r.Planning, TypeKey = r.TypeKey };
                }
                return Task.FromResult(ticket);
            });
            Grant<ConfigureWorkDeliveryPlanRequest, WorkDeliveryPlanResponse>(WorkDeliveryCapabilities.Configure, (r, _) => {
                if (Plans.Count > 0) return Task.FromResult(Plans[0]);
                ConfiguredPlans++;
                var workflow = Assert.Single(r.TicketWorkflows);
                var planning = _tickets.Values.Single(x => x.Id == workflow.TicketId).Planning!;
                var plan = Empty<WorkDeliveryPlanResponse>() with { Id = Guid.NewGuid(), Revision = 1, Name = r.Name,
                    ManagerOrganizationUserId = r.ManagerOrganizationUserId, Status = "Draft", Branches = r.Branches,
                    Scopes = [new(WorkExecutionScopes.Ticket, workflow.TicketId, Board.Id, 1, workflow.EvidenceItemIds, planning,
                        workflow.Steps.Select(x => x.Assignment).ToArray()) { TicketWorkflow = workflow }] };
                Plans.Add(plan); Lose("configure"); return Task.FromResult(plan);
            });
            Grant<FinalizeWorkItemDeliveryRequest, WorkItem>(WorkItemCapabilities.FinalizeDelivery, (r, _) => {
                if (DenyFinalize) throw new PlatformCapabilityException(WorkItemCapabilities.FinalizeDelivery, PlatformCapabilityErrorCode.Denied, "Revoked");
                var index = Items.FindIndex(x => x.Id == r.ItemId); Assert.Equal(Items[index].Revision, r.ExpectedRevision);
                Finalizations.Add(r); Items[index] = Items[index] with { Delivery = r.Delivery, Revision = r.ExpectedRevision + 1 };
                Lose("finalize"); return Task.FromResult(Items[index]);
            });
            Grant<ReviseWorkItemPlanningRequest, WorkItem>(WorkItemCapabilities.RevisePlanning, (r, _) => {
                var index = Items.FindIndex(x => x.Id == r.ItemId);
                Assert.Equal(Items[index].Revision, r.ExpectedRevision); Assert.Equal(Items[index].PlanningRevision, r.ExpectedPlanningRevision);
                PlanningWrites++;
                Items[index] = Items[index] with { Planning = r.Planning, AccountableOrganizationUserId = r.AccountableOrganizationUserId,
                    StageAssignments = r.StageAssignments!, Revision = r.ExpectedRevision + 1, PlanningRevision = r.ExpectedPlanningRevision + 1 };
                return Task.FromResult(Items[index]);
            });
            Grant<StartWorkSprintExecutionRequest, WorkSprintExecutionResponse>(WorkOrchestrationCapabilities.Start, (_, _) => { Starts++; return Task.FromResult(Empty<WorkSprintExecutionResponse>()); });
            Grant<ControlWorkDeliveryPlanRequest, WorkDeliveryPlanResponse>(WorkDeliveryCapabilities.Control, (r, _) => {
                Assert.Equal("activate", r.Action); Assert.All(Items.Where(x => x.SprintId == Sprint.Id), x => Assert.NotNull(x.Delivery));
                Activations++; Plans[0] = Plans[0] with { Status = "Active", Revision = Plans[0].Revision + 1 };
                Lose("activate"); return Task.FromResult(Plans[0]);
            });
        }
        private void Lose(string boundary) { if (LoseResponse == boundary) { LoseResponse = null; throw new InvalidOperationException("Injected interruption at " + boundary); } }
        internal Task<SprintDeliveryPreparation.Result> Prepare() => SprintDeliveryPreparation.PrepareAsync(Board.Id, Sprint.Id, Context, default);
    }
}
