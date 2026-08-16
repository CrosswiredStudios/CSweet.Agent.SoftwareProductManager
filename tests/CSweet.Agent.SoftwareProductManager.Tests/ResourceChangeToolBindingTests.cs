using System.Text.Json;
using CSweet.Agent.SDK;
using Microsoft.Extensions.AI;

namespace CSweet.Agent.SoftwareProductManager.Tests;

public sealed class ResourceChangeToolBindingTests
{
    [Fact]
    public async Task Optional_proposal_fields_can_be_omitted_by_the_model()
    {
        var invoked = false;
        var tool = ProductManagerAgent.CreateResourceChangeApprovalTool(
            (productGoal, rationale, contextRevision, roles, assumptions, constraints,
                supersedesRequestId, token) =>
            {
                invoked = true;
                Assert.Equal("Build a game", productGoal);
                Assert.Equal("Smallest viable team", rationale);
                Assert.Equal(1, contextRevision);
                Assert.Single(roles);
                Assert.Null(assumptions);
                Assert.Null(constraints);
                Assert.Null(supersedesRequestId);
                Assert.Equal(CancellationToken.None, token);
                return Task.FromResult(ResourceChangeApprovalToolResult.Failure("test complete"));
            });

        using var rolesJson = JsonDocument.Parse("""
            [{
              "roleKey":"architect",
              "team":"Product",
              "title":"Software Architect",
              "purpose":"Own architecture",
              "headcount":1,
              "priority":1,
              "timing":"Now",
              "requiredCapabilities":["software-architecture"],
              "humanRequired":false,
              "reportsToRoleKey":null
            }]
            """);
        var arguments = new AIFunctionArguments
        {
            ["productGoal"] = "Build a game",
            ["rationale"] = "Smallest viable team",
            ["contextRevision"] = 1L,
            ["roles"] = rolesJson.RootElement.Clone()
        };

        _ = await tool.InvokeAsync(arguments, CancellationToken.None);

        Assert.True(invoked);
        var required = tool.JsonSchema.GetProperty("required")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToList();
        Assert.DoesNotContain("assumptions", required);
        Assert.DoesNotContain("constraints", required);
        Assert.DoesNotContain("supersedesRequestId", required);
    }
}
