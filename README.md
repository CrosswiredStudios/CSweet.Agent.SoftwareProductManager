# C-Sweet Software Product Manager

First-party Software Product Manager agent for C-Sweet, built on `CSweet.Agent.SDK` 3.10.0 and manifest protocol v2.
The agent package version is `2.5.2`.

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

Software Architect coordination uses the same governed provider-binding model. As soon as every
approved role is filled and the team board is ready, the Product Manager sends the designated
Architect an idempotent delivery-planning kickoff. Once product goals, requirements, acceptance
criteria, and constraints are ready, the Product Manager requests a typed architecture draft,
reviews it for product alignment, resolves blocking questions through the private direct agent
conversation, and explicitly invokes the separate
publication capability only after approval and repository/base-branch selection. Repository
selection gates publication and assignment, not drafting. The Architect owns technical direction;
the Product Manager retains product scope and priority.

An explicit direct message from the active Software Architect is an actionable planning trigger.
The Product Manager reconciles the board, reads its actual manager conversation for authoritative
repository and product decisions, and advances architecture publication and sprint readiness until
a real governance decision is missing.

When an executive finalizes a product-team recommendation outside the Software Product Manager's reporting
conversation, the runtime resolves the CEO manager and routes the atomic resource-change request
into the protected CEO chat. The human CEO retains the requirement that approval originate from
that direct conversation; the Chief may reconcile the approved plan afterward.

## Build and test

```powershell
dotnet build CSweet.Agent.SoftwareProductManager.slnx
dotnet test CSweet.Agent.SoftwareProductManager.slnx
```

Requirements are .NET 10, `CSweet.Agent.SDK` 3.10.0, `CSweet.Memory`, an approved protocol-v2 installation, an active managing employee, and the grants in [GRANTS.md](GRANTS.md).

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
