using CSweet.Agent.SDK;
using CSweet.WorkManagement.Contracts;

namespace CSweet.Agent.SoftwareProductManager;

public sealed partial class ProductManagerAgent
{
    protected override Task<AgentWorkResult> ExecuteDeliveryScopeAsync(WorkExecutionAssignmentV2 assignment,
        AgentRuntimeContext context, CancellationToken ct) => DeliveryScopeReview.ExecuteAsync(assignment, context,
        context.CreateChatClient(new AgentLlmSelection(Settings.GetGuid("llmProviderId") ??
            throw new InvalidOperationException("Configure a delivery acceptance provider."), Settings.GetString("llmModel"))), ct);
}
