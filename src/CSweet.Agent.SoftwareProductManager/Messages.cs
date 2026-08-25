using System.Text.Json;
using CSweet.Agent.SDK;

namespace CSweet.Agent.SoftwareProductManager;

public sealed record AssistantCapabilityInput(
    Guid ProviderProfileId,
    string ConversationId,
    string Prompt,
    IReadOnlyDictionary<string, string>? Context,
    string? UserId = null,
    Guid MessageId = default,
    Guid ChatTurnId = default);

public sealed record AssistantResponseCreated(
    string ConversationId,
    string Response,
    IReadOnlyList<ProposedAction> ProposedActions,
    DateTimeOffset CreatedAt);

public sealed record ProposedAction(
    string ActionType,
    string Summary,
    string ParametersJson,
    bool RequiresApproval);

public sealed record AssistantResponseChunk(
    string ConversationId,
    int Sequence,
    string Delta,
    bool IsFinal,
    string? Error = null,
    Guid TurnId = default,
    string Kind = "output",
    IReadOnlyDictionary<string, string>? Metadata = null,
    int Attempt = 0);

public sealed record ProductOperatingContext(
    BusinessProfileResponse? BusinessProfile,
    FinancialOperatingProfileResponse? FinancialProfile,
    OrganizationSnapshotResponse? Organization,
    BusinessPatternSearchResponse? Patterns,
    ManagementCycleResponse? ManagementCycle,
    ProductRoleBriefResponse? RoleBrief,
    IReadOnlyList<string> UnavailableCapabilities);

public sealed record ProductCharterCheckpoint(
    int Version,
    string OwnedOutcome,
    IReadOnlyList<string> TargetCustomers,
    IReadOnlyList<string> SuccessMeasures,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<string> NonGoals,
    IReadOnlyList<string> ManagerDecisions,
    IReadOnlyDictionary<string, string> SourceRevisions);

public sealed record ProductManagerRoleHealth(
    string RoleKey,
    string RoleCategoryKey,
    string RoleTitle,
    int DesiredHeadcount,
    int EffectiveHeadcount,
    IReadOnlyList<string> Evidence,
    bool Vital);

public sealed record ProductManagerOperatingAssessment(
    string MandateHealth,
    string TeamHealth,
    string PlanningHealth,
    string DeliveryHealth,
    IReadOnlyList<string> Conditions,
    IReadOnlyList<ProductManagerRoleHealth> Roles,
    ProductCharterCheckpoint Charter,
    IReadOnlyList<string> ActionsCreated,
    string AttentionReason,
    string ExecutionPath,
    long DurationMilliseconds,
    DateTimeOffset AssessedAt);

public sealed record ArchitecturePublicationApproval(
    string ApproverRole,
    string Rationale,
    DateTimeOffset ApprovedAt,
    Guid? SourceConversationId = null,
    Guid? SourceMessageId = null);

public sealed record ArchitectureDesignRequest(
    Guid BoardId,
    string ProductGoal,
    IReadOnlyList<string> Requirements,
    IReadOnlyList<string> AcceptanceCriteria,
    string IdempotencyKey,
    IReadOnlyList<string>? Constraints = null,
    IReadOnlyList<string>? NonGoals = null,
    IReadOnlyList<string>? QualityAttributes = null,
    DateTimeOffset? DesiredStartAt = null,
    int? SprintLengthDays = null,
    Guid? SourceConversationId = null)
{
    public bool RollingRefinement { get; init; }
}

public sealed record GuardedArchitecturePublishRequest(
    Guid BoardId,
    JsonElement Design,
    ArchitecturePublicationApproval Approval,
    string IdempotencyKey)
{
    public Guid RepositoryId { get; init; }
    public string BaseBranch { get; init; } = string.Empty;
    public int FirstSprintSequence { get; init; }
    public Guid AccountableOrganizationUserId { get; init; }
    public Guid DeveloperInstallationId { get; init; }
    public Guid QualityInstallationId { get; init; }
    public IReadOnlyList<Guid> DeveloperInstallationIds { get; init; } = [];
    public IReadOnlyList<Guid> QualityInstallationIds { get; init; } = [];
    public IReadOnlyList<ArchitectureAssignmentPrincipal> DeveloperAssignments { get; init; } = [];
    public IReadOnlyList<ArchitectureAssignmentPrincipal> QualityAssignments { get; init; } = [];
}

public sealed record ArchitectureAssignmentPrincipal(
    string PrincipalKind,
    Guid? OrganizationUserId = null,
    Guid? AgentInstallationId = null);

public sealed record ArchitecturePublishResponse(
    Guid PlanId,
    Guid EpicId,
    IReadOnlyList<PublishedArchitectureSprint> Sprints,
    IReadOnlyList<PublishedArchitectureTicket> Tickets,
    DateTimeOffset PublishedAt)
{
    public bool DeliveryFinalized { get; init; }
    public IReadOnlyList<PublishedArchitectureEpic> Epics { get; init; } = [];
}

public sealed record PublishedArchitectureEpic(string Key, Guid ItemId, string Title);
public sealed record PublishedArchitectureSprint(int Ordinal, Guid SprintId, string Name);
public sealed record PublishedArchitectureTicket(string Key, Guid ItemId, Guid SprintId, string Kind);

public sealed record GuardedArchitecturePublishResult(
    ArchitecturePublishResponse Publication,
    IReadOnlyList<Guid> ReadyTicketIds);
