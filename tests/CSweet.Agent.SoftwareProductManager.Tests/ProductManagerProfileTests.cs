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
    public void TeamName_DerivesConciseProductIdentityFromLongGoal()
    {
        var name = ProductManagerAgent.DeriveConciseTeamName(
            "Deliver a Babylon.js PoC of a Starfox 64-style web-based 3D space shooter demonstrating core flight feel.");

        Assert.Equal("Starfox PoC", name);
        Assert.True(name.Split(' ').Length <= 6);
        Assert.True(name.Length <= 48);
    }

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
            ProductManagerProfile.SoftwareArchitecturePublishCapability,
            ProductManagerProfile.SoftwareArchitecturePublishStoryTasksCapability
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
        Assert.Contains(WorkSprintCapabilities.Read, requires);
        Assert.Contains(ProductManagerProfile.TeamRosterCapability, requires);
        Assert.Contains(ProductManagerProfile.SoftwareArchitectureDesignCapability, requires);
        Assert.Contains(ProductManagerProfile.SoftwareArchitecturePublishCapability, requires);
        Assert.Contains(ProductManagerProfile.SoftwareArchitecturePublishStoryTasksCapability, requires);
        Assert.Contains(WorkSprintCapabilities.Create, requires);
        Assert.Contains(WorkSprintCapabilities.ManageScope, requires);
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
        Assert.Equal(300, manifest.Runtime.DefaultTickFrequencySeconds);
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
                PersonalTodoEvents.Available,
                CommunicationEvents.MessageMentioned,
                ProductManagerProfile.OnboardedEvent,
                ProductManagerProfile.UserMessageReceivedEvent,
                AgentCoordinationEvents.TurnRequested,
                AgentAttentionEvents.ReviewDue,
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
        Assert.Contains("CSweet.Agent.SDK\" Version=\"3.13.0", project, StringComparison.Ordinal);
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
        Assert.Contains("directly message your CEO manager", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("approved organization and relationship memory", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Never open with a generic readiness message", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Chief of Staff is your executive liaison, not your line manager", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("shares your CEO manager", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Never maintain the Chief's hiring backlog", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not present a finalized role list", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("routes the request to your authoritative CEO manager", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a Chief-triggered update is not manager authorization", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not provide technical architecture", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("primary startup goal", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("software board", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resubmit", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            ProductManagerProfile.SoftwareArchitectureDesignCapability,
            ProductManagerProfile.SystemPrompt,
            StringComparison.Ordinal);
        Assert.Contains(
            ProductManagerProfile.SoftwareArchitecturePublishCapability,
            ProductManagerProfile.SystemPrompt,
            StringComparison.Ordinal);
        Assert.Contains(
            ProductManagerProfile.SoftwareArchitecturePublishStoryTasksCapability,
            ProductManagerProfile.SystemPrompt,
            StringComparison.Ordinal);
        Assert.Contains("direct agent conversation", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("approval boundary", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wake-up signal", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("single PM-owned planning commitment", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("until genuinely blocked", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("one consolidated response", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("product definition", ProductManagerProfile.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChiefLiaison_RequiresAnActiveAgentSharingTheHumanCeo()
    {
        var ceoId = Guid.NewGuid();
        var otherCeoId = Guid.NewGuid();
        var productManager = new OrganizationPerson(
            Guid.NewGuid(), "Product Manager", "Agent", null, ceoId, Guid.NewGuid(), true);
        var chief = new OrganizationPerson(
            Guid.NewGuid(), "Chief of Staff", "Agent", null, ceoId, Guid.NewGuid(), true);
        var unrelatedChief = new OrganizationPerson(
            Guid.NewGuid(), "Chief of Staff West", "Agent", null, otherCeoId, Guid.NewGuid(), true);
        var organization = new OrganizationSnapshotResponse(
            Guid.NewGuid(),
            "Active",
            [
                productManager,
                chief,
                unrelatedChief,
                new OrganizationPerson(ceoId, "CEO", "Human", null, null, null, true),
                new OrganizationPerson(otherCeoId, "Other CEO", "Human", null, null, null, true)
            ],
            [], [], [], [], DateTimeOffset.UtcNow);

        Assert.Equal(chief.Id, ProductManagerAgent.FindChiefLiaison(productManager, organization)?.Id);
        Assert.Equal(ceoId, ProductManagerAgent.FindCeoManager(productManager, organization)?.Id);
        Assert.Null(ProductManagerAgent.FindChiefLiaison(
            productManager with { ReportsToId = Guid.NewGuid() }, organization));
        Assert.Null(ProductManagerAgent.FindCeoManager(
            productManager with { ReportsToId = chief.Id }, organization));
        Assert.Null(ProductManagerAgent.FindChiefLiaison(
            productManager,
            organization with
            {
                People = organization.People.Where(person => person.Id != chief.Id).ToList()
            }));
    }

    [Fact]
    public async Task PersonalTodo_JokeMentionSendsBrokeredDirectMessageAndCompletes()
    {
        var recipientId = Guid.NewGuid();
        CommunicationSendCapture? sent = null;
        var chatId = Guid.NewGuid();
        var runtime = new AgentTestRuntime()
            .RegisterCapability<CreateCommunicationChat, CommunicationAction>(
                CommunicationCapabilities.ChatCreate,
                (request, _) => Task.FromResult(new CommunicationAction(
                    true, null, "Created",
                    new CommunicationChat(chatId, "Matt", request.Description,
                        true, true, false, true, DateTimeOffset.UtcNow,
                        [new CommunicationParticipant(recipientId, "Matt", "Human", "CEO")],
                        null, null, 0))))
            .RegisterCapability<CommunicationSendCapture, CommunicationMessage>(
                CommunicationCapabilities.MessageSend,
                (request, _) =>
                {
                    sent = request;
                    return Task.FromResult(SentMessage(request));
                });
        var agent = new ProductManagerAgent(
            NullLogger<ProductManagerAgent>.Instance,
            new ProductManagerOrchestrator(NullLogger<ProductManagerOrchestrator>.Instance));
        var ownerId = Guid.NewGuid();
        var item = new PersonalTodoItem(
            Guid.NewGuid(), Guid.NewGuid(), ownerId, ownerId, "Matt",
            "Tell @Matt a joke", "Send a joke to Matt through a message",
            PersonalTodoStatuses.Running, WorkPriorities.Critical, 1024, 2,
            null, null, null,
            [new PersonalTodoMention(recipientId, "Matt", "Human")],
            null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var result = await agent.HandlePersonalTodoAsync(
            item, runtime.CreateContext(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(sent);
        Assert.Equal(chatId, sent.ChatId);
        Assert.Contains("backlog", sent.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AttentionBeforeTeamApprovalCreatesStaffingCommitmentWithoutReadingTeamRoster()
    {
        var organizationId = Guid.NewGuid();
        var installationId = Guid.NewGuid();
        var productManagerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var organization = new OrganizationSnapshotResponse(
            organizationId,
            "Active",
            [
                new OrganizationPerson(productManagerId, "Product Manager", "Agent", null,
                    managerId, installationId, true),
                new OrganizationPerson(managerId, "Matt", "Human", null,
                    null, null, true)
            ],
            [], [], [], [], DateTimeOffset.UtcNow);
        var direct = new CommunicationChat(
            conversationId,
            "Product Manager",
            "Private reporting conversation.",
            true,
            true,
            false,
            true,
            DateTimeOffset.UtcNow,
            [
                new CommunicationParticipant(productManagerId, "Product Manager", "Agent", "Product Manager"),
                new CommunicationParticipant(managerId, "Matt", "Human", "CEO")
            ],
            null,
            null,
            0);
        AddPersonalTodoItemRequest? added = null;
        var runtime = new AgentTestRuntime()
            .RegisterCapability<ResourceChangeReadRequest, ResourceChangeReadResponse>(
                PlatformCapabilities.ResourceChangeRead,
                (_, _) => Task.FromResult(new ResourceChangeReadResponse([])))
            .RegisterCapability<object, OrganizationSnapshotResponse>(
                PlatformCapabilities.OrganizationSnapshotRead,
                (_, _) => Task.FromResult(organization))
            .RegisterCapability<object, CommunicationHub>(
                CommunicationCapabilities.ChatRead,
                (_, _) => Task.FromResult(new CommunicationHub(
                    productManagerId, productManagerId, false, true, [direct])))
            .RegisterCapability<object, PersonalTodoDirectory>(
                PersonalTodoCapabilities.Read,
                (_, _) => Task.FromResult(new PersonalTodoDirectory([], productManagerId)))
            .RegisterCapability<AddPersonalTodoItemRequest, PersonalTodoItem>(
                PersonalTodoCapabilities.Add,
                (request, _) =>
                {
                    Assert.Equal(request.SourceConversationId.HasValue, request.SourceMessageId.HasValue);
                    added = request;
                    return Task.FromResult(new PersonalTodoItem(
                        Guid.NewGuid(), Guid.NewGuid(), productManagerId, productManagerId,
                        "Product Manager", request.Title, request.Description ?? string.Empty,
                        PersonalTodoStatuses.Ready, request.Priority, 1024, 1, null,
                        request.SourceConversationId, request.SourceMessageId, [], null, null,
                        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
                    {
                        CorrelationId = request.CorrelationId
                    });
                });
        var context = runtime.CreateContext(
            organizationId.ToString("D"),
            installationId.ToString("D"),
            new AgentIdentity(
                productManagerId.ToString("D"), "Product Manager", null, "Product Manager",
                null, [], null, managerId.ToString("D"), "Matt"));
        var agent = new ProductManagerAgent(
            NullLogger<ProductManagerAgent>.Instance,
            new ProductManagerOrchestrator(NullLogger<ProductManagerOrchestrator>.Instance));
        var now = DateTimeOffset.UtcNow;

        await agent.HandleAttentionReviewAsync(
            new AgentAttentionReviewContext(Guid.NewGuid(), now, now.AddMinutes(5), AgentAttentionReasons.Periodic),
            context,
            CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal($"product-team-staffing:{installationId:N}", added.CorrelationId);
        Assert.Null(added.SourceConversationId);
        Assert.Null(added.SourceMessageId);
    }

    [Fact]
    public async Task ManagerReplyWakesWaitingStaffingCommitmentWithoutStartingGeneralChatGeneration()
    {
        var organizationId = Guid.NewGuid();
        var installationId = Guid.NewGuid();
        var productManagerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var commitment = new PersonalTodoItem(
            Guid.NewGuid(), Guid.NewGuid(), productManagerId, productManagerId,
            "Product Manager", "Recommend initial product team", "Submit the team request.",
            PersonalTodoStatuses.Running, "High", 1024, 3, null, conversationId, null,
            [], null, null, DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow)
        {
            CorrelationId = $"product-team-staffing:{installationId:N}",
            Wait = new PersonalTodoWaitState(
                DateTimeOffset.UtcNow.AddMinutes(5), "Waiting for manager direction.", managerId)
        };
        var organization = new OrganizationSnapshotResponse(
            organizationId, "Active",
            [
                new OrganizationPerson(productManagerId, "Product Manager", "Agent", null,
                    managerId, installationId, true),
                new OrganizationPerson(managerId, "Matt", "Human", null, null, null, true)
            ], [], [], [], [], DateTimeOffset.UtcNow);
        RequeuePersonalTodoItemRequest? requeued = null;
        var runtime = new AgentTestRuntime()
            .RegisterCapability<object, OrganizationSnapshotResponse>(
                PlatformCapabilities.OrganizationSnapshotRead,
                (_, _) => Task.FromResult(organization))
            .RegisterCapability<ResourceChangeReadRequest, ResourceChangeReadResponse>(
                PlatformCapabilities.ResourceChangeRead,
                (_, _) => Task.FromResult(new ResourceChangeReadResponse([])))
            .RegisterCapability<object, PersonalTodoDirectory>(
                PersonalTodoCapabilities.Read,
                (_, _) => Task.FromResult(new PersonalTodoDirectory([
                    new PersonalTodoBoard(Guid.NewGuid(), productManagerId, "Product Manager",
                        managerId, "Matt", 1, [commitment])
                ], productManagerId)))
            .RegisterCapability<RequeuePersonalTodoItemRequest, PersonalTodoItem>(
                PersonalTodoCapabilities.Requeue,
                (request, _) =>
                {
                    requeued = request;
                    return Task.FromResult(commitment with
                    {
                        Status = PersonalTodoStatuses.Ready,
                        Revision = commitment.Revision + 1,
                        Wait = null
                    });
                });
        var context = runtime.CreateContext(
            organizationId.ToString("D"),
            installationId.ToString("D"),
            new AgentIdentity(productManagerId.ToString("D"), "Product Manager", null,
                "Product Manager", null, [], null, managerId.ToString("D"), "Matt"));
        var agent = new ProductManagerAgent(
            NullLogger<ProductManagerAgent>.Instance,
            new ProductManagerOrchestrator(NullLogger<ProductManagerOrchestrator>.Instance));
        var turnId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        await agent.HandleEventAsync(
            new AgentEventEnvelope(
                Guid.NewGuid(), Guid.NewGuid(), ProductManagerProfile.UserMessageReceivedEvent,
                JsonSerializer.SerializeToElement(new CommunicationMessageReceivedEvent(
                    Guid.NewGuid(), conversationId.ToString("D"), managerId.ToString("D"),
                    "Make a Mario Kart clone PoC. There are no budget or time constraints.",
                    new Dictionary<string, string>
                    {
                        [CommunicationMessageContextKeys.SenderEmployeeType] = "Human",
                        [CommunicationMessageContextKeys.SenderOrganizationUserId] = managerId.ToString("D"),
                        [CommunicationMessageContextKeys.SenderDisplayName] = "Matt",
                        [CommunicationMessageContextKeys.SenderRole] = "CEO"
                    },
                    TurnId: turnId,
                    MessageId: messageId)),
                DateTimeOffset.UtcNow),
            context,
            CancellationToken.None);

        Assert.NotNull(requeued);
        Assert.Equal(commitment.Id, requeued.ItemId);
        Assert.Contains(messageId.ToString("N"), requeued.IdempotencyKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StaffingCommitmentWithoutManagerDirectionSendsOneRecoverableIntroductionThenWaits()
    {
        var organizationId = Guid.NewGuid();
        var installationId = Guid.NewGuid();
        var productManagerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var item = new PersonalTodoItem(
            Guid.NewGuid(), Guid.NewGuid(), productManagerId, productManagerId,
            "Product Manager", "Recommend initial product team", "Submit the team request.",
            PersonalTodoStatuses.Running, "High", 1024, 1, null, null, null,
            [], null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        {
            CorrelationId = $"product-team-staffing:{installationId:N}"
        };
        var organization = new OrganizationSnapshotResponse(
            organizationId, "Active",
            [
                new OrganizationPerson(productManagerId, "Product Manager", "Agent", null,
                    managerId, installationId, true),
                new OrganizationPerson(managerId, "Matt", "Human", null, null, null, true)
            ], [], [], [], [], DateTimeOffset.UtcNow);
        var direct = new CommunicationChat(
            conversationId, "Product Manager", "Private reporting conversation.", true, true,
            false, true, DateTimeOffset.UtcNow,
            [
                new CommunicationParticipant(productManagerId, "Product Manager", "Agent", "Product Manager"),
                new CommunicationParticipant(managerId, "Matt", "Human", "CEO")
            ], null, null, 0);
        var transcript = new List<CommunicationMessage>();
        var sends = new List<CommunicationSendCapture>();
        var runtime = new AgentTestRuntime()
            .RegisterCapability<ResourceChangeReadRequest, ResourceChangeReadResponse>(
                PlatformCapabilities.ResourceChangeRead,
                (_, _) => Task.FromResult(new ResourceChangeReadResponse([])))
            .RegisterCapability<object, OrganizationSnapshotResponse>(
                PlatformCapabilities.OrganizationSnapshotRead,
                (_, _) => Task.FromResult(organization))
            .RegisterCapability<JsonElement, JsonElement>(
                CommunicationCapabilities.ChatRead,
                (request, _) => Task.FromResult(request.TryGetProperty("chatId", out var _)
                    ? JsonSerializer.SerializeToElement(new CommunicationMessages(transcript))
                    : JsonSerializer.SerializeToElement(new CommunicationHub(
                        productManagerId, productManagerId, false, true, [direct]))))
            .RegisterCapability<CommunicationSendCapture, CommunicationMessage>(
                ProductManagerProfile.SendCommunicationMessageCapability,
                (request, _) =>
                {
                    sends.Add(request);
                    var sent = new CommunicationMessage(
                        Guid.NewGuid(), transcript.Count + 1, request.ChatId, productManagerId,
                        "Product Manager", "Agent", request.Content, DateTimeOffset.UtcNow,
                        Guid.NewGuid());
                    transcript.Add(sent);
                    return Task.FromResult(sent);
                });
        var context = runtime.CreateContext(
            organizationId.ToString("D"), installationId.ToString("D"),
            new AgentIdentity(productManagerId.ToString("D"), "Product Manager", null,
                "Product Manager", null, [], null, managerId.ToString("D"), "Matt"));
        var agent = new ProductManagerAgent(
            NullLogger<ProductManagerAgent>.Instance,
            new ProductManagerOrchestrator(NullLogger<ProductManagerOrchestrator>.Instance));

        var first = await agent.HandlePersonalTodoAsync(item, context, CancellationToken.None);
        var replay = await agent.HandlePersonalTodoAsync(item, context, CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(replay);
        var sent = Assert.Single(sends);
        Assert.Equal(conversationId, sent.ChatId);
        Assert.Equal($"product-manager-onboarding-direction:{installationId:N}", sent.IdempotencyKey);
    }

    [Fact]
    public void BoundedHiringPromptIncludesOnlyCompactDecisionContext()
    {
        var business = new BusinessProfileResponse(
            OrganizationId: Guid.NewGuid(),
            Name: "Super Awesome Games",
            BusinessType: "Game Studio",
            Industry: "Video Games",
            Description: null,
            Mission: "We make great web games",
            LifecycleStage: "Validation",
            TargetCustomers: ["casual browser players"],
            Offerings: ["browser games"],
            RevenueModel: null,
            Jurisdictions: [],
            OperatingStyle: null,
            Constraints: ["Use approved providers"],
            Tools: [],
            RiskPreference: null,
            TimeZone: "UTC",
            Revision: 7,
            Completeness: 0.8m,
            Provenance: new Dictionary<string, ProfileFieldProvenance>());
        var context = new ProductOperatingContext(business, null, null, null, null, null, []);

        var prompt = ProductManagerAgent.BuildBoundedHiringPrompt(
            "Make a Mario Kart clone PoC with no time or budget constraints.", context);

        Assert.Contains("Mario Kart clone PoC", prompt, StringComparison.Ordinal);
        Assert.Contains("casual browser players", prompt, StringComparison.Ordinal);
        Assert.Contains("Context revision: 7", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("organization snapshot", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.True(prompt.Length < 3_000, $"Bounded hiring prompt was {prompt.Length} characters.");
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
        var incoming = new CommunicationMessageReceivedEvent(
            Guid.NewGuid(),
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"),
            "The architecture role is ready.",
            new Dictionary<string, string>
            {
                [CommunicationMessageContextKeys.SenderEmployeeType] = "Agent",
                [CommunicationMessageContextKeys.SenderRole] = "Software Architect",
                [CommunicationMessageContextKeys.SenderDisplayName] = "C-Sweet Software Architect"
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
    public async Task ArchitectReadinessAndAttention_ImmediatelyWakePersistedWaitingCommitmentExactlyOnce()
    {
        var organizationId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var productManagerId = Guid.NewGuid();
        var productManagerInstallationId = Guid.NewGuid();
        var architectId = Guid.NewGuid();
        var architectInstallationId = Guid.NewGuid();
        var chatId = Guid.NewGuid();
        var readinessTurnId = Guid.NewGuid();
        var readinessMessageId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var personalBoardId = Guid.NewGuid();
        var role = Role("architecture", "Software Architect", 1, "Now") with
        {
            ReportsToOrganizationUserId = productManagerId
        };
        var approved = new ResourceChangeRequestResponse(
            requestId, organizationId, productManagerId, productManagerInstallationId, managerId,
            chatId, Guid.NewGuid(), "Deliver a playable web game prototype",
            "Create the smallest approved planning team.", 1, [role],
            [new ResourceChangeRoleDelta("Add", role, null)], [], [], null,
            "Approved", "Delivered", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        {
            TeamId = teamId,
            TeamName = "Web Games"
        };
        var team = new AgentTeamContext(
            teamId.ToString("D"), "web-games", "Web Games", 1,
            productManagerId.ToString("D"), "Product Manager",
            [
                new AgentTeammate(productManagerId.ToString("D"), "Product Manager", "Agent", null,
                    "Software Product Manager", "Self", "Active"),
                new AgentTeammate(architectId.ToString("D"), "Software Architect", "Agent", null,
                    "Software Architect", "DirectReport", "Active")
            ],
            [new TeamRoleCoverage("Software Architect", 1)], 2, false);
        var organization = new OrganizationSnapshotResponse(
            organizationId, "Active",
            [
                new OrganizationPerson(productManagerId, "Product Manager", "Agent", null, managerId,
                    productManagerInstallationId, true),
                new OrganizationPerson(managerId, "Manager", "Human", null, null, null, true),
                new OrganizationPerson(architectId, "Software Architect", "Agent", null, productManagerId,
                    architectInstallationId, true)
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
        var board = new WorkBoardSummary(boardId, "Web Games", "Approved", false, false, 1, [])
        {
            TeamId = teamId,
            ManagerOrganizationUserId = productManagerId
        };
        var direct = new CommunicationChat(
            chatId, "Software Architect", "Private direct conversation.", true, true, false, true,
            DateTimeOffset.UtcNow,
            [
                new CommunicationParticipant(productManagerId, "Product Manager", "Agent", "Software Product Manager"),
                new CommunicationParticipant(architectId, "Software Architect", "Agent", "Software Architect")
            ], null, null, 0);
        var readiness = new CommunicationMessage(
            readinessMessageId, 1, chatId, architectId, "Software Architect", "Agent",
            "I’m onboarded and ready to begin working with you on the product plan and kanban backlog.",
            DateTimeOffset.UtcNow.AddMinutes(-5), readinessTurnId);
        AddPersonalTodoItemRequest? addedCommitment = null;
        var commitment = new PersonalTodoItem(
            Guid.NewGuid(), personalBoardId, productManagerId, productManagerId, "Product Manager",
            "Complete PM–Architect planning",
            "Reconcile the approved product plan with the Software Architect and publish the provisional backlog.",
            PersonalTodoStatuses.Running, "High", 1024, 1, null, null, null, [], null, null,
            DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow.AddMinutes(-5))
        {
            CorrelationId = $"product-architect-planning:{teamId:N}",
            Wait = new PersonalTodoWaitState(
                DateTimeOffset.UtcNow.AddMinutes(25),
                "Waiting for the Software Architect's onboarding readiness response.",
                architectId)
        };
        var requeueCount = 0;
        AgentCoordinationSession? session = null;
        var coordinationStarts = new List<StartAgentCoordinationRequest>();
        var sentMessages = new List<CommunicationSendCapture>();

        var runtime = new AgentTestRuntime()
            .RegisterCapability<ResourceChangeReadRequest, ResourceChangeReadResponse>(
                PlatformCapabilities.ResourceChangeRead,
                (_, _) => Task.FromResult(new ResourceChangeReadResponse([approved])))
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
            .RegisterCapability<CreateWorkItemRequest, WorkItem>(
                WorkItemCapabilities.Create,
                (request, _) => Task.FromResult(new WorkItem(
                    Guid.NewGuid(), columns[0].Id, request.ParentItemId, null, request.Kind,
                    request.Title, request.Description ?? string.Empty, "Backlog", request.Priority,
                    null, 1, 1, null) { Planning = request.Planning }))
            .RegisterCapability<ConfigureSoftwareOrchestrationTemplateRequest, WorkOrchestrationPolicyRevision>(
                WorkOrchestrationCapabilities.ConfigureSoftwareTemplate,
                (request, _) => Task.FromResult(new WorkOrchestrationPolicyRevision(
                    Guid.NewGuid(), Guid.NewGuid(), request.BoardId, 1, "Software delivery", "ready",
                    request.MergeMode, new WorkOrchestrationConcurrencyLimits(100, 25, 10, 5, 1),
                    [], [], true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)))
            .RegisterCapability<object, PersonalTodoDirectory>(
                PersonalTodoCapabilities.Read,
                (_, _) => Task.FromResult(new PersonalTodoDirectory([
                    new PersonalTodoBoard(personalBoardId, productManagerId, "Product Manager", managerId,
                        "Manager", 1, [commitment])
                ], productManagerId)))
            .RegisterCapability<AddPersonalTodoItemRequest, PersonalTodoItem>(
                PersonalTodoCapabilities.Add,
                (request, _) =>
                {
                    addedCommitment = request;
                    return Task.FromResult(commitment);
                })
            .RegisterCapability<RequeuePersonalTodoItemRequest, PersonalTodoItem>(
                PersonalTodoCapabilities.Requeue,
                (request, _) =>
                {
                    Assert.Equal(commitment.Id, request.ItemId);
                    Assert.Equal(commitment.Revision, request.ExpectedRevision);
                    requeueCount++;
                    commitment = commitment with
                    {
                        Status = PersonalTodoStatuses.Ready,
                        Revision = commitment.Revision + 1,
                        UpdatedAt = DateTimeOffset.UtcNow,
                        Wait = null
                    };
                    return Task.FromResult(commitment);
                })
            .RegisterCapability<JsonElement, object>(
                CommunicationCapabilities.ChatRead,
                (request, _) => Task.FromResult(request.TryGetProperty("chatId", out var chatIdProperty)
                    ? (object)new CommunicationMessages([readiness])
                    : new CommunicationHub(productManagerId, productManagerId, false, true, [direct])))
            .RegisterCapability<ListAgentCoordinationRequest, AgentCoordinationSessions>(
                CommunicationCapabilities.CoordinationList,
                (_, _) => Task.FromResult(new AgentCoordinationSessions(
                    session is null ? [] : [session])))
            .RegisterCapability<StartAgentCoordinationRequest, AgentCoordinationSession>(
                CommunicationCapabilities.CoordinationStart,
                (request, _) =>
                {
                    coordinationStarts.Add(request);
                    session ??= new AgentCoordinationSession(
                        Guid.NewGuid(), request.SourceConversationId, request.SourceConversationId,
                        request.SourceChatTurnId, request.SourceMessageId,
                        new AgentCoordinationParticipant(productManagerId, productManagerInstallationId,
                            "Product Manager", "Software Product Manager"),
                        new AgentCoordinationParticipant(architectId, architectInstallationId,
                            "Software Architect", "Software Architect"),
                        request.Subject, request.Objective, request.SuccessCriteria,
                        AgentCoordinationStatuses.Active, 1, 1, architectId, false, null,
                        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, []);
                    return Task.FromResult(session);
                })
            .RegisterCapability<CommunicationSendCapture, CommunicationMessage>(
                CommunicationCapabilities.MessageSend,
                (request, _) =>
                {
                    sentMessages.Add(request);
                    return Task.FromResult(SentMessage(request));
                });
        var context = runtime.CreateContext(
            organizationId.ToString("D"), productManagerInstallationId.ToString("D"));
        var agent = new ProductManagerAgent(
            NullLogger<ProductManagerAgent>.Instance,
            new ProductManagerOrchestrator(NullLogger<ProductManagerOrchestrator>.Instance));
        var now = DateTimeOffset.UtcNow;
        var review = new AgentAttentionReviewContext(
            Guid.NewGuid(), now, now.AddMinutes(5), AgentAttentionReasons.Recovered);

        await agent.HandleEventAsync(
            new AgentEventEnvelope(
                Guid.NewGuid(),
                Guid.NewGuid(),
                ProductManagerProfile.UserMessageReceivedEvent,
                JsonSerializer.SerializeToElement(new CommunicationMessageReceivedEvent(
                    Guid.NewGuid(),
                    chatId.ToString("D"),
                    architectId.ToString("D"),
                    readiness.Content,
                    new Dictionary<string, string>
                    {
                        [CommunicationMessageContextKeys.SenderEmployeeType] = "Agent",
                        [CommunicationMessageContextKeys.SenderOrganizationUserId] = architectId.ToString("D"),
                        [CommunicationMessageContextKeys.SenderRole] = "Software Architect",
                        [CommunicationMessageContextKeys.SenderDisplayName] = "Software Architect"
                    },
                    readinessMessageId,
                    1,
                    readinessTurnId)),
                now),
            context,
            CancellationToken.None);
        await agent.HandleAttentionReviewAsync(review, context, CancellationToken.None);
        var firstResult = await agent.HandlePersonalTodoAsync(commitment!, context, CancellationToken.None);
        var replayResult = await agent.HandlePersonalTodoAsync(commitment!, context, CancellationToken.None);

        Assert.Null(addedCommitment);
        Assert.Equal(1, requeueCount);
        Assert.Equal($"product-architect-planning:{teamId:N}", commitment!.CorrelationId);
        Assert.NotNull(firstResult);
        Assert.NotNull(replayResult);
        var start = Assert.Single(coordinationStarts);
        Assert.Equal(readinessMessageId, start.SourceMessageId);
        Assert.Equal(readinessTurnId, start.SourceChatTurnId);
        Assert.Equal($"product-architect-planning:{teamId:N}", start.IdempotencyKey);
        Assert.Empty(sentMessages);
        Assert.Contains("Welcome aboard", start.InitialMessage, StringComparison.Ordinal);
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

        Assert.Contains("software_architecture_design_v2", names);
        Assert.Contains("software_architecture_publish_plan_v2", names);
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
        AddPersonalTodoItemRequest? staffingCommitment = null;
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
            .RegisterCapability<object, PersonalTodoDirectory>(
                PersonalTodoCapabilities.Read,
                (_, _) => Task.FromResult(new PersonalTodoDirectory([], productManagerId)))
            .RegisterCapability<AddPersonalTodoItemRequest, PersonalTodoItem>(
                PersonalTodoCapabilities.Add,
                (request, _) =>
                {
                    Assert.Equal(request.SourceConversationId.HasValue, request.SourceMessageId.HasValue);
                    staffingCommitment = request;
                    return Task.FromResult(new PersonalTodoItem(
                        Guid.NewGuid(), Guid.NewGuid(), productManagerId, productManagerId,
                        ProductManagerProfile.DefaultDisplayName, request.Title,
                        request.Description ?? string.Empty, PersonalTodoStatuses.Ready,
                        request.Priority, 1024, 1, null, request.SourceConversationId,
                        request.SourceMessageId, [], null, null, DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow)
                    {
                        CorrelationId = request.CorrelationId
                    });
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
        Assert.NotNull(staffingCommitment);
        Assert.Null(staffingCommitment.SourceConversationId);
        Assert.Null(staffingCommitment.SourceMessageId);
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
        var sessionId = Guid.NewGuid();
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
            .RegisterCapability<ReadAgentCoordinationRequest, AgentCoordinationSession>(
                CommunicationCapabilities.CoordinationRead,
                (_, _) => Task.FromResult(new AgentCoordinationSession(
                    sessionId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                    new AgentCoordinationParticipant(productManagerId, productInstallationId,
                        "Product Manager", "Product Manager"),
                    new AgentCoordinationParticipant(architectId, architectInstallationId,
                        "Architect", "Software Architect"),
                    "Demo delivery", "Complete the demo", ["The demo passes acceptance tests."],
                    AgentCoordinationStatuses.Active, 3, 3, productManagerId, false, null,
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [])))
            .RegisterCapability<ArchitectureDesignRequest, JsonElement>(
                ProductManagerProfile.SoftwareArchitectureDesignCapability,
                (_, _) => Task.FromResult(JsonSerializer.SerializeToElement(new
                {
                    plan = new { blockingQuestions = new[] { "Which browser performance target is authoritative?" } }
                })))
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
        Assert.Contains("outcome Epics", first.Content, StringComparison.OrdinalIgnoreCase);

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
                        "Epic proposal: Core Product Outcome and Delivery Confidence.", now)
                ]),
            runtime.CreateContext(), CancellationToken.None);

        Assert.Equal(AgentCoordinationDispositions.Continue, second.Disposition);
        Assert.Contains("Stories", second.Content, StringComparison.OrdinalIgnoreCase);

        var third = await agent.HandleCoordinationTurnAsync(
            new AgentCoordinationTurnRequest(
                sessionId, 5, 5, "Demo delivery", "Complete the demo",
                ["The demo passes acceptance tests."], self, architect, false,
                [
                    initial,
                    new AgentCoordinationTurn(Guid.NewGuid(), 1, productManagerId,
                        AgentCoordinationDispositions.Continue, first.Content, now),
                    new AgentCoordinationTurn(Guid.NewGuid(), 2, architectId,
                        AgentCoordinationDispositions.Continue,
                        "Epic proposal: Core Product Outcome and Delivery Confidence.", now),
                    new AgentCoordinationTurn(Guid.NewGuid(), 3, productManagerId,
                        AgentCoordinationDispositions.Continue, second.Content, now),
                    new AgentCoordinationTurn(Guid.NewGuid(), 4, architectId,
                        AgentCoordinationDispositions.Continue,
                        "Story and sprint proposal: deliver the browser path before hardening.", now)
                ]),
            runtime.CreateContext(), CancellationToken.None);

        Assert.Equal(AgentCoordinationDispositions.Continue, third.Disposition);
        Assert.Contains("Task decomposition", third.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Coordination_UsesV2ProviderAndCompletesAfterHierarchicalPublication()
    {
        var productManagerId = Guid.NewGuid();
        var productInstallationId = Guid.NewGuid();
        var architectId = Guid.NewGuid();
        var architectInstallationId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        var sprintId = Guid.NewGuid();
        var epicId = Guid.NewGuid();
        var storyId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var published = false;
        var designCalls = new List<ArchitectureDesignRequest>();
        var publishCalls = new List<GuardedArchitecturePublishRequest>();
        var boardSummary = new WorkBoardSummary(
            boardId, "Demo Delivery", "Approved board", false, false, 1, [])
        {
            TeamId = teamId,
            ManagerOrganizationUserId = productManagerId
        };
        var planning = new WorkItemPlanningSpecification(
            ["Deliver the demo."], ["The demo passes."], ["Verify the browser path."]);
        var runtime = new AgentTestRuntime()
            .RegisterCapability<TeamRosterRequest, TeamRosterResponse>(
                ProductManagerProfile.TeamRosterCapability,
                (_, _) => Task.FromResult(new TeamRosterResponse(new AgentTeamContext(
                    teamId.ToString("D"), "demo", "Demo Delivery", 1,
                    productManagerId.ToString("D"), "Product Manager",
                    [
                        new AgentTeammate(productManagerId.ToString("D"), "Product Manager", "Agent",
                            null, "Product Manager", "Self", "Active"),
                        new AgentTeammate(architectId.ToString("D"), "Architect", "Agent",
                            null, "Software Architect", "DirectReport", "Active")
                    ], [], 2, false))))
            .RegisterCapability<WorkBoardListRequest, IReadOnlyList<WorkBoardSummary>>(
                WorkBoardCapabilities.Read,
                (_, _) => Task.FromResult<IReadOnlyList<WorkBoardSummary>>([boardSummary]))
            .RegisterCapability<CreateWorkItemRequest, WorkItem>(
                WorkItemCapabilities.Create,
                (request, _) => Task.FromResult(new WorkItem(
                    Guid.NewGuid(), columnId, request.ParentItemId, null, request.Kind,
                    request.Title, request.Description ?? string.Empty, "Backlog", request.Priority,
                    null, 1, 1, null) { Planning = request.Planning }))
            .RegisterCapability<WorkBoardReference, WorkBoardDetail>(
                WorkItemCapabilities.Read,
                (_, _) => Task.FromResult(new WorkBoardDetail(
                    boardSummary,
                    [new WorkBoardColumn(columnId, "Backlog", "Backlog", 1, "Pull", null)],
                    published
                        ?
                        [
                            new WorkItem(epicId, columnId, null, null, WorkItemKinds.Epic,
                                "Playable Demo", "Outcome", "Backlog", WorkPriorities.High, null, 1, 1, null),
                            new WorkItem(storyId, columnId, epicId, sprintId, WorkItemKinds.Story,
                                "Complete a race", "Story", "Backlog", WorkPriorities.High, null, 2, 1, null)
                            { Planning = planning },
                            new WorkItem(taskId, columnId, storyId, sprintId, WorkItemKinds.Task,
                                "Implement race loop", """
## Objective
Implement the race loop.
## Context
Child of the playable demo Story.
## Requirements
- Deliver deterministic race state.
## Acceptance criteria
- The demo passes.
## Interfaces and data
- Keep the browser boundary explicit.
## Ordered implementation guidance
- Implement state, then rendering.
## Tests
- Cover success and failure paths.
## Dependencies
- None.
## Constraints
- Remain browser portable.
## Migration and rollback
Revert the isolated state module.
## Definition of done
- Acceptance evidence is attached.
""", "Backlog", WorkPriorities.High, null, 3, 1, null)
                            { Planning = planning }
                        ]
                        : [])))
            .RegisterCapability<WorkBoardReference, IReadOnlyList<WorkSprint>>(
                WorkSprintCapabilities.Read,
                (_, _) => Task.FromResult<IReadOnlyList<WorkSprint>>(
                [
                    new WorkSprint(sprintId, boardId, "Sprint 1", "Playable loop", "Planned",
                        null, null, null, null, null, published ? 2 : 0, 0, 0, 0, 1)
                    { Sequence = 1 }
                ]))
            .RegisterCapability<ReadAgentCoordinationRequest, AgentCoordinationSession>(
                CommunicationCapabilities.CoordinationRead,
                (_, _) => Task.FromResult(new AgentCoordinationSession(
                    sessionId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                    new AgentCoordinationParticipant(productManagerId, productInstallationId,
                        "Product Manager", "Product Manager"),
                    new AgentCoordinationParticipant(architectId, architectInstallationId,
                        "Architect", "Software Architect"),
                    "Demo Delivery", "Complete the demo", ["The demo passes acceptance tests."],
                    AgentCoordinationStatuses.Active, 2, 2, productManagerId, false, null,
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [])))
            .RegisterCapability<object, PersonalTodoDirectory>(
                PersonalTodoCapabilities.Read,
                (_, _) => Task.FromResult(new PersonalTodoDirectory([], productManagerId)))
            .RegisterCapability<AddPersonalTodoItemRequest, PersonalTodoItem>(
                PersonalTodoCapabilities.Add,
                (request, _) => Task.FromResult(new PersonalTodoItem(
                    Guid.NewGuid(), Guid.NewGuid(), productManagerId, productManagerId, "Product Manager",
                    request.Title, request.Description ?? string.Empty, PersonalTodoStatuses.Ready,
                    request.Priority, 2048, 1, request.DueDate, request.SourceConversationId,
                    request.SourceMessageId, [], null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
                { CorrelationId = request.CorrelationId }))
            .RegisterCapability<ArchitectureDesignRequest, JsonElement>(
                ProductManagerProfile.SoftwareArchitectureDesignCapability,
                (request, _) =>
                {
                    designCalls.Add(request);
                    return Task.FromResult(JsonSerializer.SerializeToElement(new
                    {
                        planId = Guid.NewGuid(),
                        plan = new { blockingQuestions = Array.Empty<string>() }
                    }));
                })
            .RegisterCapability<GuardedArchitecturePublishRequest, ArchitecturePublishResponse>(
                ProductManagerProfile.SoftwareArchitecturePublishCapability,
                (request, _) =>
                {
                    publishCalls.Add(request);
                    published = true;
                    return Task.FromResult(new ArchitecturePublishResponse(
                        Guid.NewGuid(), epicId,
                        [new PublishedArchitectureSprint(1, sprintId, "Sprint 1")],
                        [
                            new PublishedArchitectureTicket("STORY-1", storyId, sprintId, WorkItemKinds.Story),
                            new PublishedArchitectureTicket("TASK-1", taskId, sprintId, WorkItemKinds.Task)
                        ],
                        DateTimeOffset.UtcNow)
                    {
                        Epics = [new PublishedArchitectureEpic("EPIC-1", epicId, "Playable Demo")]
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

        var result = await agent.HandleCoordinationTurnAsync(
            new AgentCoordinationTurnRequest(
                sessionId, 2, 2, "Demo Delivery", "Complete the demo",
                ["The demo passes acceptance tests."], self, architect, false,
                [
                    new AgentCoordinationTurn(Guid.NewGuid(), 0, productManagerId,
                        AgentCoordinationDispositions.Continue,
                        "Welcome aboard. Build the complete outcome backlog.", now),
                    new AgentCoordinationTurn(Guid.NewGuid(), 1, architectId,
                        AgentCoordinationDispositions.Continue,
                        "Task decomposition complete: browser boundary, deterministic race state, and dependency-ordered slices.", now)
                ]),
            runtime.CreateContext(), CancellationToken.None);

        Assert.Equal(AgentCoordinationDispositions.Continue, result.Disposition);
        Assert.Empty(designCalls);
        Assert.Empty(publishCalls);
        Assert.NotNull(result.Artifact);
        Assert.Equal(IncrementalPlanningArtifactTypes.ProductBrief, result.Artifact!.Type);
        Assert.Contains("persisted the outcome Epics", result.Content, StringComparison.OrdinalIgnoreCase);
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
        Assert.Equal("Web Games", ProductManagerAgent.BuildProductBoardName("We make amazing web games"));
        Assert.Equal("Product Work", ProductManagerAgent.BuildProductBoardName(new string('x', 300)));
        Assert.True(ProductManagerAgent.IsValidProductBoardName("Creator Onboarding"));
        Assert.False(ProductManagerAgent.IsValidProductBoardName("Creator Onboarding Kanban Board"));
        Assert.True(ProductManagerAgent.BuildProductBoardName("Creator onboarding").Length <= 32);
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
        Assert.Contains("Remaining: None", messageRequest.Content, StringComparison.OrdinalIgnoreCase);
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
        var coordinationRequests = new List<StartAgentCoordinationRequest>();
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
            .RegisterCapability<ConfigureWorkBoardRequest, WorkBoardSummary>(
                WorkBoardCapabilities.Configure,
                (request, _) => Task.FromResult(board with
                {
                    Name = request.Name,
                    Description = request.Description,
                    Revision = request.ExpectedRevision + 1
                }))
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
            .RegisterCapability<StartAgentCoordinationRequest, AgentCoordinationSession>(
                CommunicationCapabilities.CoordinationStart,
                (request, _) =>
                {
                    coordinationRequests.Add(request);
                    return Task.FromResult(new AgentCoordinationSession(
                        Guid.NewGuid(), request.SourceConversationId, request.SourceConversationId,
                        request.SourceChatTurnId, request.SourceMessageId,
                        new AgentCoordinationParticipant(productManagerId, productManagerInstallationId,
                            "Product Manager", "Software Product Manager"),
                        new AgentCoordinationParticipant(architectId, architectInstallationId,
                            "Architect", "Software Architect"),
                        request.Subject, request.Objective, request.SuccessCriteria,
                        AgentCoordinationStatuses.Active, 1, 1, architectId, false, null,
                        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, []));
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

        Assert.DoesNotContain(messages, x =>
            x.IdempotencyKey == $"software-team-architect-reconcile:{teamId:N}:d1:q1");
        Assert.Empty(coordinationRequests);
        Assert.DoesNotContain(createdChats, x =>
            x.IsDirect && x.ParticipantOrganizationUserIds.SequenceEqual([architectId]));
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
    public void RefreshedTeamPlan_RoutesBackToTheCeoForADirectApprovalTurn()
    {
        var plan = new ProductPlanResponse(
            "Ship the first validated release",
            [], [], [], [], [], [], [], [],
            4,
            DateTimeOffset.UtcNow);

        var message = ProductManagerAgent.BuildCeoTeamReviewRequest(plan, "refreshed");

        Assert.Contains("Product Manager-authored team design", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reconciled it with the Chief of Staff", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("you are the CEO and approval authority", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("your direct instruction in this conversation", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("submitted", message, StringComparison.OrdinalIgnoreCase);
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
