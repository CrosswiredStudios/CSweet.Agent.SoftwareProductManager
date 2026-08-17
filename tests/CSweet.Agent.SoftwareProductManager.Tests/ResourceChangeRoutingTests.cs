using CSweet.Agent.SDK;

namespace CSweet.Agent.SoftwareProductManager.Tests;

public sealed class ResourceChangeRoutingTests
{
    [Fact]
    public void ApprovalClaimGuard_ReplacesUnverifiedSubmissionClaim()
    {
        const string draft =
            "I submitted the Lean Technical Spike Team to your manager for approval.";

        var guarded = ProductManagerAgent.EnsureAccurateApprovalStatus(draft, toolResult: null);

        Assert.Contains("no durable approval action was attempted", guarded);
        Assert.Contains("No approval is pending", guarded);
        Assert.DoesNotContain("I submitted", guarded);
    }

    [Fact]
    public void ApprovalClaimGuard_AppendsDurableRequestIdAfterSuccessfulSubmission()
    {
        var requestId = Guid.NewGuid();
        var response = new ResourceChangeRequestResponse(
            requestId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Technical spike",
            "Validate feasibility.",
            1,
            [],
            [],
            [],
            [],
            null,
            "Pending",
            "DeliveredInChat",
            null,
            DateTimeOffset.UtcNow,
            null);

        var guarded = ProductManagerAgent.EnsureAccurateApprovalStatus(
            "I submitted the recommendation for approval.",
            ResourceChangeApprovalToolResult.Success(response));

        Assert.Contains(requestId.ToString("D"), guarded);
        Assert.Contains("Pending", guarded);
    }

    [Fact]
    public void ApprovalMessage_IsTerminalOnlyInItsOriginatingConversationTurn()
    {
        var conversationId = Guid.NewGuid();
        var turnId = Guid.NewGuid();
        var response = new ResourceChangeRequestResponse(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            conversationId,
            turnId,
            "Technical spike",
            "Validate feasibility.",
            1,
            [],
            [],
            [],
            [],
            null,
            "Pending",
            "DeliveredInChat",
            null,
            DateTimeOffset.UtcNow,
            null);

        Assert.True(ProductManagerAgent.ShouldUseApprovalMessageAsTerminal(
            response, conversationId.ToString("D"), turnId));
        Assert.False(ProductManagerAgent.ShouldUseApprovalMessageAsTerminal(
            response, Guid.NewGuid().ToString("D"), turnId));
        Assert.False(ProductManagerAgent.ShouldUseApprovalMessageAsTerminal(
            response, conversationId.ToString("D"), Guid.NewGuid()));
    }

    [Fact]
    public async Task ManagerTurn_RetainsSourceConversationAndTurn()
    {
        var fixture = new RoutingFixture(managerType: "Agent", sourceIsManager: true);

        await fixture.SubmitAsync();

        Assert.Equal(fixture.SourceConversationId, fixture.Proposal!.ConversationId);
        Assert.Equal(fixture.SourceTurnId, fixture.Proposal.ChatTurnId);
        Assert.Equal(0, fixture.ManagerChatCreateCount);
    }

    [Fact]
    public async Task ManagerTurn_WithLegacyTranscriptWithoutTurnId_RetainsSourceConversationAndTurn()
    {
        var fixture = new RoutingFixture(
            managerType: "Human",
            sourceIsManager: true,
            includeTranscriptTurnId: false);

        await fixture.SubmitAsync();

        Assert.Equal(fixture.SourceConversationId, fixture.Proposal!.ConversationId);
        Assert.Equal(fixture.SourceTurnId, fixture.Proposal.ChatTurnId);
        Assert.Equal(0, fixture.ManagerChatCreateCount);
    }

    [Fact]
    public async Task MissingOptionalCollections_AreNormalizedAndSubmitted()
    {
        var fixture = new RoutingFixture(managerType: "Human", sourceIsManager: true);

        await fixture.SubmitAsync(omitOptionalCollections: true);

        Assert.Empty(fixture.Proposal!.Assumptions);
        Assert.Empty(fixture.Proposal.Constraints);
    }

    [Fact]
    public async Task TeamMetadata_IsBoundedBeforeSubmission()
    {
        var fixture = new RoutingFixture(managerType: "Human", sourceIsManager: true);

        await fixture.SubmitAsync(teamName: new string('T', 200));

        Assert.Equal("First Playable Browser Game", fixture.Proposal!.TeamName);
        Assert.Equal("product-team:", fixture.Proposal.TeamKey![..13]);
    }

    [Fact]
    public async Task ExecutiveEmployeeReportingTarget_IsCanonicalizedToProductManager()
    {
        var fixture = new RoutingFixture(managerType: "Human", sourceIsManager: true);
        var role = fixture.Role(
            "game-developer",
            "Game Developer",
            reportsToOrganizationUserId: fixture.ManagerId);

        await fixture.SubmitAsync(roles: [role]);

        var submittedRole = Assert.Single(fixture.Proposal!.Roles);
        Assert.Equal(fixture.ProductManagerId, submittedRole.ReportsToOrganizationUserId);
        Assert.Null(submittedRole.ReportsToRoleKey);
    }

    [Fact]
    public async Task ProposedRoleReportingTarget_IsPreservedAndEmployeeTargetIsCleared()
    {
        var fixture = new RoutingFixture(managerType: "Human", sourceIsManager: true);

        await fixture.SubmitAsync(roles:
        [
            fixture.Role(
                "Lead",
                "Lead Game Developer",
                reportsToOrganizationUserId: fixture.ManagerId),
            fixture.Role(
                "designer",
                "Game Designer",
                reportsToOrganizationUserId: fixture.ManagerId,
                reportsToRoleKey: "LEAD")
        ]);

        var lead = Assert.Single(fixture.Proposal!.Roles, role => role.RoleKey == "lead");
        var designer = Assert.Single(fixture.Proposal.Roles, role => role.RoleKey == "designer");
        Assert.Equal(fixture.ProductManagerId, lead.ReportsToOrganizationUserId);
        Assert.Null(lead.ReportsToRoleKey);
        Assert.Null(designer.ReportsToOrganizationUserId);
        Assert.Equal("lead", designer.ReportsToRoleKey);
    }

    [Fact]
    public async Task UnknownProposedRoleReportingTarget_IsRejectedBeforeSubmission()
    {
        var fixture = new RoutingFixture(managerType: "Human", sourceIsManager: true);

        var exception = await Assert.ThrowsAsync<ResourceChangeRoutingException>(() => fixture.SubmitAsync(roles:
        [
            fixture.Role("designer", "Game Designer", reportsToRoleKey: "missing-lead")
        ]));

        Assert.Contains("not in the proposal", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, fixture.ProposalCallCount);
    }

    [Theory]
    [InlineData("Product Manager")]
    [InlineData("product-manager")]
    [InlineData("self")]
    public async Task RequesterRoleReportingTarget_IsCanonicalizedToProductManager(string requesterRoleKey)
    {
        var fixture = new RoutingFixture(managerType: "Human", sourceIsManager: true);

        await fixture.SubmitAsync(roles:
        [
            fixture.Role("game-developer", "Game Developer", reportsToRoleKey: requesterRoleKey)
        ]);

        var submittedRole = Assert.Single(fixture.Proposal!.Roles);
        Assert.Equal(fixture.ProductManagerId, submittedRole.ReportsToOrganizationUserId);
        Assert.Null(submittedRole.ReportsToRoleKey);
    }

    [Fact]
    public async Task ExecutiveTurn_WithAgentManager_RoutesToProtectedManagerConversation()
    {
        var fixture = new RoutingFixture(managerType: "Agent", sourceIsManager: false);

        await fixture.SubmitAsync();

        Assert.Equal(fixture.ManagerConversationId, fixture.Proposal!.ConversationId);
        Assert.Equal(Guid.Empty, fixture.Proposal.ChatTurnId);
        Assert.Equal(1, fixture.ManagerChatCreateCount);
    }

    [Fact]
    public async Task ExecutiveTurn_WithHumanManager_FailsWithActionableRoutingMessage()
    {
        var fixture = new RoutingFixture(managerType: "Human", sourceIsManager: false);

        var exception = await Assert.ThrowsAsync<ResourceChangeRoutingException>(
            () => fixture.SubmitAsync());

        Assert.Contains("direct conversation", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(fixture.Proposal);
    }

    [Fact]
    public async Task IdenticalRoleSet_UsesStableIdempotencyKeyAcrossExecutiveRetries()
    {
        var fixture = new RoutingFixture(managerType: "Agent", sourceIsManager: false);

        await fixture.SubmitAsync();
        var firstKey = fixture.Proposal!.IdempotencyKey;
        await fixture.SubmitAsync();

        Assert.Equal(firstKey, fixture.Proposal!.IdempotencyKey);
    }

    [Fact]
    public async Task AmbiguousTransportFailure_RetriesOnceWithTheSameIdempotencyKey()
    {
        var fixture = new RoutingFixture(managerType: "Human", sourceIsManager: true, ambiguousFailures: 1);

        await fixture.SubmitAsync();

        Assert.Equal(2, fixture.ProposalCallCount);
        Assert.NotNull(fixture.Proposal);
        Assert.Single(fixture.ProposalKeys.Distinct(StringComparer.Ordinal));
    }

    private sealed class RoutingFixture
    {
        private readonly AgentRuntimeContext _context;
        private readonly ProductOperatingContext _operatingContext;
        private readonly AssistantCapabilityInput _input;

        public RoutingFixture(
            string managerType,
            bool sourceIsManager,
            bool includeTranscriptTurnId = true,
            int ambiguousFailures = 0)
        {
            var organizationId = Guid.NewGuid();
            var installationId = Guid.NewGuid();
            ProductManagerId = Guid.NewGuid();
            ManagerId = Guid.NewGuid();
            var sourceSenderId = sourceIsManager ? ManagerId : Guid.NewGuid();
            SourceConversationId = Guid.NewGuid();
            ManagerConversationId = sourceIsManager ? SourceConversationId : Guid.NewGuid();
            SourceTurnId = Guid.NewGuid();
            var sourceMessageId = Guid.NewGuid();

            var runtime = new AgentTestRuntime()
                .RegisterCapability<object, CommunicationMessages>(
                    ProductManagerProfile.ReadCommunicationCapability,
                    (_, _) => Task.FromResult(new CommunicationMessages(
                    [
                        new CommunicationMessage(
                            sourceMessageId,
                            1,
                            SourceConversationId,
                            sourceSenderId,
                            "Manager",
                            managerType,
                            "Finalize the product team.",
                            DateTimeOffset.UtcNow,
                            includeTranscriptTurnId ? SourceTurnId : null)
                    ])))
                .RegisterCapability<CreateCommunicationChat, CommunicationAction>(
                    ProductManagerProfile.CreateCommunicationCapability,
                    (_, _) =>
                    {
                        ManagerChatCreateCount++;
                        return Task.FromResult(new CommunicationAction(
                            true,
                            null,
                            "Direct chat already exists.",
                            new CommunicationChat(
                                ManagerConversationId,
                                string.Empty,
                                null,
                                true,
                                true,
                                true,
                                false,
                                DateTimeOffset.UtcNow,
                                [],
                                null,
                                null,
                                0)));
                    })
                .RegisterCapability<ResourceChangeProposalRequest, ResourceChangeRequestResponse>(
                    ProductManagerProfile.ProposeResourceChangeCapability,
                    (request, _) =>
                    {
                        ProposalCallCount++;
                        Proposal = request;
                        ProposalKeys.Add(request.IdempotencyKey);
                        if (ProposalCallCount <= ambiguousFailures)
                            throw new HttpRequestException("The response ended before it could be read.");
                        return Task.FromResult(Response(request, organizationId, ProductManagerId, installationId, ManagerId));
                    });

            _context = runtime.CreateContext(
                organizationId.ToString("D"),
                installationId.ToString("D"),
                new AgentIdentity(
                    ProductManagerId.ToString("D"),
                    "Product Manager",
                    null,
                    "Product Manager",
                    null,
                    [],
                    null,
                    ManagerId.ToString("D"),
                    "Chief of Staff"));
            _operatingContext = new ProductOperatingContext(
                null,
                null,
                new OrganizationSnapshotResponse(
                    organizationId,
                    "Active",
                    [
                        new OrganizationPerson(
                            ProductManagerId,
                            "Product Manager",
                            "Agent",
                            null,
                            ManagerId,
                            installationId,
                            true),
                        new OrganizationPerson(
                            ManagerId,
                            "Chief of Staff",
                            managerType,
                            null,
                            null,
                            managerType == "Agent" ? Guid.NewGuid() : null,
                            true)
                    ],
                    [],
                    [],
                    [],
                    [],
                    DateTimeOffset.UtcNow),
                null,
                null,
                null,
                []);
            _input = new AssistantCapabilityInput(
                Guid.NewGuid(),
                SourceConversationId.ToString("D"),
                "There is no budget and we will use free agents.",
                null,
                Guid.NewGuid().ToString("D"),
                sourceMessageId,
                SourceTurnId);
        }

        public Guid SourceConversationId { get; }
        public Guid ManagerConversationId { get; }
        public Guid SourceTurnId { get; }
        public Guid ProductManagerId { get; }
        public Guid ManagerId { get; }
        public int ManagerChatCreateCount { get; private set; }
        public int ProposalCallCount { get; private set; }
        public List<string> ProposalKeys { get; } = [];
        public ResourceChangeProposalRequest? Proposal { get; private set; }

        public async Task SubmitAsync(
            bool omitOptionalCollections = false,
            string teamName = "Product",
            IReadOnlyList<ResourceChangeRole>? roles = null)
        {
            _ = await ProductManagerAgent.RequestResourceChangeApprovalAsync(
                "Validate and ship the first playable browser game",
                "A compact cross-functional team covers implementation, design, and independent quality.",
                1,
                roles ??
                [
                    new ResourceChangeRole(
                        "web3d",
                        teamName,
                        "Lead Web3D Developer",
                        "Own browser rendering and core mechanics.",
                        1,
                        1,
                        "Now",
                        ["web3d-engineering"],
                        false,
                        null,
                        null)
                ],
                omitOptionalCollections ? null : ["Free agents are acceptable."],
                omitOptionalCollections ? null : ["No paid workforce budget."],
                null,
                _input,
                _operatingContext,
                _context,
                CancellationToken.None);
        }

        public ResourceChangeRole Role(
            string roleKey,
            string title,
            Guid? reportsToOrganizationUserId = null,
            string? reportsToRoleKey = null) =>
            new(
                roleKey,
                "Product",
                title,
                $"Own the {title} work.",
                1,
                1,
                "Now",
                ["product-delivery"],
                false,
                reportsToOrganizationUserId,
                reportsToRoleKey);

        private static ResourceChangeRequestResponse Response(
            ResourceChangeProposalRequest request,
            Guid organizationId,
            Guid productManagerId,
            Guid installationId,
            Guid managerId) =>
            new(
                Guid.NewGuid(),
                organizationId,
                productManagerId,
                installationId,
                managerId,
                request.ConversationId,
                request.ChatTurnId,
                request.ProductGoal,
                request.Rationale,
                request.ContextRevision,
                request.Roles,
                request.Roles.Select(x => new ResourceChangeRoleDelta("Add", x, null)).ToList(),
                request.Assumptions,
                request.Constraints,
                request.SupersedesRequestId,
                "Pending",
                "QueuedForManagerAgent",
                null,
                DateTimeOffset.UtcNow,
                null);
    }
}
