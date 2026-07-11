# Workflow

- For each milestone, draft a plan first and present it to the user; execution starts only after the user approves the plan.
- Never open a pull request automatically — always confirm with the user first.
- Make minimal, focused changes; do not refactor unrelated code in the same change.
- One logical change per commit, with an imperative-mood message explaining *why*.
- When unsure between two designs, present both with trade-offs and ask before implementing.
- Never commit or push directly to the default branch (`master`/`main`). Milestones are used to track the progress of the project. All work happens on a `feature/M<number>-<desc>`-prefixed branch.
- The branch lands on the default branch only through a reviewed pull request. Enforce this with branch protection (require a PR, disallow direct pushes, require the squash-merge strategy) so it can't be bypassed by accident.
- Merge by squash so each feature arrives as a single logical commit on the default branch, consistent with the "one logical change per commit" rule above; the squash commit message keeps the imperative, *why*-focused form.
- Versioning follows semantic versioning: each phase gets its own minor version (Phase 1 → `0.1.x`, Phase 2 → `0.2.x`, …; Phase 6 ships `1.0.0`).
- When a phase completes, tag it on the default branch with an annotated tag (e.g., `git tag -a v0.1.0 -m "..."`) and push the tag to GitHub for reference.
- Each roadmap milestone has a matching GitHub milestone (created per phase); the milestone's PR is associated on creation and the milestone closed on merge.
- Update this file when a new convention or correction is established.
