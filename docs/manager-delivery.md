# Manager delivery readiness

`ProductManagerAgent` already owns product discovery, team approval, incremental Architect
coordination, planning commitments and initial sprint readiness. Version 4.1.0 adds the missing
existing-project preparation and directed discussion paths needed for a bounded developer pilot.

| Responsibility | Implementation and evidence |
| --- | --- |
| Retain an assigned project | `ReconcileAssignedProjectAsync` verifies current project/board ownership and reads existing plans, instead of creating another team or board. Assignment alone cannot grant access. |
| Prepare a selected sprint | `SprintDeliveryPreparation.PrepareAsync` uses the Producer's explicit scope, repository choice and task-finalization pattern. It retains exact accepted planning and stage principals; the platform independently validates current participation, grants, approvals and review independence. |
| Preserve unfinished operations | Fingerprinted immutable branch bindings, stable create/configure/finalize/control keys and a private revisioned receipt recover lost responses. Changed accepted scope or staffing requires explicit reconciliation. |
| Answer directed questions | `ProductTicketDiscussion` adapts the Producer's question/revision correlation and bounded discussion reads. It retains the generated reply before posting, rechecks current evidence and handles duplicate, edited, deleted and missed events. Technical design remains with the technical specialist. |
| Preserve sprint boundaries | `ReconcileSprintReadinessAsync` completes an old readiness obligation when its own sprint is no longer Planned; it cannot start a successor under the old correlation. |

`prepare_sprint_delivery` creates an explicit evidence workflow ticket for only the selected sprint's
implementation tasks. Its mechanical completion waits for those tasks' declared execution; it adds
no implicit product acceptance gate and waives none of the existing task reviews. The delivery
branch is isolated from the repository default branch. Plan activation does not start a sprint,
promote source to the default branch, authorize deployment, install agents or expand grants.

Prerequisites are current project and board management, a Planned sprint, Ready code tasks,
accepted requirements/criteria, accountable owners and exact stage assignments. Missing planning,
ambiguous repositories, foreign plans, changed scope, revoked grants and active executions block
with actionable errors. The Product Manager must use existing typed planning/staffing operations
or request the responsible manager's decision; it cannot silently adopt or replace another plan.

This is a focused adaptation, not a copy of Gabriel's game-specific production system. Maya retains
her product and software architecture contracts. Gabriel's game staffing, pitch/GDD lifecycle,
asset strategies, scope-replacement policies and progressive game production machinery are not
implicitly enabled by this release.

## Verification

Run `dotnet test CSweet.Agent.SoftwareProductManager.slnx -c Release
-p:UseLocalCSweetAgentSdk=false -p:UseLocalCSweetWorkManagementContracts=false`.
`SprintDeliveryPreparationTests` exercises complete preparation, interrupted responses, retained
source/reviewer bindings, changed scope, denied finalization and stale readiness callbacks.
`ProductTicketDiscussionTests` covers durable generation/post boundaries, duplicate and stale
events, changed evidence, malformed hints, bounded reads and revoked mutation authority.

The configured-model matrix remains `evals/product-manager-lifecycle.v1.json`. For an explicitly
selected local OpenAI-compatible profile, first export the built agent contract with
`dotnet <published-agent.dll> --export-eval-contract > <exported-contract.json>`. Then `scripts/evaluate-lifecycle.py --url <loopback-base-url>
--model <configured-model> --contract <exported-contract.json> --out <local-result.json>` runs synthetic model behavior scenarios using
the real role prompts, without application credentials or platform effects. Inspect the responses
against every matrix criterion; the automatic checks are only a first pass. Full callback/effect
coverage is separate from model behavior. Keep deployment/business operational evidence local.

After publishing, install through the normal package review and hire flow. Live acceptance must
verify current project membership/management, an actual prepared plan, preflight, independent
source review and a directed manager question round trip before broader use. Unit and synthetic
model checks do not constitute that live acceptance.
