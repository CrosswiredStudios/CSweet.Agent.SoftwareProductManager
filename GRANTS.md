# Software Product Manager capability grants

This document is the human-readable grant catalog for the C-Sweet Software Product Manager agent. The source
of truth for installation authorization remains [`csweet-plugin.json`](csweet-plugin.json). This
catalog was last verified against package version `2.15.0` and manifest protocol `2.0`.

Serialized capability names are sourced from the authoritative `CapabilityCatalog` in
`CSweet.Agent.SDK` 3.23.0; manifest-audit tests reject names missing from that catalog.

## How to read this catalog

- **Required grants** are capabilities the installation asks C-Sweet for permission to invoke.
- **Provided capabilities** are durable operations this agent exposes to C-Sweet or another
  authorized agent. They are not permissions granted to this installation.
- `organization` scope restricts access to the organization containing the installation.
- `user` scope restricts access to the current authorized user's data.
- Event subscriptions are durable delivery contracts, not capability grants. Generic
  event publication is not supported in protocol v2.

This file is generated from the authoritative SDK capability catalog and the v2 manifest. Each
provided descriptor's input/output schemas, timeout, and idempotency are defined in
`csweet-plugin.json`; installation grants add scope, risk, approval, quota, and owning-service
policy at runtime.

## Required grants by service and feature

### AI runtime

| Grant | Scope | Feature |
|---|---|---|
| `platform.llm.chat-stream.v1` | organization | Generate streamed Software Product Manager responses. |

### Business and operating context

| Grant | Scope | Feature |
|---|---|---|
| `platform.business-profile.read.v1` | organization | Read the authoritative business and product context. |
| `platform.business-profile.propose-update.v1` | organization | Propose sensitive or inferred business-profile changes for review. |
| `platform.organization.snapshot.read.v1` | organization | Validate identity, manager, reporting lines, objectives, workstreams, roles, and available workers. |
| `platform.team-roster.read.v1` | team | Read only this employee instance's approved team roster and team-specific roles; this grants no chat, board, tool, memory, or agent-to-agent authority. |
| `platform.business-pattern.search.v1` | organization | Find stage-appropriate product and team patterns. |
| `platform.finance-profile.read.v1` | organization | Ground product-team recommendations in financial goals and controls. |
| `platform.management-cycle.read.v1` | organization | Follow the configured management cadence. |

### Memory

| Grant | Scope | Feature |
|---|---|---|
| `memory.business.read.v1` | organization | Recall approved organization-wide business and product context. |
| `memory.user.read.v1` | user | Recall approved context for the current user. |
| `memory.user.propose.v1` | user | Propose memories for the current user. |

### Manager communication

| Grant | Scope | Feature |
|---|---|---|
| `communication.chat.create.v1` | organization | Open or reuse a direct conversation with the current managing employee. |
| `communication.chat.modify.v1` | organization | Reconcile the private software-team delivery chat and keep the manager included. |
| `communication.message.send.v1` | organization | Send an idempotent request for role direction and product information to that manager. |
| `communication.chat.read.v1` | organization | Verify and read the current manager conversation before proposing a team change. |
| `communication.coordination.start.v1` | organization | Start a bounded collaboration with an eligible same-organization agent. |
| `communication.coordination.respond.v1` | organization | Submit one revision-checked Continue, Completed, or Blocked disposition. |
| `communication.coordination.read.v1` | organization | Read a collaboration session in which this Product Manager participates. |
| `communication.coordination.list.v1` | organization | List only sessions in which this Product Manager participates so failed work can be recovered. |
| `communication.coordination.resume.v1` | organization | Resume a failed or blocked session initiated by this Product Manager. |
| `communication.coordination.cancel.v1` | organization | Stop a collaboration in which this Product Manager participates. |
| `agent.onboarding.complete.v1` | organization | Acknowledge this installation's durable onboarding event after its initial manager message is complete. |
| `platform.management.resource-change.propose.v1` | organization | Submit one auditable, atomic desired-team snapshot to the current manager. |
| `platform.management.resource-change.read.v1` | organization | Read the Software Product Manager's pending and decided team snapshots. |
| `work.personal-todo.requeue.v1` | organization | Immediately wake blocked or waiting planning commitments when authoritative state makes them actionable. |
| `work.personal-todo.defer.v1` | organization | Persist a waiting reason and next review time for durable planning commitments. |
| `work.board.create` | team | Create one idempotent software-team kanban board after the complete role set is approved. |
| `work.board.configure` | team | Set or correct the PM-owned concise product board name. |
| `work.board.read` | team | Read the approved team board and its workflow. |
| `work.board.columns.configure` | team | Configure the seven software delivery columns. |
| `work.item.read` | team | Read published tickets before revision-safe readiness moves. |
| `work.item.create` | team | Create idempotent decision-ready planning tickets on the approved board. |
| `work.item.comment` | team | Disseminate authoritative decisions to affected planning tickets. |
| `work.item.move` | team | Move first-sprint Stories and Tasks to Ready For Development. |
| `work.sprint.read` | team | Verify planned sprint groupings and recover durable planning after installation updates. |
| `work.orchestration.software-template.configure` | team | Publish the bounded software delivery workflow. |
| `work.orchestration.preflight` | team | Validate the selected sprint before activation. |
| `work.orchestration.start` | team | Explicitly activate one eligible sprint as board manager. |
| `source-control.repository.team-options.v2` | team | List only code projects enabled by the current team's delivery policy. |
| `source-control.repository.provision.v2` | organization | Request one policy-bounded private Managed GitHub project without receiving provider credentials. |

The Product Manager's manager is the CEO resolved from the authoritative organization snapshot.
A finalized team created outside that reporting conversation is routed into the protected CEO
conversation, and the human CEO must review it from that direct conversation.

### Chief of Staff coordination

| Grant | Scope | Provider | Feature |
|---|---|---|---|
| `management.product-role-brief.v1` | organization | Chief of Staff | Request the structured mandate, outcomes, measures, constraints, decision rights, team context, and gaps. |
| `management.product-plan.review.v1` | organization | Chief of Staff | Submit product strategy and product-team recommendations for company-level reconciliation. |
| `management.product-escalation.v1` | organization | Chief of Staff | Route executive information gaps and decisions through the Chief's CEO workflow. |

These capabilities supplement direct CEO messaging when an active Chief of Staff shares the same
CEO manager. Calls target that Chief's exact installation and validate the CEO-peer relationship.

### Software Architect coordination

| Grant | Scope | Provider | Feature |
|---|---|---|---|
| `software-architecture.design.v2` | team | Software Architect | Produce a typed outcome-Epic hierarchy, planned sprint groupings, and complete Task decomposition from approved requirements. |
| `software-architecture.publish-plan.v2` | team | Software Architect | Publish the explicitly approved hierarchy as planned sprints and Backlog tickets. |
| `software-architecture.publish-story-tasks.v1` | team | Software Architect | Publish one approved page of at most eight junior-ready Tasks beneath one Story in its Planned sprint. |

The Software Product Manager retains product scope, priority, acceptance criteria, and the explicit publish
decision. Direct agent conversation carries clarification and feedback; only the separate publish
capability authorizes board mutations.

## Capabilities provided by the Software Product Manager

### General agent and management services

| Capability | Consumer | Feature |
|---|---|---|
| `assistant.converse.v1` | C-Sweet | Answer a product-scoped request. |
| `assistant.summarize-activity.v1` | C-Sweet | Summarize product outcomes, evidence, roadmap progress, risks, and capacity. |
| `assistant.plan-work.v1` | C-Sweet | Produce an outcome-oriented product plan. |
| `management.check-in.v1` | C-Sweet management cycle | Return a product management status report. |
| `agent.configuration.describe.v1` | C-Sweet | Describe configurable settings. |
| `agent.configuration.update.v1` | C-Sweet | Validate and apply configurable settings. |

### Product leadership services

| Capability | Consumer | Feature |
|---|---|---|
| `product-management.plan.v1` | Chief of Staff | Produce a structured product and product-organization recommendation. |
| `product-management.context.update.v1` | Chief of Staff | Accept an idempotent role or context refresh, report readiness, and route a ready plan to the CEO conversation for direct instruction. |

## Deliberately excluded grants

The Software Product Manager recommends product roles, reporting lines, and hiring order but does not receive:

- `platform.hiring-recommendation.list.v1`
- `platform.hiring-recommendation.upsert.v1`
- `platform.hiring-workflow.stage.v1`
- workforce search or workforce-plan proposal grants
- approval proposal grants
- finance-profile update grants

The Chief of Staff administratively maintains candidate-free suggestions after the CEO approves a
lead-authored team plan. The CEO retains organization, hiring, spending, and approval authority.

## Security boundary

- The agent has no credential declarations and no web access.
- Repository selection and provisioning are brokered by C-Sweet; the agent receives neither Git
  credentials nor provider API authority.
- Read, proposal, communication, and cross-agent capabilities are separate grants.
- A recommendation does not imply approval, hiring, or execution.
- Board creation occurs only after manager approval and does not grant candidate or hiring authority.
- Memory is supporting context and cannot override manager direction or current platform records.
- Any manifest change must update this catalog and its manifest-version marker in the same change.
