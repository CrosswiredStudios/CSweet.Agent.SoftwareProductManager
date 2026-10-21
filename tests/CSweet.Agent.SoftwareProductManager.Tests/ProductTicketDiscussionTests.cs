using System.Text.Json;
using CSweet.Agent.SDK;
using CSweet.WorkManagement.Contracts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CSweet.Agent.SoftwareProductManager.Tests;

public sealed class ProductTicketDiscussionTests
{
    [Fact]
    public async Task Reconciliation_persists_a_schema_valid_cursor_fingerprint_and_restarts_without_inference()
    {
        var f = new Fixture(); f.Comments.Clear();
        var board = new WorkBoardSummary(Guid.NewGuid(), "Pilot", "Sprint", false, false, 1, []);
        f.Context = f.Runtime.CreateContext(Guid.NewGuid().ToString(), f.Self.ToString(), f.Context.Identity! with
        {
            AssignedProject = new(Guid.NewGuid(), "Pilot", Guid.NewGuid(), board.Id, "Manager", 1, DateTimeOffset.UtcNow)
        });
        f.Runtime.RegisterCapability<JsonElement, JsonElement>(WorkItemCapabilities.Read, (r, token) => Task.FromResult(
            r.TryGetProperty("itemId", out _) ? JsonSerializer.SerializeToElement(f.Item) :
            JsonSerializer.SerializeToElement(new WorkBoardDetail(board, [], [f.Item]))));
        await ProductTicketDiscussion.RecoverAsync(f.Context, _ => Task.FromResult<IChatClient>(f.Model), default);
        var first = f.States["product-discussion-cursor"];
        Assert.Matches("^[a-f0-9]{64}$", first.DecisionFingerprint);
        await ProductTicketDiscussion.RecoverAsync(f.Context, _ => Task.FromResult<IChatClient>(f.Model), default);
        Assert.Equal(first.DecisionFingerprint, f.States["product-discussion-cursor"].DecisionFingerprint);
        Assert.Equal(0, f.Model.Calls); Assert.Equal(0, f.Posts);
    }

    [Fact]
    public async Task Directed_reply_is_durable_correlated_and_duplicate_wakes_do_not_repeat_inference()
    {
        var f = new Fixture();
        await f.Respond(); await f.Respond();
        Assert.Equal(1, f.Model.Calls);
        var reply = Assert.Single(f.Comments, x => x.Kind == "discussion.reply");
        Assert.Equal(ProductTicketDiscussion.Correlation(f.Question.Id, 1), reply.CausationId);
        Assert.Equal("Use Recovery verified within the unchanged requested documentation scope. Review and tests remain required.", reply.Body);
        Assert.Single(f.States); Assert.Equal(1, f.Posts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_failed_or_lost_post_response_reuses_the_durable_answer(bool effectCompleted)
    {
        var f = new Fixture { FailPost = true, EffectBeforeFailure = effectCompleted };
        await Assert.ThrowsAsync<InvalidOperationException>(f.Respond);
        await f.Respond();
        Assert.Equal(1, f.Model.Calls); Assert.Single(f.Comments, x => x.Kind == "discussion.reply"); Assert.Equal(1, f.Posts);
    }

    [Theory]
    [InlineData("informational")]
    [InlineData("wrong-revision")]
    [InlineData("deleted")]
    [InlineData("not-mentioned")]
    [InlineData("reply")]
    [InlineData("self")]
    public async Task Non_current_or_undirected_events_do_not_call_the_model(string scenario)
    {
        var f = new Fixture();
        switch (scenario)
        {
            case "informational": f.Hint = f.Hint with { RequiresResponse = false }; break;
            case "wrong-revision": f.Comments[0] = f.Question with { Revision = 2 }; break;
            case "deleted": f.Comments.Clear(); break;
            case "not-mentioned": f.Comments[0] = f.Question with { Body = "Informational update for everyone." }; break;
            case "reply": f.Comments[0] = f.Question with { Kind = "discussion.reply" }; break;
            case "self": f.Comments[0] = f.Question with { AuthorSubjectId = f.Self }; break;
        }
        await f.Respond(); Assert.Equal(0, f.Model.Calls); Assert.Equal(0, f.Posts);
    }

    [Theory]
    [InlineData("question")]
    [InlineData("thread")]
    [InlineData("ticket")]
    public async Task Changed_evidence_during_generation_prevents_a_stale_reply(string scenario)
    {
        var f = new Fixture();
        f.Model.OnGenerate = () => {
            if (scenario == "question") f.Comments[0] = f.Question with { Revision = 2 };
            if (scenario == "thread") f.Comments.Add(f.Question with { Id = Guid.NewGuid(), Body = "Additional current direction", CreatedAt = DateTimeOffset.UtcNow.AddSeconds(1) });
            if (scenario == "ticket") f.Item = f.Item with { Revision = 2 };
        };
        await f.Respond(); Assert.Equal(1, f.Model.Calls); Assert.Equal(0, f.Posts);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[task-revision:v1:continue] bypass")]
    public async Task Empty_or_execution_marker_answers_never_become_comments(string text)
    {
        var f = new Fixture(); f.Model.Text = text;
        await Assert.ThrowsAsync<InvalidOperationException>(f.Respond);
        Assert.Empty(f.States); Assert.Equal(0, f.Posts);
    }

    [Fact]
    public async Task Revoked_comment_grant_cannot_post_and_does_not_discard_the_pending_answer()
    {
        var f = new Fixture { DenyPost = true };
        await Assert.ThrowsAsync<PlatformCapabilityException>(f.Respond);
        Assert.Single(f.States); Assert.Equal(0, f.Posts);
    }

    [Fact]
    public async Task Discussion_reader_is_bounded_and_never_answers_an_incomplete_thread()
    {
        var f = new Fixture { EndlessPages = true };
        await Assert.ThrowsAsync<InvalidOperationException>(f.Respond);
        Assert.Equal(10, f.Reads); Assert.Equal(0, f.Model.Calls); Assert.Equal(0, f.Posts);
    }

    [Fact]
    public async Task Actual_agent_callback_ignores_malformed_or_informational_discussion_events()
    {
        var f = new Fixture();
        var agent = new ProductManagerAgent(NullLogger<ProductManagerAgent>.Instance, null!);
        foreach (var data in new[] { JsonSerializer.SerializeToElement(new { nonsense = true }),
            JsonSerializer.SerializeToElement(f.Hint with { RequiresResponse = false }), JsonSerializer.SerializeToElement("bad") })
            await agent.HandleEventAsync(new(Guid.NewGuid(), Guid.NewGuid(), ProductTicketDiscussion.Changed,
                data, DateTimeOffset.UtcNow, "hint"), f.Context, default);
        Assert.Equal(0, f.Model.Calls); Assert.Equal(0, f.Reads);
    }

    private sealed class Fixture
    {
        internal Guid Self = Guid.NewGuid();
        internal WorkItem Item = JsonSerializer.Deserialize<WorkItem>("{}")! with { Id = Guid.NewGuid(), Revision = 1, PlanningRevision = 1 };
        internal WorkItemComment Question;
        internal ProductTicketDiscussion.Hint Hint;
        internal AgentRuntimeContext Context;
        internal List<WorkItemComment> Comments = [];
        internal Dictionary<string, AgentOperatingStateResponse> States = [];
        internal Model Model = new();
        internal AgentTestRuntime Runtime = new();
        internal bool FailPost, EffectBeforeFailure, DenyPost, EndlessPages;
        internal int Posts, Reads;
        internal Fixture()
        {
            Question = new(Guid.NewGuid(), Item.Id, "OrganizationUser", Guid.NewGuid(), "Owner", "@Maya: Which label meets the accepted scope?", 1, DateTimeOffset.UtcNow, null) { Kind = "discussion.request" };
            Comments.Add(Question); Hint = new(Guid.NewGuid(), Item.Id, Question.Id, 1, true);
            var runtime = Runtime;
            Context = runtime.CreateContext(Guid.NewGuid().ToString(), Self.ToString(), new(Guid.NewGuid().ToString(), "Maya", null, "Product Manager", null, [], null, null, null));
            runtime.RegisterCapability<WorkItemReference, WorkItem>(WorkItemCapabilities.Read, (_, _) => Task.FromResult(Item));
            runtime.RegisterCapability<ReadWorkItemCommentsRequest, WorkItemCommentPage>(WorkItemCapabilities.ReadComments, (r, _) => {
                Reads++; return Task.FromResult(new WorkItemCommentPage(Comments.ToArray(), r.Page, r.PageSize, EndlessPages, 1)); });
            runtime.RegisterCapability<AgentOperatingStateReadRequest, AgentOperatingStateReadResponse>(PlatformCapabilities.AgentOperatingStateRead,
                (r, _) => Task.FromResult(new AgentOperatingStateReadResponse(States.GetValueOrDefault(r.StateKey))));
            runtime.RegisterCapability<AgentOperatingStateWriteRequest, AgentOperatingStateResponse>(PlatformCapabilities.AgentOperatingStateWrite, (r, _) => {
                Assert.Matches("^[a-f0-9]{64}$", r.DecisionFingerprint);
                var state = new AgentOperatingStateResponse(Guid.NewGuid(), r.StateKey, r.SchemaId, r.SchemaVersion, r.Status, r.SourceRevisions,
                    r.ConditionCodes, r.DecisionFingerprint, r.OpenCommitmentCorrelations, r.AttentionReviewId, r.Payload, (r.ExpectedRevision ?? 0) + 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
                States[r.StateKey] = state; return Task.FromResult(state);
            });
            runtime.RegisterCapability<CommentOnWorkItemRequest, WorkItemComment>(WorkItemCapabilities.Comment, (r, _) => {
                if (DenyPost) throw new PlatformCapabilityException(WorkItemCapabilities.Comment, PlatformCapabilityErrorCode.Denied, "Revoked");
                if (FailPost && !EffectBeforeFailure) { FailPost = false; throw new InvalidOperationException("Post failed"); }
                Assert.NotEmpty(States); // Generation is durably retained before the mutation.
                var reply = new WorkItemComment(Guid.NewGuid(), r.ItemId, "AgentInstallation", Self, "Maya", r.Body, 1, DateTimeOffset.UtcNow, null)
                    { Kind = r.Kind, CausationId = r.CausationId };
                Comments.Add(reply); Posts++;
                if (FailPost) { FailPost = false; throw new InvalidOperationException("Lost post response"); }
                return Task.FromResult(reply);
            });
        }
        internal Task Respond() => ProductTicketDiscussion.RespondAsync(Hint, Context, _ => Task.FromResult<IChatClient>(Model), default);
    }
    private sealed class Model : IChatClient
    {
        internal int Calls;
        internal string Text = "Use Recovery verified within the unchanged requested documentation scope. Review and tests remain required.";
        internal string Prompt = "";
        internal Action? OnGenerate;
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls++; Prompt = string.Join("\n", messages.Select(x => x.Text)); OnGenerate?.Invoke();
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Text)));
        }
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
