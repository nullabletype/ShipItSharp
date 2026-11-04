# Agent delivery and review

Read [PROJECT.md](../PROJECT.md) and the accepted task before implementation. New or uncertain CLI flows need agreement on observable behaviour first. Routine repairs can proceed using existing decisions.

## Deliver one bounded task

1. Inspect Git status and fetch origin. Start from current `origin/main` on a task branch, or verify the existing task branch and base when resuming. Preserve unrelated changes. Use an isolated checkout when concurrent writers would overlap.
2. Confirm scope, acceptance cases, architecture impact and required offline/live checks. Track active time and correction cycles in a sanitised checkpoint. Resolve decisions that would materially change the implementation before proceeding.
3. Implement the smallest complete change. For CLI behaviour changes, include unit tests and integration coverage for affected Octopus workflows. Test failures and partial success, cancellation and the confirmation boundary as applicable. Keep help and operator documentation aligned.
4. Run focused checks, then the contract's required gate. Record command, environment, exit result and passed/failed/skipped counts. Treat missing credentials, unavailable infrastructure and failing behaviour distinctly. For consequential rules, ask whether removing the rule would make its test fail.
5. Inspect intended files and outgoing history for secrets and local-only output. Commit only the task's changes. Record base and candidate SHA. Any later edits invalidate affected validation and approvals.
6. Request independent verification using the instructions below. Correct actionable findings within the remaining budget, revalidate and obtain review of the corrected candidate. The implementer may respond to a review but cannot replace its verdict.
7. Prepare the actual local walkthrough for changed operator experience or website work. Record Steven's approval with candidate SHA and reviewed scenarios. Apply the project's publication authority before push or PR creation/update.
8. When publication is authorised, observe CI for the published candidate through completion. Record branch SHA and synthetic merge SHA separately where applicable. Report ready for merge only after required checks, independent review, live evidence and human gates are satisfied. Integration and release follow their separate authority.

## Independent reviewer instructions

Use a separate agent/session that did not author the candidate. Provide the accepted task, base and candidate SHA, direct source/diff access, validation commands and known environment limits. A subagent may perform this review. It reads source and reports findings. It must not modify implementation or policy.

Run candidate-controlled commands only in a disposable verification environment without Octopus credentials, publication credentials, desktop access or unrelated private data. A fresh worktree alone does not provide that isolation. If execution isolation is unavailable, perform source review and mark independently executed checks pending. Required live checks use the separately approved test environment and the contract's serialisation rule.

The reviewer checks every acceptance case against implementation and evidence, inspects tests for an important missed failure, and examines CLI compatibility, confirmation placement, output/exit behaviour and architecture impact where affected. Verify documentation commands against source/help. Return severity-ordered findings with file/line, concrete failure, evidence and required correction.

Record the review in the PR body/comment, or the local checkpoint before publication.

| Field | Required content |
| --- | --- |
| Identity | Reviewer/session and confirmation it did not author the candidate |
| Revision | Task, base and candidate SHA, or changed-file content hashes before a local commit |
| Acceptance | Each criterion with observed evidence or explicit gap |
| Execution | Commands actually run, environment, result and limitations |
| Findings | Actionable defects and re-review scope after correction |
| Verdict | Accept, changes required, or pending, with outstanding human/live/CI gates |

## Stop and resume

Write a small checkpoint to ignored `.agent-work/handoff.md` when interrupted or stopped. Include task/scope, branch/base/candidate, intended and unrelated files, completed and pending evidence, review verdict, approval scope, active time/correction counters, owned processes/output, cleanup needs and the next safe action. Keep raw logs and credentials out.

On resume, inspect actual Git state and reconcile running commands and pending remote operations before retrying. Preserve counters. Only Steven can extend budgets or change scope. Missing verification remains visible until completed.
