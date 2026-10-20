"""Synthetic configured-model checks. Never invokes platform effects or reads application credentials."""
import argparse
import json
import pathlib
import re
import time
import urllib.parse
import urllib.request


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--url", required=True, help="Explicit local OpenAI-compatible model base URL")
    parser.add_argument("--model", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--contract", required=True, help="JSON exported by the built agent --export-eval-contract")
    args = parser.parse_args()
    parsed = urllib.parse.urlparse(args.url)
    if parsed.hostname not in ("localhost", "127.0.0.1", "::1") or parsed.scheme != "http" or parsed.username or parsed.password:
        raise SystemExit("This credential-free evaluator accepts an explicitly selected loopback HTTP model only.")
    contract = json.loads(pathlib.Path(args.contract).read_text(encoding="utf-8-sig"))
    system = contract["systemPrompt"]
    hiring = contract["boundedHiringPrompt"]
    # These fixtures contain no real business data. Full callback/effect checks are the independent .NET suite.
    fixtures = [
        ("initial-discovery", "Synthetic business: Atlas Notes builds a notes app. Customer and measurable outcome are unknown. Ask the one most useful missing product question; do not propose staffing yet.", False),
        ("bounded-hiring-design", "Synthetic CEO direction: propose the smallest software team for an offline notes app for students. Success: 10 students complete save-and-find in under a minute. No cloud, no paid services, no dates or budget cap. Current team has only the PM. contextRevision=1. Include Software Architect, Software Developer and independent Software QA, one each; direct reports omit reportsToRoleKey. Submit the atomic request now; no narrative claim of approval.", True),
        ("roadmap-refresh", "Synthetic existing approved project: Atlas Notes. The approved Architect, Developer and independent QA remain staffed. No staffing change is requested. Current outcome: 10 students save and find a note under a minute, offline. Refresh the now/next/later outcome roadmap after evidence that search is confusing; name success measures and distinguish the hypothesis. Do not claim you changed any platform record.", False),
        ("architect-interaction", "Synthetic assigned architect asks: should account synchronization be included in this increment? The accepted outcome is offline notes, no cloud or accounts. Give a product-scope decision and the next bounded directive. Do not design architecture, write code or waive quality checks. No platform change has been performed.", False),
        ("replacement-plan-explanation", "Synthetic CEO asks you to explain a PROPOSED team revision: approved baseline has architect A, developer D and independent QA Q; Q left. Proposed replacement is one independent QA, retaining A and D. Explain the operational impact and what remains blocked. This is an explanation only, not an instruction to submit or approve the revision. No hiring or budget has been approved.", False),
        ("escalation-quality", "Synthetic Developer D is blocked because the approved test environment lacks a required credential. No team member can supply it. The CEO owns the credential decision. Ask the accountable decision owner one focused, actionable question and state safe work that can continue. Do not request the secret in chat, claim access exists, approve work, or invent a deadline.", False),
    ]
    tool = contract["tool"]
    results = []
    for key, prompt, bounded in fixtures:
        payload = {"model": args.model, "messages": [{"role": "system", "content": hiring if bounded else system}, {"role": "user", "content": prompt}],
                   "max_tokens": 8192, "temperature": 0, "stream": False}
        if bounded:
            payload.update(tools=[tool], tool_choice={"type": "function", "function": {"name": "request_resource_change_approval"}})
        request = urllib.request.Request(args.url.rstrip("/") + "/chat/completions", data=json.dumps(payload).encode(), headers={"Content-Type": "application/json"})
        start = time.monotonic()
        with urllib.request.urlopen(request, timeout=240) as response:
            result = json.load(response)
        message = result["choices"][0]["message"]
        calls = message.get("tool_calls") or []
        content = message.get("content") or ""
        checks = {"nonempty": bool(content.strip() or calls)}
        if bounded:
            checks["one_atomic_request"] = len(calls) == 1 and calls[0]["function"]["name"] == "request_resource_change_approval"
            proposal = json.loads(calls[0]["function"]["arguments"]) if calls else {}
            roles = proposal.get("roles", [])
            serialized = json.dumps(roles).lower()
            checks["independent_role_set"] = all(term in serialized for term in ("architect", "developer", "qa"))
            checks["single_headcount"] = all(role.get("headcount") == 1 for role in roles) and len(roles) == 3
            checks["no_invented_human_requirement"] = all(role.get("humanRequired") is False for role in roles)
        else:
            checks["no_effect_claim_via_tool"] = not calls
        if key == "initial-discovery":
            checks["one_focused_question"] = content.count("?") == 1
            checks["not_bundled"] = "and what" not in content.lower() and "include that too" not in content.lower()
        if key == "replacement-plan-explanation":
            checks["no_unverified_workflow_claim"] = all(term not in content.lower() for term in ("in testing", "ready to merge", "platform rules require", "preflight will fail"))
        if key in ("initial-discovery", "replacement-plan-explanation", "escalation-quality"):
            checks["bounded_informational_reply"] = len(content.split()) <= 180
        results.append({"scenario": key, "checks": checks, "seconds": round(time.monotonic() - start, 2), "usage": result.get("usage"), "message": message})
        print(key + ": " + ("PASS" if all(checks.values()) else "FAIL"), flush=True)
    output = pathlib.Path(args.out)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps({"model": args.model, "scope": "synthetic model behavior; platform effects verified separately", "results": results}, indent=2), encoding="utf-8")
    if not all(all(row["checks"].values()) for row in results):
        raise SystemExit(1)


if __name__ == "__main__":
    main()
