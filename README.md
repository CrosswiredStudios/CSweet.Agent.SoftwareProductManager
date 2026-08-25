# C-Sweet Software Product Manager

First-party Software Product Manager agent for C-Sweet, built on `CSweet.Agent.SDK` 3.18.0 and manifest protocol v2.
The agent package version is `2.11.0`.

The configured-model lifecycle matrix is defined in
[`evals/product-manager-lifecycle.v1.json`](evals/product-manager-lifecycle.v1.json) and must run
against every supported configured model profile before release.

It owns product outcomes, customer discovery, product strategy, prioritization, roadmaps, requirements, success measures, delivery alignment, and product-team design. It does not choose technical architecture, write production code, make legal conclusions, source candidates, hire workers, or spend money.

## Runtime behavior

The agent receives exact-installation durable events and capability work. Its primary startup goal is
to understand the authoritative product context and then recommend the smallest appropriate
product team. Onboarding validates its CEO-direct employee/reporting identity, opens or reuses the CEO manager
conversation, and uses its configured model to compose a business-specific opening from authoritative
operating context and relevant approved organization and relationship memory. It identifies the
deliverable it believes it owns, asks only one genuinely missing clarification when necessary, then
obtains and reviews the Chief of Staff role brief when that agent shares its CEO manager, and submits one
atomic team snapshot for an explicit CEO decision when the plan is ready. A deterministic,
contextual message is used only when model generation is unavailable.

Pre-team staffing is supervised by a durable Product Manager commitment. A manager reply wakes
that commitment and runs a bounded hiring decision with only the atomic resource-change approval
tool and compact authoritative context. Provider or transport failures wait silently and retry;
attention reviews do not require a team roster until the manager has approved a team.

Delivery plans are team-aware: agent-only delivery teams use short dependency-based execution
windows without human story points; teams containing human delivery members may use human cadence
and estimates. Governed Development and QA stages accept exact human or agent assignees.

Requested revisions are applied and resubmitted when an authoritative constraint makes the change
deterministic; otherwise the Software Product Manager asks its manager one focused refinement question.
After approval it waits for the complete role set to be filled, then creates one idempotent,
appropriately named product-team kanban board and begins Product Manager–Architect planning. The Chief
of Staff then owns creation of one candidate-free hiring suggestion per approved added or increased
role, making the same approved role set visible in the Hiring tab and in the Chief's CEO
conversation. The Chief administers these lead-authored suggestions without taking ownership of
the Product Manager's team design.

Chat chunks are durable progress. Configuration and final responses are durable results. Typed
platform calls and model tools always reflect the current grant revision.

Chief of Staff coordination uses install-time, same-organization capability bindings between active agents that share the same CEO. The Chief acts as executive liaison rather than line manager. Payload identities remain untrusted and neither agent can select a target installation. Provider credentials and runtime transport details never enter agent code.

Software Architect coordination uses the same governed provider-binding model. As soon as an
approved team has an active Product Manager and Architect, the Architect sends one idempotent
onboarding-readiness message. The Product Manager acknowledges that inbound turn and starts one
durable planning session; Developer and QA hiring are not prerequisites. Once product goals,
requirements, acceptance criteria, and constraints are ready, the Product Manager requests a typed architecture draft,
reviews it for product alignment, resolves blocking questions through the private direct agent
conversation, and explicitly invokes the separate
publication capability only after approval and repository/base-branch selection. Repository
selection gates publication and assignment, not drafting. The Architect owns technical direction;
the Product Manager retains product scope and priority.

Architecture planning is incremental. The PM persists outcome Epics first, requests bounded Story
and sprint proposals for one Epic at a time, then approves Task pages of at most eight Tasks for one
Story at a time through `software-architecture.publish-story-tasks.v1`. Structured coordination
artifacts prevent any generation request from carrying the complete project or transcript. The
legacy `software-architecture.design.v2` and `software-architecture.publish-plan.v2` capabilities
remain compatible. One PM-owned personal commitment stays Doing through strict board verification.
All published work remains Backlog and every sprint remains Planned. Only a separate PM-owned
sprint-readiness commitment may later preflight and explicitly start one eligible sprint.

The PM leads every planning continuation. Its brief is a typed directive; the Architect must either
deliver the requested design, Story, or Task artifact or return a typed clarification batch. The PM
answers product decisions within its mandate and reissues the directive linked to that question
digest. Exact-digest architecture approval embeds the next Story directive, and malformed or
text-only continuations are recovered by deterministically reconstructing the next missing stage.

The model prompt uses the SDK's authenticated interaction policies. Architect conversations use
`lead.v1`; conversations with the PM's authoritative manager use `supporting-specialist.v1`;
other reports use `lead.v1`; and unclassified authorized collaborators use `peer.v1`. These modes
govern conversational progression only and do not alter grants, approval boundaries, or specialist
decision ownership.

An explicit direct message from the active Software Architect only wakes the durable planning
commitment. The commitment reconciles the board, transcript, roster, bindings, and authoritative
product decisions and advances the next missing planning stage until the complete backlog verifies
or a real governance decision is missing.

The manifest requests a five-minute **Think every** cadence. Platform-issued attention reviews
reconcile durable PM–Architect commitments without calling the model when no work is actionable.
Waiting commitments stay silent for 30 minutes and escalate through the reporting chain after two
hours; runtime failures resume through public coordination recovery capabilities.
Architect readiness immediately requeues an existing waiting planning commitment, while attention
reviews provide the same recovery path after missed or replayed communication events.

When an executive finalizes a product-team recommendation outside the Software Product Manager's reporting
conversation, the runtime resolves the CEO manager and routes the atomic resource-change request
into the protected CEO chat. The human CEO retains the requirement that approval originate from
that direct conversation; the Chief may reconcile the approved plan afterward.

## Build and test

```powershell
dotnet build CSweet.Agent.SoftwareProductManager.slnx
dotnet test CSweet.Agent.SoftwareProductManager.slnx
```

Requirements are .NET 10, `CSweet.Agent.SDK` 3.18.0, `CSweet.Memory`, an approved protocol-v2 installation, an active managing employee, and the grants in [GRANTS.md](GRANTS.md).

## SDK 1.1.1 authoring contract

The protocol-v1 transport APIs were removed. The implementation now uses `AgentEventEnvelope`, `AgentCapabilityRequest`, `AgentWorkResult`, typed `AgentRuntimeContext.Platform` calls, `ReportProgressAsync`, live model tools, and `PlatformChatClient`. The v2 manifest adds schemas, timeouts, and idempotency and removes generic publications.

## Provided capability behavior

Assistant, planning, and check-in callbacks may emit progress and always produce a durable result.
`product-management.plan.v1` is advisory and idempotent per work item.
`product-management.context.update.v1` records authoritative context and, when ready, routes the
refreshed plan to the protected CEO conversation. The Product Manager waits for the CEO's direct
instruction before submitting the complete team through the separately granted resource-change capability. Configuration
update changes runtime configuration. External communication, approvals, board creation, and other
effects occur only through separately granted platform capabilities. See [GRANTS.md](GRANTS.md).
