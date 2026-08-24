using System.Text.Json;
using CSweet.WorkManagement.Contracts;

namespace CSweet.Agent.SoftwareProductManager;

internal static class IncrementalPlanningArtifactTypes
{
    public const string ProductBrief = "product-management.brief.v1";
    public const string ArchitectureBrief = "product-management.architecture-brief.v2";
    public const string DesignProposal = "software-architecture.design-proposal.v1";
    public const string ArchitectureDecision = "product-management.architecture-decision.v1";
    public const string StoryProposal = "software-architecture.story-proposal.v1";
    public const string StoryProposalV2 = "software-architecture.story-proposal.v2";
    public const string TaskProposal = "software-architecture.task-proposal.v1";
    public const string TaskProposalV2 = "software-architecture.task-proposal.v2";
    public const string Question = "software-architecture.question.v1";
}

public sealed record IncrementalProductBrief(
    Guid BoardId,
    string PlanKey,
    string ProductGoal,
    IReadOnlyList<string> Requirements,
    IReadOnlyList<string> AcceptanceCriteria,
    IncrementalEpic Epic,
    string Stage,
    IncrementalStory? Story = null,
    int PageOrdinal = 0)
{
    public IReadOnlyList<string> Constraints { get; init; } = [];
    public IReadOnlyList<string> NonGoals { get; init; } = [];
    public IReadOnlyDictionary<string, string> SourceRevisions { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
    public string? ApprovedDesignDigest { get; init; }
    public int DesignRevision { get; init; }
}

public sealed record SoftwareArchitectureDesignProposal(
    string PlanKey,
    Guid BoardId,
    int Revision,
    JsonElement Design,
    IReadOnlyList<string> ImpactSummary,
    IReadOnlyDictionary<string, string> SourceRevisions);

public sealed record ProductArchitectureDecision(
    string PlanKey,
    string DesignDigest,
    string Decision,
    string Rationale,
    int Revision);

public sealed record IncrementalEpic(string Key, string Title, string Outcome, IReadOnlyList<string> AcceptanceCriteria);

public sealed record IncrementalStoryProposal(
    string PlanKey, string EpicKey, IReadOnlyList<IncrementalStory> Stories, IReadOnlyList<string> Risks)
{
    public string? ApprovedDesignDigest { get; init; }
    public IReadOnlyDictionary<string, string> SourceRevisions { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed record IncrementalStory(
    string Key,
    string Title,
    string Outcome,
    IReadOnlyList<string> Requirements,
    IReadOnlyList<string> AcceptanceCriteria,
    IReadOnlyList<string> Dependencies,
    string SprintKey,
    int SprintOrdinal,
    string SprintGoal);

public sealed record IncrementalTaskProposal(
    string PlanKey, string StoryKey, int PageOrdinal, bool IsFinalPage, IReadOnlyList<JuniorReadyTask> Tasks)
{
    public string? ApprovedDesignDigest { get; init; }
    public IReadOnlyDictionary<string, string> SourceRevisions { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed record JuniorReadyTask(
    string Key,
    string Title,
    string Purpose,
    IReadOnlyList<string> Requirements,
    string AffectedBoundary,
    IReadOnlyList<string> TechnicalConstraints,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> EdgeCases,
    IReadOnlyList<string> TestExpectations,
    IReadOnlyList<string> VerificationEvidence,
    string DefinitionOfDone)
{
    public IReadOnlyList<WorkTechnicalDelegationRecommendation> DelegationRecommendations { get; init; } = [];
}

public sealed record IncrementalArchitectureQuestion(string PlanKey, string ScopeKey, string Question);

public sealed record PublishStoryTasksRequest(
    Guid BoardId,
    Guid StoryId,
    Guid SprintId,
    IncrementalTaskProposal Proposal,
    string ApprovalRationale,
    string IdempotencyKey);

public sealed record PublishStoryTasksResponse(
    Guid BoardId,
    Guid StoryId,
    Guid SprintId,
    string StoryKey,
    int PageOrdinal,
    bool IsFinalPage,
    IReadOnlyList<PublishedStoryTask> Tasks,
    DateTimeOffset PublishedAt);

public sealed record PublishedStoryTask(string Key, Guid ItemId, string Title);

internal sealed record ManagedIncrementalEpic(IncrementalEpic Epic, Guid ItemId);
