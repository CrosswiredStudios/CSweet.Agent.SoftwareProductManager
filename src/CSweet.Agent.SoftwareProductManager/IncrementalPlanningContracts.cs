namespace CSweet.Agent.SoftwareProductManager;

internal static class IncrementalPlanningArtifactTypes
{
    public const string ProductBrief = "product-management.brief.v1";
    public const string StoryProposal = "software-architecture.story-proposal.v1";
    public const string TaskProposal = "software-architecture.task-proposal.v1";
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
    int PageOrdinal = 0);

public sealed record IncrementalEpic(string Key, string Title, string Outcome, IReadOnlyList<string> AcceptanceCriteria);

public sealed record IncrementalStoryProposal(
    string PlanKey, string EpicKey, IReadOnlyList<IncrementalStory> Stories, IReadOnlyList<string> Risks);

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
    string PlanKey, string StoryKey, int PageOrdinal, bool IsFinalPage, IReadOnlyList<JuniorReadyTask> Tasks);

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
    string DefinitionOfDone);

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
