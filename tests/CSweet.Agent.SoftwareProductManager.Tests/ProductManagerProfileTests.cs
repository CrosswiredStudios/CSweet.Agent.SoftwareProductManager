using System.Runtime.CompilerServices;
using System.Text.Json;
using CSweet.Agent.SDK;
using CSweet.WorkManagement.Contracts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CSweet.Agent.SoftwareProductManager.Tests;

public sealed class ProductManagerProfileTests
{
    [Fact]
    public void Manifest_UsesProductIdentityAndLeastPrivilegeCoordination()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ManifestPath()));
        var root = document.RootElement;
        Assert.Equal("com.csweet.product-manager", ProductManagerProfile.AgentId);
        Assert.Equal("C-Sweet Software Product Manager", ProductManagerProfile.DefaultDisplayName);
        Assert.Equal(ProductManagerProfile.AgentId, root.GetProperty("id").GetString());
        Assert.Equal(ProductManagerProfile.DefaultDisplayName, root.GetProperty("name").GetString());
        Assert.Equal(ProductManagerProfile.Version, root.GetProperty("version").GetString());
        var provides = root.GetProperty("provides").EnumerateArray()
            .Select(x => x.GetProperty("name").GetString()).ToHashSet();
        var requires = root.GetProperty("requires").EnumerateArray()
            .Select(x => x.GetProperty("name").GetString()).ToHashSet();
        var providerCapabilities = new HashSet<string>(StringComparer.Ordinal)
        {
            ProductManagerProfile.SoftwareArchitectureDesignCapability,
            ProductManagerProfile.SoftwareArchitecturePublishCapability
        };
        Assert.All(provides.Concat(requires).Where(capability =>
                !providerCapabilities.Contains(capability!)),
            capability => Assert.Contains(capability!, CapabilityCatalog.All));
        Assert.Contains(ProductManagementCapabilities.Plan, provides);
        Assert.Contains(ProductManagementCapabilities.ContextUpdate, provides);
        Assert.Contains(ProductManagementCapabilities.RoleBrief, requires);
        Assert.Contains(ProductManagementCapabilities.PlanReview, requires);
        Assert.Contains(ProductManagementCapabilities.Escalation, requires);
        Assert.Contains(WorkBoardCapabilities.Create, requires);
        Assert.Contains(ProductManagerProfile.TeamRosterCapability, requires);
        Assert.Contains(ProductManagerProfile.SoftwareArchitectureDesignCapability, requires);
        Assert.Contains(ProductManagerProfile.SoftwareArchitecturePublishCapability, requires);
        Assert.DoesNotContain(PlatformCapabilities.HiringRecommendationList, requires);
        Assert.DoesNotContain(PlatformCapabilities.HiringRecommendationUpsert, requires);
        Assert.DoesNotContain(PlatformCapabilities.HiringWorkflowStage, requires);
        Assert.Contains(ProductManagerProfile.CreateCommunicationCapability, requires);
        Assert.Contains(ProductManagerProfile.SendCommunicationMessageCapability, requires);
        Assert.Contains(ProductManagerProfile.ProposeResourceChangeCapability, requires);
        Assert.Contains(PlatformCapabilities.ResourceChangeRead, requires);
        Assert.Contains(AgentLifecycleCapabilities.CompleteOnboarding, requires);
        Assert.Contains(MemoryCapabilities.BusinessRead, requires);
    }

    [Fact]
    public async Task Manifest_LoadsAndMatchesTheStandaloneAuthoringContract()
    {
        var manifestPath = ManifestPath();
        var manifest = await AgentManifestLoader.LoadAsync(manifestPath, CancellationToken.None);

        Assert.Equal(ProductManagerProfile.AgentId, manifest.Id);
        Assert.Equal(ProductManagerProfile.Version, manifest.Version);
        Assert.Equal(1, manifest.Runtime.MaximumConcurrentJobs);
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(manifestPath)!, "AGENTS.md")));

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        var root = document.RootElement;
        Assert.All(root.GetProperty("provides").EnumerateArray(), capability =>
        {
            Assert.False(capability.GetProperty("inputSchema").GetProperty("additionalProperties").GetBoolean());
            Assert.False(capability.GetProperty("outputSchema").GetProperty("additionalProperties").GetBoolean());
        });
        Assert.Equal(
            [
                ProductManagerProfile.OnboardedEvent,
                ProductManagerProfile.UserMessageReceivedEvent,
                AgentCoordinationEvents.TurnRequested,
                ManagementEvents.ReviewDue,
                ManagementEvents.ResourceChangeDecided,
                ProductManagerProfile.RecommendationFulfilledEvent
            ],
            root.GetProperty("events").GetProperty("subscribes").EnumerateArray()
                .Select(item => item.GetString()!).ToArray());
        Assert.Equal(
            ["llmProviderId", "llmModel", "responseTone"],
            root.GetProperty("configuration").EnumerateArray()
                .Select(item => item.GetProperty("key").GetString()!).ToArray());
        Assert.All(
            root.GetProperty("configuration").EnumerateArray(),
            field => Assert.False(string.IsNullOrWhiteSpace(
                field.GetProperty("description").GetString())));
        var manifestTone = root.GetProperty("configuration").EnumerateArray()
            .Single(field => field.GetProperty("key").GetString() == "responseTone");
        Assert.Equal("concise", manifestTone.GetProperty("defaultValue").GetString());
        Assert.Equal(
            ["concise", "balanced", "detailed"],
            manifestTone.GetProperty("options").EnumerateArray()
                .Select(option => option.GetProperty("value").GetString()!).ToArray());

        var project = await File.ReadAllTextAsync(Path.Combine(
            Path.GetDirectoryName(manifestPath)!,
            "src",
            "CSweet.Agent.SoftwareProductManager",
            "CSweet.Agent.SoftwareProductManager.csproj"));
        Assert.Contains("CSweet.Agent.SDK\" Version=\"3.2.0", project, StringComparison.Ordinal);
        Assert.Contains("<ProjectReference", project, StringComparison.Ordinal);
        Assert.Contains($"<Version>{ProductManagerProfile.Version}</Version>", project, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemPrompt_EnforcesProductAndChiefBoundaries()
    {
        Assert.Contains("customer discovery", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("roadmap", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("success measures", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("at most two", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("directly message your managing employee", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("approved organization and relationship memory", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Never open with a generic readiness message", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CEO, Chief of Staff, another human, or another agent", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Never maintain the Chief's hiring backlog", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not present a finalized role list", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("routes the request to your authoritative manager", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not provide technical architecture", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("primary startup goal", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("kanban board", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resubmit", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            ProductManagerProfile.SoftwareArchitectureDesignCapability,
            ProductManagerProfile.SystemPrompt,
            StringComparison.Ordinal);
        Assert.Contains(
            ProductManagerProfile.SoftwareArchitecturePublishCapability,
            ProductManagerProfile.SystemPrompt,
            StringComparison.Ordinal);
        Assert.Contains("direct agent conversation", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("approval boundary", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("coordination trigger", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("until genuinely blocked", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("one consolidated response", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("product definition", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResponseNormalization_RemovesRepeatedProductDefinitionAndItsToolNarration()
    {
        var response = """
I have defined the initial product context based on your direction:

**Product Definition:**
- **Target Customer:** Fans of rail-shooter games.
- **Problem:** Validating whether the core gameplay loop is engaging in a browser.
- **Desired Outcome:** A playable prototype demonstrating flight, combat, and progression.

I have updated the product context to reflect our goal.

**Product Definition:**
- **Target Customer:** Fans of rail-shooter games.
- **Problem:** Validating whether the core gameplay loop is engaging in a browser.
- **Desired Outcome:** A playable prototype demonstrating flight, combat, and progression.

What level of prototype fidelity are we aiming for?
""";

        var normalized = ProductManagerAgent.ConsolidateRepeatedProductDefinition(response);

        Assert.Equal(1, normalized.Split("Product Definition", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("updated the product context", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("What level of prototype fidelity", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void ArchitectDirectMessage_BecomesAnAutonomousPlanningTrigger()
    {
        var incoming = new UserMessageReceived(
            Guid.NewGuid(),
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"),
            "The architecture role is ready.",
            new Dictionary<string, string>
            {
                [AgentMessageContextKeys.SenderEmployeeType] = "Agent",
                [AgentMessageContextKeys.SenderRole] = "Software Architect",
                [AgentMessageContextKeys.SenderDisplayName] = "C-Sweet Software Architect"
            },
            Guid.NewGuid(),
            1,
            Guid.NewGuid());

        var prompt = ProductManagerAgent.BuildInboundPrompt(incoming);

        Assert.Contains("delivery-planning coordination trigger", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("publish planned sprints and tickets", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exactly one focused blocking decision", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SoftwareArchitectProviderCapabilitiesAreVisibleAsGovernedModelTools()
    {
        var runtime = new AgentTestRuntime()
            .RegisterCapability<object, object>(
                ProductManagerProfile.SoftwareArchitectureDesignCapability,
                (_, _) => Task.FromResult<object>(new { }),
                modelVisible: true)
            .RegisterCapability<object, object>(
                ProductManagerProfile.SoftwareArchitecturePublishCapability,
                (_, _) => Task.FromResult<object>(new { }),
                modelVisible: true);

        var tools = await runtime.CreateContext().GetModelToolsAsync();
        var names = tools.OfType<AIFunctionDeclaration>().Select(x => x.Name).ToArray();

        Assert.Contains("software_architecture_design_v1", names);
        Assert.Contains("software_architecture_publish_plan_v1", names);
    }

    [Fact]
    public void ContextualOnboardingFallback_IdentifiesTheManagedDeliverableAndTeamApprovalNextStep()
    {
        var organizationId = Guid.NewGuid();
        var profile = new BusinessProfileResponse(
            organizationId,
            "Super Awesome Games",
            "Game Studio",
            "Games",
            "Build browser games.",
            "Make classic games accessible on the web.",
            "Validation",
            ["Classic game fans"],
            ["A browser-based Star Fox 64-inspired game"],
            null,
            ["United States"],
            null,
            [],
            [],
            null,
            "UTC",
            3,
            0.8m,
            new Dictionary<string, ProfileFieldProvenance>());
        var finance = new FinancialOperatingProfileResponse(
            organizationId,
            "USD",
            null,
            null,
            null,
            null,
            20_000m,
            null,
            3,
            "Approval",
            2);
        var organization = new OrganizationSnapshotResponse(
            organizationId,
            "Active",
            [],
            [],
            [new OrganizationObjective(
                Guid.NewGuid(),
                "Deliver a playable browser prototype",
                "Validate the core gameplay loop.",
                "Active",
                null)],
            [],
            [],
            DateTimeOffset.UtcNow);
        var context = new ProductOperatingContext(profile, finance, organization, null, null, null, []);

        var message = ProductManagerOrchestrator.BuildManagerDirectionRequest(context, "Chief of Staff");

        Assert.Contains("Deliver a playable browser prototype", message, StringComparison.Ordinal);
        Assert.Contains("Classic game fans", message, StringComparison.Ordinal);
        Assert.Contains("smallest cross-functional team", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("one proposal for your approval", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ready to begin", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Please confirm my mandate", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Onboarding_UsesTheConfiguredModelForTheFirstManagerMessage()
    {
        var organizationId = Guid.NewGuid();
        var installationId = Guid.NewGuid();
        var productManagerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var providerProfileId = Guid.NewGuid();
        var onboardingEventId = Guid.NewGuid();
        var workItemId = Guid.NewGuid();
        var generatedOpening = "I’m managing the playable browser prototype for classic game fans. I’m now shaping the smallest team and will submit it for approval.";
        CommunicationSendCapture? sentMessage = null;
        CompleteAgentOnboardingRequest? completionRequest = null;
        var profile = new BusinessProfileResponse(
            organizationId,
            "Super Awesome Games",
            "Game Studio",
            "Games",
            "Build browser games.",
            "Make classic games accessible on the web.",
            "Validation",
            ["Classic game fans"],
            ["A browser-based Star Fox 64-inspired game"],
            null,
            ["United States"],
            null,
            [],
            [],
            null,
            "UTC",
            3,
            0.8m,
            new Dictionary<string, ProfileFieldProvenance>());
        var organization = new OrganizationSnapshotResponse(
            organizationId,
            "Active",
            [
                new OrganizationPerson(
                    productManagerId,
                    ProductManagerProfile.DefaultDisplayName,
                    "Agent",
                    null,
                    managerId,
                    installationId,
                    true),
                new OrganizationPerson(managerId, "CEO", "Human", null, null, null, true)
            ],
            [],
            [new OrganizationObjective(
                Guid.NewGuid(),
                "Deliver a playable browser prototype",
                "Validate the core gameplay loop.",
                "Active",
                null)],
            [],
            [],
            DateTimeOffset.UtcNow);
        var runtime = new AgentTestRuntime()
            .RegisterCapability<JsonElement, BusinessProfileResponse>(
                PlatformCapabilities.BusinessProfileRead,
                (_, _) => Task.FromResult(profile))
            .RegisterCapability<JsonElement, OrganizationSnapshotResponse>(
                PlatformCapabilities.OrganizationSnapshotRead,
                (_, _) => Task.FromResult(organization))
            .RegisterCapability<CommunicationSendCapture, CommunicationMessage>(
                ProductManagerProfile.SendCommunicationMessageCapability,
                (request, _) =>
                {
                    sentMessage = request;
                    return Task.FromResult(SentMessage(request));
                })
            .RegisterCapability<CompleteAgentOnboardingRequest, JsonElement>(
                AgentLifecycleCapabilities.CompleteOnboarding,
                (request, _) =>
                {
                    completionRequest = request;
                    return Task.FromResult(JsonSerializer.SerializeToElement(new { completed = true }));
                });
        var chatClient = new CapturingChatClient(generatedOpening);
        var agent = new ProductManagerAgent(
            new TestLlmClientFactory(chatClient),
            NullLogger<ProductManagerAgent>.Instance,
            new ProductManagerOrchestrator(NullLogger<ProductManagerOrchestrator>.Instance));
        var context = runtime.CreateContext(
            organizationId.ToString("D"),
            installationId.ToString("D"));
        var configurationResult = await agent.ExecuteCapabilityAsync(
            new AgentCapabilityRequest(
                Guid.NewGuid(),
                AgentConfigurationCapabilities.Update,
                JsonSerializer.SerializeToElement(new UpdateAgentConfigurationRequest(
                    new Dictionary<string, JsonElement>
                    {
                        ["llmProviderId"] = JsonSerializer.SerializeToElement(providerProfileId.ToString("D")),
                        ["llmModel"] = JsonSerializer.SerializeToElement("test-model")
                    }))),
            context,
            CancellationToken.None);
        Assert.True(configurationResult.Succeeded);

        await agent.HandleEventAsync(
            new AgentEventEnvelope(
                workItemId,
                onboardingEventId,
                ProductManagerProfile.OnboardedEvent,
                JsonSerializer.SerializeToElement(new AgentOnboardedEvent(
                    organizationId,
                    productManagerId,
                    managerId,
                    conversationId,
                    DateTimeOffset.UtcNow)),
                DateTimeOffset.UtcNow,
                Guid.NewGuid().ToString("N")),
            context,
            CancellationToken.None);

        Assert.NotNull(sentMessage);
        Assert.Equal(conversationId, sentMessage.ChatId);
        Assert.Equal(generatedOpening, sentMessage.Content);
        Assert.Contains("Super Awesome Games", chatClient.Prompt, StringComparison.Ordinal);
        Assert.Contains("Deliver a playable browser prototype", chatClient.Prompt, StringComparison.Ordinal);
        Assert.Contains("approved C-Sweet organization", chatClient.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not send a generic welcome", chatClient.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(completionRequest);
        Assert.Equal(onboardingEventId, completionRequest.EventId);
        Assert.NotEqual(workItemId, completionRequest.EventId);
    }

    [Fact]
    public async Task Configuration_DescribesEveryFieldAndRejectsUnsupportedTone()
    {
        var agent = new ProductManagerAgent(
            NullLogger<ProductManagerAgent>.Instance,
            new ProductManagerOrchestrator(NullLogger<ProductManagerOrchestrator>.Instance));
        var context = new AgentTestRuntime().CreateContext();
        var describe = await agent.ExecuteCapabilityAsync(
            new AgentCapabilityRequest(
                Guid.NewGuid(),
                AgentConfigurationCapabilities.Describe,
                JsonSerializer.SerializeToElement(new { })),
            context,
            CancellationToken.None);

        Assert.True(describe.Succeeded);
        var schema = describe.Value!.Value.Deserialize<AgentConfigurationSchemaResponse>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(schema);
        Assert.Equal(
            ["llmProviderId", "llmModel", "responseTone"],
            schema.Fields.Select(field => field.Key).ToArray());
        var tone = schema.Fields.Single(field => field.Key == "responseTone");
        Assert.Equal(
            ["concise", "balanced", "detailed"],
            tone.Options!.Select(option => option.Value).ToArray());
        Assert.Equal("concise", schema.Settings["responseTone"].GetString());

        var invalid = await agent.ExecuteCapabilityAsync(
            new AgentCapabilityRequest(
                Guid.NewGuid(),
                AgentConfigurationCapabilities.Update,
                JsonSerializer.SerializeToElement(new UpdateAgentConfigurationRequest(
                    new Dictionary<string, JsonElement>
                    {
                        ["responseTone"] = JsonSerializer.SerializeToElement("Blunt")
                    }))),
            context,
            CancellationToken.None);

        Assert.False(invalid.Succeeded);
        Assert.Contains("must be one of", invalid.Error, StringComparison.OrdinalIgnoreCase);

        var valid = await agent.ExecuteCapabilityAsync(
            new AgentCapabilityRequest(
                Guid.NewGuid(),
                AgentConfigurationCapabilities.Update,
                JsonSerializer.SerializeToElement(new UpdateAgentConfigurationRequest(
                    new Dictionary<string, JsonElement>
                    {
                        ["llmProviderId"] = JsonSerializer.SerializeToElement(Guid.NewGuid().ToString("D")),
                        ["llmModel"] = JsonSerializer.SerializeToElement("model"),
                        ["responseTone"] = JsonSerializer.SerializeToElement("concise")
                    }))),
            context,
            CancellationToken.None);

        Assert.True(valid.Succeeded);
    }

    [Fact]
    public async Task Coordination_QuestionsArchitectAndNeverCreatesGenericPlaceholderTickets()
    {
        var productManagerId = Guid.NewGuid();
        var productInstallationId = Guid.NewGuid();
        var architectId = Guid.NewGuid();
        var architectInstallationId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        var created = new List<CreateWorkItemRequest>();
        var runtime = new AgentTestRuntime()
            .RegisterCapability<WorkBoardListRequest, IReadOnlyList<WorkBoardSummary>>(
                WorkBoardCapabilities.Read,
                (_, _) => Task.FromResult<IReadOnlyList<WorkBoardSummary>>(
                [
                    new WorkBoardSummary(boardId, "Demo delivery", "Approved board", false, false, 1, [])
                    {
                        ManagerOrganizationUserId = productManagerId
                    }
                ]))
            .RegisterCapability<WorkBoardReference, WorkBoardDetail>(
                WorkItemCapabilities.Read,
                (_, _) => Task.FromResult(new WorkBoardDetail(
                    new WorkBoardSummary(boardId, "Demo delivery", "Approved board", false, false, 1, []),
                    [new WorkBoardColumn(columnId, "Ready For Development", "ToDo", 1, "Pull", null)],
                    [])))
            .RegisterCapability<CreateWorkItemRequest, WorkItem>(
                WorkItemCapabilities.Create,
                (request, _) =>
                {
                    created.Add(request);
                    return Task.FromResult(new WorkItem(
                        Guid.NewGuid(), columnId, null, null, request.Kind, request.Title,
                        request.Description ?? string.Empty, "Ready", request.Priority,
                        null, created.Count, 1, null)
                    {
                        Identifier = $"DEMO-{created.Count}"
                    });
                });
        var agent = new ProductManagerAgent(
            NullLogger<ProductManagerAgent>.Instance,
            new ProductManagerOrchestrator(NullLogger<ProductManagerOrchestrator>.Instance));
        var self = new AgentCoordinationParticipant(
            productManagerId, productInstallationId, "Product Manager", "Product Manager");
        var architect = new AgentCoordinationParticipant(
            architectId, architectInstallationId, "Architect", "Software Architect");
        var now = DateTimeOffset.UtcNow;
        var initial = new AgentCoordinationTurn(
            Guid.NewGuid(), 0, architectId, AgentCoordinationDispositions.Continue,
            "Populate the kanban board for the demo.", now);

        var first = await agent.HandleCoordinationTurnAsync(
            new AgentCoordinationTurnRequest(
                Guid.NewGuid(), 1, 1, "Demo delivery", "Complete the demo",
                ["The demo passes acceptance tests."], self, architect, false, [initial]),
            runtime.CreateContext(), CancellationToken.None);
        Assert.Equal(AgentCoordinationDispositions.Continue, first.Disposition);
        Assert.Contains("dependency order", first.Content, StringComparison.OrdinalIgnoreCase);

        var sessionId = Guid.NewGuid();
        var second = await agent.HandleCoordinationTurnAsync(
            new AgentCoordinationTurnRequest(
                sessionId, 3, 3, "Demo delivery", "Complete the demo",
                ["The demo passes acceptance tests."], self, architect, false,
                [
                    initial,
                    new AgentCoordinationTurn(Guid.NewGuid(), 1, productManagerId,
                        AgentCoordinationDispositions.Continue, first.Content, now),
                    new AgentCoordinationTurn(Guid.NewGuid(), 2, architectId,
                        AgentCoordinationDispositions.Continue,
                        "Build the API contract, persistence path, rollback, and fault tests in that order.", now)
                ]),
            runtime.CreateContext(), CancellationToken.None);

        Assert.Equal(AgentCoordinationDispositions.Blocked, second.Disposition);
        Assert.Empty(created);
        Assert.Contains("planning", second.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Revision_SequencesImmediateRolesToTheAuthoritativeConcurrentHireCap()
    {
        var roles = new[]
        {
            Role("architecture", "Software Architect", 1, "Now"),
            Role("development", "Software Developer", 2, "Now"),
            Role("quality", "Software QA", 3, "Now")
        };
        var finance = new FinancialOperatingProfileResponse(
            Guid.NewGuid(), "USD", null, null, null, null, null, null, 1, "Approval", 7);

        var revised = ProductManagerAgent.ReviseRolesForAuthoritativeConstraints(roles, finance);

        Assert.Equal("Now", revised[0].Timing);
        Assert.Equal("Next", revised[1].Timing);
        Assert.Equal("Next", revised[2].Timing);
        Assert.Equal(
            ["Software Architect", "Software Developer", "Software QA"],
            revised.Select(x => x.Title).ToArray());
    }

    [Fact]
    public void ProductBoardName_IsAppropriateStableAndWithinPlatformLimit()
    {
        var name = ProductManagerAgent.BuildProductBoardName(new string('x', 300));

        Assert.EndsWith(" - Product Team", name, StringComparison.Ordinal);
        Assert.True(name.Length <= 160);
    }

    [Fact]
    public void TwoColumnBoardRepair_PreservesIdsAndBuildsExactWorkflow()
    {
        var boardId = Guid.NewGuid();
        var toDoId = Guid.NewGuid();
        var doneId = Guid.NewGuid();
        var detail = new WorkBoardDetail(
            new WorkBoardSummary(boardId, "Software", "", false, false, 1, []),
            [
                new WorkBoardColumn(toDoId, "To Do", "ToDo", 0, "Disabled", null),
                new WorkBoardColumn(doneId, "Done", "Done", 1, "Disabled", null)
            ],
            []);

        var repaired = ProductManagerAgent.BuildReconciledSoftwareBoardColumns(detail);

        Assert.Equal(
            ["Backlog", "Ready For Development", "In Development", "Dev Complete", "In Testing", "Ready To Merge", "Done"],
            repaired.Select(x => x.Name));
        Assert.Equal(toDoId, repaired[0].Id);
        Assert.Equal(doneId, repaired[^1].Id);
        Assert.Null(repaired[1].Id);
    }

    [Fact]
    public void BoardRepair_RejectsOccupiedUnmatchedColumn()
    {
        var boardId = Guid.NewGuid();
        var customId = Guid.NewGuid();
        var detail = new WorkBoardDetail(
            new WorkBoardSummary(boardId, "Software", "", false, false, 2, []),
            [new WorkBoardColumn(customId, "Design Review", "InProgress", 0, "Disabled", null)],
            [new WorkItem(Guid.NewGuid(), customId, null, null, "Task", "Review", "", "Active", "Medium", null, 1, 1, null)]);

        var error = Assert.Throws<InvalidOperationException>(() =>
            ProductManagerAgent.BuildReconciledSoftwareBoardColumns(detail));

        Assert.Contains("occupied unmatched", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnverifiedBoardClaim_IsRejected()
    {
        const string claim = "The Kanban board has been created and configured with all seven columns.";

        Assert.True(ProductManagerAgent.ClaimsBoardProvisioningAction(claim));
        var verified = ProductManagerAgent.EnsureAccurateBoardStatus(claim, null);
        Assert.Contains("could not verify", verified, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("has been created", verified, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApprovedTeam_AcknowledgesApprovalWithoutProvisioningBoard()
    {
        var organizationId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var boardMutationCount = 0;
        CommunicationSendCapture? messageRequest = null;
        var response = ResourceChange(
            requestId,
            organizationId,
            conversationId,
            "Validate the first customer workflow",
            "Approved");
        var runtime = new AgentTestRuntime()
            .RegisterCapability<ResourceChangeReadRequest, ResourceChangeReadResponse>(
                PlatformCapabilities.ResourceChangeRead,
                (_, _) => Task.FromResult(new ResourceChangeReadResponse([response])))
            .RegisterCapability<CreateWorkBoardRequest, WorkBoardSummary>(
                WorkBoardCapabilities.Create,
                (request, _) =>
                {
                    boardMutationCount++;
                    return Task.FromResult(new WorkBoardSummary(
                        Guid.NewGuid(), request.Name, request.Description ?? string.Empty,
                        false, false, 1, [WorkBoardCapabilities.Create]));
                })
            .RegisterCapability<ConfigureWorkBoardColumnsRequest, WorkBoardDetail>(
                WorkBoardCapabilities.ConfigureColumns,
                (request, _) =>
                {
                    boardMutationCount++;
                    return Task.FromResult(new WorkBoardDetail(
                        new WorkBoardSummary(
                            request.BoardId, "Software", "", false, false, 2,
                            [WorkBoardCapabilities.Read, WorkBoardCapabilities.ConfigureColumns]),
                        request.Columns.Select((column, index) => new WorkBoardColumn(
                            Guid.NewGuid(), column.Name, column.Category, index,
                            column.WipPolicy, column.WipLimit)).ToList(),
                        []));
                })
            .RegisterCapability<ConfigureSoftwareOrchestrationTemplateRequest, WorkOrchestrationPolicyRevision>(
                WorkOrchestrationCapabilities.ConfigureSoftwareTemplate,
                (request, _) =>
                {
                    boardMutationCount++;
                    return Task.FromResult(new WorkOrchestrationPolicyRevision(
                        Guid.NewGuid(), Guid.NewGuid(), request.BoardId, 1, "Software delivery",
                        "ready", request.MergeMode,
                        new WorkOrchestrationConcurrencyLimits(100, 25, 10, 5, 1),
                        [], [], true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
                })
            .RegisterCapability<CommunicationSendCapture, CommunicationMessage>(
                ProductManagerProfile.SendCommunicationMessageCapability,
                (request, _) =>
                {
                    messageRequest = request;
                    return Task.FromResult(SentMessage(request));
                });
        var context = runtime.CreateContext(
            organizationId.ToString("D"),
            response.RequesterInstallationId.ToString("D"));
        var agent = new ProductManagerAgent(
            NullLogger<ProductManagerAgent>.Instance,
            new ProductManagerOrchestrator(NullLogger<ProductManagerOrchestrator>.Instance));

        await agent.HandleResourceChangeDecisionAsync(
            new AgentEventEnvelope(
                Guid.NewGuid(),
                Guid.NewGuid(),
                ManagementEvents.ResourceChangeDecided,
                JsonSerializer.SerializeToElement(new ResourceChangeDecisionEvent(
                    requestId,
                    organizationId,
                    response.RequesterOrganizationUserId,
                    response.ManagerOrganizationUserId,
                    "Approved",
                    DateTimeOffset.UtcNow)),
                DateTimeOffset.UtcNow),
            context,
            CancellationToken.None);

        Assert.Equal(0, boardMutationCount);
        Assert.NotNull(messageRequest);
        Assert.Contains("wait until every approved role is filled", messageRequest.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Software QA", messageRequest.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RevisionRequested_ResubmitsCompleteTeamAndSupersedesReviewedRequest()
    {
        var organizationId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var response = ResourceChange(
            requestId,
            organizationId,
            Guid.NewGuid(),
            "Validate the first customer workflow",
            "RevisionRequested") with
        {
            DecisionComment = "Start only one hire at a time.",
            Roles =
            [
                Role("design", "Product Designer", 1, "Now"),
                Role("engineering", "Product Engineer", 2, "Now")
            ]
        };
        response = response with
        {
            Deltas = response.Roles
                .Select(role => new ResourceChangeRoleDelta("Add", role, null))
                .ToList()
        };
        ResourceChangeProposalRequest? revisedProposal = null;
        var finance = new FinancialOperatingProfileResponse(
            organizationId, "USD", null, null, null, null, null, null, 1, "Approval", 2);
        var runtime = new AgentTestRuntime()
            .RegisterCapability<ResourceChangeReadRequest, ResourceChangeReadResponse>(
                PlatformCapabilities.ResourceChangeRead,
                (_, _) => Task.FromResult(new ResourceChangeReadResponse([response])))
            .RegisterCapability<JsonElement, FinancialOperatingProfileResponse>(
                PlatformCapabilities.FinanceProfileRead,
                (_, _) => Task.FromResult(finance))
            .RegisterCapability<ResourceChangeProposalRequest, ResourceChangeRequestResponse>(
                PlatformCapabilities.ResourceChangePropose,
                (request, _) =>
                {
                    revisedProposal = request;
                    return Task.FromResult(response);
                })
            .RegisterCapability<CommunicationSendCapture, CommunicationMessage>(
                ProductManagerProfile.SendCommunicationMessageCapability,
                (request, _) => Task.FromResult(SentMessage(request)));
        var context = runtime.CreateContext(
            organizationId.ToString("D"),
            response.RequesterInstallationId.ToString("D"));
        var agent = new ProductManagerAgent(
            NullLogger<ProductManagerAgent>.Instance,
            new ProductManagerOrchestrator(NullLogger<ProductManagerOrchestrator>.Instance));

        await agent.HandleResourceChangeDecisionAsync(
            new AgentEventEnvelope(
                Guid.NewGuid(),
                Guid.NewGuid(),
                ManagementEvents.ResourceChangeDecided,
                JsonSerializer.SerializeToElement(new ResourceChangeDecisionEvent(
                    requestId,
                    organizationId,
                    response.RequesterOrganizationUserId,
                    response.ManagerOrganizationUserId,
                    "RevisionRequested",
                    DateTimeOffset.UtcNow)),
                DateTimeOffset.UtcNow),
            context,
            CancellationToken.None);

        Assert.NotNull(revisedProposal);
        Assert.Equal(requestId, revisedProposal.SupersedesRequestId);
        Assert.Equal(["Now", "Next"], revisedProposal.Roles.Select(role => role.Timing).ToArray());
    }

    [Fact]
    public async Task RejectedTeam_AsksManagerForOneFocusedRefinement()
    {
        var organizationId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var response = ResourceChange(
            requestId,
            organizationId,
            Guid.NewGuid(),
            "Validate the first customer workflow",
            "Rejected") with
        {
            DecisionComment = "The proposed team is too broad."
        };
        CommunicationSendCapture? messageRequest = null;
        var runtime = new AgentTestRuntime()
            .RegisterCapability<ResourceChangeReadRequest, ResourceChangeReadResponse>(
                PlatformCapabilities.ResourceChangeRead,
                (_, _) => Task.FromResult(new ResourceChangeReadResponse([response])))
            .RegisterCapability<CommunicationSendCapture, CommunicationMessage>(
                ProductManagerProfile.SendCommunicationMessageCapability,
                (request, _) =>
                {
                    messageRequest = request;
                    return Task.FromResult(SentMessage(request));
                });
        var context = runtime.CreateContext(
            organizationId.ToString("D"),
            response.RequesterInstallationId.ToString("D"));
        var agent = new ProductManagerAgent(
            NullLogger<ProductManagerAgent>.Instance,
            new ProductManagerOrchestrator(NullLogger<ProductManagerOrchestrator>.Instance));

        await agent.HandleResourceChangeDecisionAsync(
            new AgentEventEnvelope(
                Guid.NewGuid(),
                Guid.NewGuid(),
                ManagementEvents.ResourceChangeDecided,
                JsonSerializer.SerializeToElement(new ResourceChangeDecisionEvent(
                    requestId,
                    organizationId,
                    response.RequesterOrganizationUserId,
                    response.ManagerOrganizationUserId,
                    "Rejected",
                    DateTimeOffset.UtcNow)),
                DateTimeOffset.UtcNow),
            context,
            CancellationToken.None);

        Assert.NotNull(messageRequest);
        Assert.Contains("The proposed team is too broad.", messageRequest.Content, StringComparison.Ordinal);
        Assert.Contains("What single outcome, role, or constraint", messageRequest.Content, StringComparison.Ordinal);
        Assert.EndsWith("?", messageRequest.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecommendationFulfilled_ReassessesOnlyItsOwnApprovedPlan()
    {
        var organizationId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var response = ResourceChange(
            requestId,
            organizationId,
            Guid.NewGuid(),
            "Deliver the MVP",
            "Approved");
        CommunicationSendCapture? messageRequest = null;
        var messageRequests = new List<CommunicationSendCapture>();
        var runtime = new AgentTestRuntime()
            .RegisterCapability<ResourceChangeReadRequest, ResourceChangeReadResponse>(
                PlatformCapabilities.ResourceChangeRead,
                (request, _) => Task.FromResult(new ResourceChangeReadResponse(
                    request.RequestId == requestId ? [response] : [])))
            .RegisterCapability<TeamRosterRequest, TeamRosterResponse>(
                ProductManagerProfile.TeamRosterCapability,
                (_, _) => Task.FromResult(new TeamRosterResponse(new AgentTeamContext(
                    Guid.NewGuid().ToString("D"),
                    "product",
                    "Product Team",
                    1,
                    response.RequesterOrganizationUserId.ToString("D"),
                    "Product Manager",
                    [],
                    [new TeamRoleCoverage("Product Engineer", 1)],
                    1,
                    false))))
            .RegisterCapability<CommunicationSendCapture, CommunicationMessage>(
                ProductManagerProfile.SendCommunicationMessageCapability,
                (request, _) =>
                {
                    messageRequest = request;
                    messageRequests.Add(request);
                    return Task.FromResult(SentMessage(request));
                });
        var context = runtime.CreateContext(
            organizationId.ToString("D"),
            response.RequesterInstallationId.ToString("D"));
        var agent = new ProductManagerAgent(
            NullLogger<ProductManagerAgent>.Instance,
            new ProductManagerOrchestrator(NullLogger<ProductManagerOrchestrator>.Instance));

        await agent.HandleHiringRecommendationFulfilledAsync(
            RecommendationFulfilled(organizationId, Guid.NewGuid()),
            context,
            CancellationToken.None);
        Assert.Null(messageRequest);

        var ownEvent = RecommendationFulfilled(organizationId, requestId);
        await agent.HandleHiringRecommendationFulfilledAsync(ownEvent, context, CancellationToken.None);
        await agent.HandleHiringRecommendationFulfilledAsync(ownEvent, context, CancellationToken.None);

        Assert.NotNull(messageRequest);
        Assert.Equal(response.ConversationId, messageRequest.ChatId);
        Assert.Equal($"hiring-recommendation-fulfilled:{ownEvent.EventId:N}:product-manager", messageRequest.IdempotencyKey);
        Assert.Contains("Product Engineer", messageRequest.Content, StringComparison.Ordinal);
        Assert.Contains("covers every planned role", messageRequest.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, messageRequests.Count);
        Assert.Single(messageRequests.Select(request => request.IdempotencyKey).Distinct(StringComparer.Ordinal));
    }

    [Fact]
    public async Task FinalApprovedRoleFulfillmentStartsOneStableArchitectPlanningKickoff()
    {
        var organizationId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var productManagerId = Guid.NewGuid();
        var productManagerInstallationId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var architectId = Guid.NewGuid();
        var architectInstallationId = Guid.NewGuid();
        var developerId = Guid.NewGuid();
        var developerInstallationId = Guid.NewGuid();
        var qualityId = Guid.NewGuid();
        var qualityInstallationId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var roles = new[]
        {
            Role("architecture", "Software Architect", 1, "Now") with { ReportsToOrganizationUserId = productManagerId },
            Role("development", "Software Developer", 2, "Now") with { ReportsToOrganizationUserId = productManagerId },
            Role("quality", "Software QA", 3, "Now") with { ReportsToOrganizationUserId = productManagerId }
        };
        var response = new ResourceChangeRequestResponse(
            requestId, organizationId, productManagerId, productManagerInstallationId, managerId,
            conversationId, Guid.NewGuid(), "Ship the first customer release",
            "Use the smallest complete software team.", 1, roles,
            roles.Select(x => new ResourceChangeRoleDelta("Add", x, null)).ToList(),
            ["The first release is intentionally bounded."], ["Preserve governed delivery."],
            null, "Approved", "Delivered", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        {
            TeamId = teamId,
            TeamName = "Release Team"
        };
        var team = new AgentTeamContext(
            teamId.ToString("D"), "release", "Release Team", 1,
            productManagerId.ToString("D"), "Product Manager",
            [
                new AgentTeammate(productManagerId.ToString("D"), "Product Manager", "Agent", null, "Software Product Manager", "Self", "Active"),
                new AgentTeammate(architectId.ToString("D"), "Architect", "Agent", null, "Software Architect", "Peer", "Active"),
                new AgentTeammate(developerId.ToString("D"), "Developer", "Agent", null, "Software Developer", "Peer", "Active"),
                new AgentTeammate(qualityId.ToString("D"), "QA", "Agent", null, "Software QA", "Peer", "Active")
            ],
            [
                new TeamRoleCoverage("Software Architect", 1),
                new TeamRoleCoverage("Software Developer", 1),
                new TeamRoleCoverage("Software QA", 1)
            ], 4, false);
        var organization = new OrganizationSnapshotResponse(
            organizationId, "Active",
            [
                new OrganizationPerson(productManagerId, "Product Manager", "Agent", null, managerId, productManagerInstallationId, true),
                new OrganizationPerson(managerId, "Manager", "Human", null, null, null, true),
                new OrganizationPerson(architectId, "Architect", "Agent", null, productManagerId, architectInstallationId, true),
                new OrganizationPerson(developerId, "Developer", "Agent", null, productManagerId, developerInstallationId, true),
                new OrganizationPerson(qualityId, "QA", "Agent", null, productManagerId, qualityInstallationId, true)
            ], [], [], [], [], DateTimeOffset.UtcNow);
        var columns = new[]
        {
            new WorkBoardColumn(Guid.NewGuid(), "Backlog", "ToDo", 0, "Disabled", null),
            new WorkBoardColumn(Guid.NewGuid(), "Ready For Development", "ToDo", 1, "Disabled", null),
            new WorkBoardColumn(Guid.NewGuid(), "In Development", "InProgress", 2, "Disabled", null),
            new WorkBoardColumn(Guid.NewGuid(), "Dev Complete", "InProgress", 3, "Disabled", null),
            new WorkBoardColumn(Guid.NewGuid(), "In Testing", "InProgress", 4, "Disabled", null),
            new WorkBoardColumn(Guid.NewGuid(), "Ready To Merge", "InProgress", 5, "Disabled", null),
            new WorkBoardColumn(Guid.NewGuid(), "Done", "Done", 6, "Disabled", null)
        };
        var board = new WorkBoardSummary(boardId, "Release Product Team", "Approved", false, false, 1, [])
        {
            TeamId = teamId,
            ManagerOrganizationUserId = productManagerId
        };
        var messages = new List<CommunicationSendCapture>();
        var createdChats = new List<CreateCommunicationChat>();
        var runtime = new AgentTestRuntime()
            .RegisterCapability<ResourceChangeReadRequest, ResourceChangeReadResponse>(
                PlatformCapabilities.ResourceChangeRead,
                (_, _) => Task.FromResult(new ResourceChangeReadResponse([response])))
            .RegisterCapability<TeamRosterRequest, TeamRosterResponse>(
                ProductManagerProfile.TeamRosterCapability,
                (_, _) => Task.FromResult(new TeamRosterResponse(team)))
            .RegisterCapability<object, OrganizationSnapshotResponse>(
                PlatformCapabilities.OrganizationSnapshotRead,
                (_, _) => Task.FromResult(organization))
            .RegisterCapability<WorkBoardListRequest, IReadOnlyList<WorkBoardSummary>>(
                WorkBoardCapabilities.Read,
                (_, _) => Task.FromResult<IReadOnlyList<WorkBoardSummary>>([board]))
            .RegisterCapability<WorkBoardReference, WorkBoardDetail>(
                WorkItemCapabilities.Read,
                (_, _) => Task.FromResult(new WorkBoardDetail(board, columns, [])))
            .RegisterCapability<ConfigureSoftwareOrchestrationTemplateRequest, WorkOrchestrationPolicyRevision>(
                WorkOrchestrationCapabilities.ConfigureSoftwareTemplate,
                (request, _) => Task.FromResult(new WorkOrchestrationPolicyRevision(
                    Guid.NewGuid(), Guid.NewGuid(), request.BoardId, 1, "Software delivery", "ready",
                    request.MergeMode, new WorkOrchestrationConcurrencyLimits(100, 25, 10, 5, 1),
                    [], [], true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)))
            .RegisterCapability<object, CommunicationHub>(
                CommunicationCapabilities.ChatRead,
                (_, _) => Task.FromResult(new CommunicationHub(
                    productManagerId, productManagerId, false, true, [])))
            .RegisterCapability<CreateCommunicationChat, CommunicationAction>(
                CommunicationCapabilities.ChatCreate,
                (request, _) =>
                {
                    createdChats.Add(request);
                    var participants = request.ParticipantOrganizationUserIds.Select(id =>
                        new CommunicationParticipant(
                            id,
                            id == architectId ? "Architect" : "Participant",
                            id == managerId ? "Human" : "Agent",
                            id == architectId ? "Software Architect" : "Team member")).ToList();
                    return Task.FromResult(new CommunicationAction(
                        true, null, "Created",
                        new CommunicationChat(Guid.NewGuid(), request.Title ?? "Direct", request.Description,
                            request.IsDirect, request.IsPrivate, false, true, DateTimeOffset.UtcNow,
                            participants, null, null, 0)));
                })
            .RegisterCapability<CommunicationSendCapture, CommunicationMessage>(
                CommunicationCapabilities.MessageSend,
                (request, _) =>
                {
                    messages.Add(request);
                    return Task.FromResult(SentMessage(request) with { ChatTurnId = Guid.NewGuid() });
                })
            .RegisterCapability<TeamRepositoryOptionsRequest, IReadOnlyList<TeamRepositoryOption>>(
                SourceControlCapabilities.TeamRepositoryOptions,
                (_, _) => Task.FromResult<IReadOnlyList<TeamRepositoryOption>>([]));
        var context = runtime.CreateContext(
            organizationId.ToString("D"), productManagerInstallationId.ToString("D"));
        var agent = new ProductManagerAgent(
            NullLogger<ProductManagerAgent>.Instance,
            new ProductManagerOrchestrator(NullLogger<ProductManagerOrchestrator>.Instance));
        var fulfilled = RecommendationFulfilled(organizationId, requestId);

        await agent.HandleHiringRecommendationFulfilledAsync(fulfilled, context, CancellationToken.None);
        await agent.HandleHiringRecommendationFulfilledAsync(fulfilled, context, CancellationToken.None);

        var kickoffKeys = messages
            .Where(x => x.IdempotencyKey == $"software-team-architect-kickoff:{requestId:N}")
            .ToList();
        Assert.Equal(2, kickoffKeys.Count);
        Assert.Single(kickoffKeys.Select(x => x.IdempotencyKey).Distinct());
        Assert.All(kickoffKeys, x => Assert.Contains("<software_team_planning_kickoff>", x.Content, StringComparison.Ordinal));
        Assert.Contains(messages, x => x.IdempotencyKey == $"software-team-kickoff:{requestId:N}");
        Assert.Contains(messages, x => x.Content.Contains("planning has started", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(createdChats, x => x.IsDirect && x.ParticipantOrganizationUserIds.SequenceEqual([architectId]));
    }

    [Fact]
    public void ProductPlan_HasPreferredCourse_TwoAlternatives_AndHiringOrder()
    {
        var brief = new ProductRoleBriefResponse(
            "Ready", Guid.NewGuid(), Guid.NewGuid(), 4, "Own validation",
            ["Validate the first customer problem"], ["Activation"], [], [], [], [], DateTimeOffset.UtcNow);
        var profile = new BusinessProfileResponse(
            Guid.NewGuid(), "Trailwise", "Marketplace", "Outdoor recreation", null, null, "Validation",
            ["New outdoor enthusiasts"], ["Guided trip bookings"], "Commission", ["US"], null, [], [], null,
            "UTC", 4, 0.8m, new Dictionary<string, ProfileFieldProvenance>());
        var context = new ProductOperatingContext(profile, null, null, null, null, brief, []);

        var plan = ProductManagerOrchestrator.BuildProductPlan(
            new ProductPlanRequest(brief, "Initial product team", Guid.NewGuid(), "plan-1"),
            context);

        Assert.False(string.IsNullOrWhiteSpace(plan.Recommendation));
        Assert.Equal(2, plan.Alternatives.Count);
        Assert.NotEmpty(plan.TeamStructure);
        Assert.Equal(
            plan.TeamStructure.Select(x => x.Priority).Order().ToArray(),
            plan.TeamStructure.Select(x => x.Priority).ToArray());
        Assert.Equal(
            ["Software Architect", "Software Developer", "Software QA"],
            plan.TeamStructure.Take(3).Select(x => x.Title).ToArray());
        Assert.All(plan.TeamStructure.Take(3), role => Assert.Equal("Now", role.Timing));
        Assert.All(plan.Alternatives, alternative => Assert.Equal(
            ["Software Architect", "Software Developer", "Software QA"],
            alternative.TeamStructure.Take(3).Select(x => x.Title).ToArray()));
        Assert.All(plan.TeamStructure, role => Assert.Equal(ProductManagerProfile.DefaultDisplayName, role.ReportsTo));
        Assert.NotEmpty(plan.HiringSequence);
        Assert.NotEmpty(plan.Assumptions);
    }

    [Fact]
    public void DeliveryChatParticipants_IncludeEveryActiveMemberAndReportingManagerOnce()
    {
        var productManagerId = Guid.NewGuid();
        var reportingManagerId = Guid.NewGuid();
        var architectId = Guid.NewGuid();
        var developerId = Guid.NewGuid();
        var inactiveId = Guid.NewGuid();
        var team = new AgentTeamContext(
            Guid.NewGuid().ToString("D"),
            "SOFTWARE",
            "Software",
            1,
            productManagerId.ToString("D"),
            "Product Manager",
            [
                new AgentTeammate(productManagerId.ToString("D"), "Product Manager", "Agent", null, "Software Product Manager", "Self", "Active"),
                new AgentTeammate(architectId.ToString("D"), "Architect", "Agent", null, "Software Architect", "Peer", "Active"),
                new AgentTeammate(developerId.ToString("D"), "Developer", "Agent", null, "Software Developer", "Peer", "Active"),
                new AgentTeammate(inactiveId.ToString("D"), "Former QA", "Agent", null, "Software QA", "Peer", "Inactive")
            ],
            [],
            4,
            false);

        var participants = ProductManagerAgent.BuildDeliveryChatParticipants(
            team, productManagerId, reportingManagerId);

        Assert.Equal(4, participants.Count);
        Assert.Contains(productManagerId, participants);
        Assert.Contains(reportingManagerId, participants);
        Assert.Contains(architectId, participants);
        Assert.Contains(developerId, participants);
        Assert.DoesNotContain(inactiveId, participants);
        Assert.Equal(participants.Count, participants.Distinct().Count());
    }

    [Fact]
    public void FirstSprintReadiness_SelectsOnlyStoriesAndTasksFromLowestOrdinalSprint()
    {
        var firstSprintId = Guid.NewGuid();
        var laterSprintId = Guid.NewGuid();
        var firstStory = new PublishedArchitectureTicket("STORY-1", Guid.NewGuid(), firstSprintId, WorkItemKinds.Story);
        var firstTask = new PublishedArchitectureTicket("TASK-1", Guid.NewGuid(), firstSprintId, WorkItemKinds.Task);
        var firstEpic = new PublishedArchitectureTicket("EPIC-1", Guid.NewGuid(), firstSprintId, WorkItemKinds.Epic);
        var laterStory = new PublishedArchitectureTicket("STORY-2", Guid.NewGuid(), laterSprintId, WorkItemKinds.Story);
        var publication = new ArchitecturePublishResponse(
            Guid.NewGuid(),
            firstEpic.ItemId,
            [
                new PublishedArchitectureSprint(2, laterSprintId, "Sprint 2"),
                new PublishedArchitectureSprint(1, firstSprintId, "Sprint 1")
            ],
            [firstStory, firstTask, firstEpic, laterStory],
            DateTimeOffset.UtcNow);

        var ready = ProductManagerAgent.SelectFirstSprintReadyTickets(publication);

        Assert.Equal([firstStory.ItemId, firstTask.ItemId], ready.Select(x => x.ItemId).ToArray());
    }

    [Fact]
    public void ArchitectureAssignmentPool_PreservesExactHumanAndAgentPrincipals()
    {
        var humanId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var installationId = Guid.NewGuid();

        var pool = ProductManagerAgent.BuildArchitectureAssignmentPool(
        [
            new OrganizationPerson(humanId, "Human developer", "Human", null, null, null, true),
            new OrganizationPerson(agentId, "Agent developer", "Agent", null, null, installationId, true)
        ]);

        Assert.Contains(pool, x => x.PrincipalKind == WorkOrchestrationPrincipalKinds.Human &&
                                   x.OrganizationUserId == humanId && x.AgentInstallationId is null);
        Assert.Contains(pool, x => x.PrincipalKind == WorkOrchestrationPrincipalKinds.AgentInstallation &&
                                   x.AgentInstallationId == installationId && x.OrganizationUserId is null);
    }

    [Theory]
    [InlineData(
        "I attempted to submit the team for approval, but the request was blocked by the platform.",
        true)]
    [InlineData(
        "I submitted the team for approval.",
        true)]
    [InlineData(
        "I cannot submit a team until the product goal is defined.",
        false)]
    public void ApprovalActionDetection_RequiresEvidenceForAttemptAndSuccessClaims(
        string response,
        bool expected)
    {
        Assert.Equal(expected, ProductManagerAgent.ClaimsApprovalAction(response));
    }

    [Fact]
    public void ApprovalStatus_RemovesInventedPlatformRejectionWhenNoToolRan()
    {
        var response = ProductManagerAgent.EnsureAccurateApprovalStatus(
            "I attempted to submit the team for approval, but the request was blocked by the platform.",
            toolResult: null);

        Assert.Contains("no durable approval action was attempted", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No approval is pending", response, StringComparison.Ordinal);
        Assert.DoesNotContain("blocked by the platform", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApprovalStatus_UsesTheActualPlatformFailureFromTheTool()
    {
        var response = ProductManagerAgent.EnsureAccurateApprovalStatus(
            "The request failed.",
            ResourceChangeApprovalToolResult.Failure(
                "The platform rejected the approval request: The proposal must originate from a current manager turn."));

        Assert.Contains(
            "The proposal must originate from a current manager turn.",
            response,
            StringComparison.Ordinal);
        Assert.Contains("No approval is pending", response, StringComparison.Ordinal);
    }

    [Fact]
    public void ApprovalStatus_AppendsTheDurableRequestIdAfterSuccess()
    {
        var request = ResourceChange(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Deliver the MVP",
            "Pending");

        var response = ProductManagerAgent.EnsureAccurateApprovalStatus(
            "I submitted the complete team.",
            ResourceChangeApprovalToolResult.Success(request));

        Assert.Contains(request.Id.ToString("D"), response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Pending", response, StringComparison.Ordinal);
    }

    [Fact]
    public void ContextUpdate_WaitsForGaps_AndRefreshesWhenReady()
    {
        var gapBrief = new ProductRoleBriefResponse(
            "AwaitingExecutiveInput", Guid.NewGuid(), Guid.NewGuid(), 1, "Pending",
            [], [], [], [],
            [], [new ProductRoleBriefGap("customer", "Who is the customer?", "Changes product scope.")],
            DateTimeOffset.UtcNow);
        var waiting = ProductManagerOrchestrator.BuildContextUpdateResponse(
            new ProductContextUpdateRequest(gapBrief, Guid.NewGuid(), "update-1"));
        var ready = ProductManagerOrchestrator.BuildContextUpdateResponse(
            new ProductContextUpdateRequest(gapBrief with
            {
                Status = "Ready",
                MissingInformation = []
            }, Guid.NewGuid(), "update-2"));

        Assert.Equal("Waiting", waiting.State);
        Assert.False(waiting.PlanRefreshRequired);
        Assert.Equal("Ready", ready.State);
        Assert.True(ready.PlanRefreshRequired);
    }

    [Fact]
    public void ManagementReport_IsProductFocusedAndConcise()
    {
        var organization = new OrganizationSnapshotResponse(
            Guid.NewGuid(), "Active", [], [], [],
            [new WorkstreamSummary(Guid.NewGuid(), "Launch", "Ship a validated release", "Blocked", "Launch", null,
                DateTimeOffset.UtcNow.AddDays(-1), null, null)],
            [], DateTimeOffset.UtcNow);
        var context = new ProductOperatingContext(null, null, organization, null, null, null, []);
        var request = new ManagementCheckInRequest(
            Guid.NewGuid(), "ManagerRollup", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow,
            [], [], DateTimeOffset.UtcNow.AddHours(2));

        var report = ProductManagerOrchestrator.BuildManagementReport(request, context);

        Assert.Contains("product", report.Markdown!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Launch", report.Blockers);
        Assert.True(report.ImmediateActions.Count <= 5);
        Assert.True(report.ConversationTopics.Count <= 3);
    }

    private static string ManifestPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "csweet-plugin.json");
            if (File.Exists(candidate) &&
                File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("csweet-plugin.json was not found.");
    }

    private static ResourceChangeRole Role(string key, string title, int priority, string timing) =>
        new(
            key,
            "Product",
            title,
            $"Own {title}.",
            1,
            priority,
            timing,
            ["product-delivery"],
            false,
            Guid.NewGuid(),
            null);

    private static ResourceChangeRequestResponse ResourceChange(
        Guid requestId,
        Guid organizationId,
        Guid conversationId,
        string goal,
        string status)
    {
        var requester = Guid.NewGuid();
        var role = Role("engineer", "Product Engineer", 1, "Now") with
        {
            ReportsToOrganizationUserId = requester
        };
        return new ResourceChangeRequestResponse(
            requestId,
            organizationId,
            requester,
            Guid.NewGuid(),
            Guid.NewGuid(),
            conversationId,
            Guid.Empty,
            goal,
            "Smallest complete team.",
            1,
            [role],
            [new ResourceChangeRoleDelta("Add", role, null)],
            [],
            [],
            null,
            status,
            "Delivered",
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static AgentEventEnvelope RecommendationFulfilled(Guid organizationId, Guid sourceRequestId)
    {
        var occurredAt = DateTimeOffset.UtcNow;
        return new AgentEventEnvelope(
            Guid.NewGuid(),
            Guid.NewGuid(),
            HiringEvents.RecommendationFulfilled,
            JsonSerializer.SerializeToElement(new HiringRecommendationFulfilledEvent(
                organizationId,
                Guid.NewGuid(),
                sourceRequestId,
                Guid.NewGuid(),
                "product-engineer",
                "Product Engineer",
                Guid.NewGuid(),
                null,
                1,
                1,
                [Guid.NewGuid()],
                occurredAt)),
            occurredAt);
    }

    private static CommunicationMessage SentMessage(CommunicationSendCapture request) =>
        new(
            Guid.NewGuid(),
            1,
            request.ChatId,
            null,
            ProductManagerProfile.DefaultDisplayName,
            "Agent",
            request.Content,
            DateTimeOffset.UtcNow);

    private sealed record CommunicationSendCapture(
        Guid ChatId,
        string Content,
        string? IdempotencyKey);

    private sealed class TestLlmClientFactory(IChatClient chatClient) : IAgentLlmClientFactory
    {
        public Task<IChatClient> CreateChatClientAsync(
            AgentLlmSelection selection,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(chatClient);
    }

    private sealed class CapturingChatClient(string response) : IChatClient
    {
        public string Prompt { get; private set; } = string.Empty;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Prompt = string.Join("\n", messages.Select(message => message.Text));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, response)));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Prompt = string.Join("\n", messages.Select(message => message.Text));
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, response);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }
}
