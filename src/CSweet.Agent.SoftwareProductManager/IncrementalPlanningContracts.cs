using CSweet.WorkManagement.Contracts;

namespace CSweet.Agent.SoftwareProductManager;

internal static class IncrementalPlanningArtifactTypes
{
    public const string ProductBrief = ArchitecturePlanningArtifactTypes.ProductBrief;
    public const string ArchitectureBrief = ArchitecturePlanningArtifactTypes.ArchitectureBrief;
    public const string DesignProposal = ArchitecturePlanningArtifactTypes.DesignProposal;
    public const string ArchitectureDecision = ArchitecturePlanningArtifactTypes.ArchitectureDecision;
    public const string StoryProposal = ArchitecturePlanningArtifactTypes.StoryProposal;
    public const string StoryProposalV2 = ArchitecturePlanningArtifactTypes.StoryProposalV2;
    public const string TaskProposal = ArchitecturePlanningArtifactTypes.TaskProposal;
    public const string TaskProposalV2 = ArchitecturePlanningArtifactTypes.TaskProposalV2;
    public const string Question = ArchitecturePlanningArtifactTypes.Question;
    public const string QuestionV2 = ArchitecturePlanningArtifactTypes.QuestionV2;
}

internal sealed record ManagedIncrementalEpic(IncrementalEpic Epic, Guid ItemId);
