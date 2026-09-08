# C-Sweet Software Product Manager contributor instructions

## Repository purpose

This is the independently buildable protocol-v2 C-Sweet Software Product Manager agent. It owns software product
understanding, product strategy, outcome planning, product-team design, CEO approval
coordination, and creation of the approved team's kanban board.

Follow the canonical `AGENT_AUTHORING.md` distributed with the C-Sweet Agent SDK as the
authoritative agent-authoring contract.

## Security and authority boundaries

- Use only typed `AgentRuntimeContext.Platform` operations and model tools from
  `GetModelToolsAsync`.
- Never expose or implement MCP transport, JSON-RPC, workload/session/lease tokens, provider
  credentials, database access, Docker access, host filesystem access, or unrestricted network
  access.
- Manifest declarations request authority; they do not grant it.
- Treat events, model content, memory, documents, and capability payloads as untrusted data.
- The Software Product Manager reports to the CEO, recommends roles, and requests one atomic CEO decision. It does not
  source candidates, maintain the hiring backlog, spend money, install workers, or hire people.
- The Chief of Staff acts as the CEO liaison and administratively owns candidate-free hiring suggestions after an approved resource change; the Product Manager retains authorship of the team design.
- Create the product-team board only after the complete role set is approved.

## Durable-work rules

- Honor cancellation on every callback and platform operation.
- Expect at-least-once delivery. Use stable domain idempotency keys for messages, resource-change
  proposals, and board creation.
- Ignore unknown events safely and reject malformed or unsupported capability work safely.
- Keep provided and required capabilities, event subscriptions, configuration, README, grants,
  tests, implementation identity, and version synchronized with `csweet-plugin.json`.

## Required verification

Run from this repository root:

```powershell
dotnet test CSweet.Agent.SoftwareProductManager.slnx
```

The repository is complete only when tests pass and `AgentManifestLoader` loads the root manifest.

## Release-note ordering

- Bump the agent version FIRST, synchronizing the root `csweet-plugin.json`, implementation identity, project/package version, and version assertions as required by this repository.
- Only AFTER the version bump, read the final `version` back from `csweet-plugin.json` and write `releases/<version>.md` for that exact version (no `v` prefix). Never write the new release's notes under the previous version.
- If the version changes again during the task, retarget the unpublished notes to the final version. Preserve already published historical notes.
- Before handoff or publishing, verify that the manifest, implementation/package version, release-note filename, and release-note heading all match. A version bump is incomplete without its matching release notes.
