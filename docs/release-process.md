# Release process

FlexForms uses trunk-based development with release branches. `flexforms-web` and `flexforms-api` follow the same process.

- `main` is the integration branch. Every merge deploys to **development**. Merging does not release anything.
- A release is cut from `main` into `release/X.Y`. Only that branch is deployed to **test**, as release candidates `vX.Y.Z-rc.N`.
- **production** only runs images that were already deployed to test. Nothing is rebuilt for production.
- The GitHub release marked **Latest** is what is live in production.

```text
main ──●──●──●──●──●──●──●──►            development (every merge)
            \           \
             release/2.3  release/2.4
              rc.1─rc.2─● v2.3.0         test (rc.N) → production (v2.3.0)
                         \
                          ● v2.3.1        hotfix
```

## Day to day (developers)

1. Branch from the latest `main`, open a PR into `main`, and get it reviewed. PR checks must pass.
2. Merge. The change deploys to development automatically. It only reaches production once it's part of a cut release.
3. If the change isn't ready to go live, keep it behind a feature flag, or ask the release owner to leave it out of the release (see below).

## Cutting a release (release owner)

1. Go to **Actions → Cut release → Run workflow**, from `main`.
   - Leave the inputs blank to cut the latest `main` with the next version (e.g. `release/2.4`).
   - Or set `commit` to cut from an earlier commit, or set `version` (e.g. `3.0`) for a major version.
2. The workflow creates `release/X.Y` and deploys it to test as `vX.Y.0-rc.1`. A GitHub pre-release with generated notes is created for the rc.
3. QA tests on test. Features merged to `main` from now on go into the next release, not this one.

## Fixing a bug found in test

1. Fix it on a branch from `main` and open a PR into `main` as normal.
2. Add the label `backport release/X.Y` to the PR. When it merges, a cherry-pick PR into `release/X.Y` is opened automatically.
3. Review and merge the backport PR. The next release candidate (`rc.N+1`) is built and deployed to test.

If the cherry-pick conflicts, the backport PR is opened as a draft with instructions. Resolve it on that branch.

## Leaving a feature out of a release

- If it's behind a feature flag, keep the flag off in production.
- Otherwise, revert it on the release branch only. The feature stays on `main` for the next release:

  ```bash
  git switch -c revert/feature-x origin/release/X.Y
  git revert <squash-commit-sha>        # or: git revert -m 1 <merge-commit-sha>
  git push -u origin revert/feature-x   # open a PR into release/X.Y
  ```

  Merging the PR produces a new release candidate without the feature. Don't use GitHub's **Revert** button for this, because it reverts on `main`.

## Going live (release owner)

1. Go to **Actions → Deploy to environment → Run workflow**.
2. Under **Use workflow from**, open the **Tags** tab and pick the release candidate that passed test (e.g. `v2.3.0-rc.2`).
3. Choose environment **production** and run it, then approve the production deployment when asked.
4. The image already built and tested for that rc is deployed. Release `v2.3.0` is then created on the same commit and marked **Latest**.

Production can't be deployed from a branch. The workflow fails if you try.

## Hotfix to production

1. Branch from `release/X.Y` (the release that is live), fix, and open a PR into `release/X.Y`.
2. Add the label `backport main` so the fix also lands on `main`.
3. Merging deploys `vX.Y.(Z+1)-rc.1` to test. Promote it as in **Going live**.

The hotfix only contains that fix. Nothing merged to `main` since the release goes with it.

## Rolling back

Run **Deploy to environment** from the tag of the previous release (e.g. `v2.2.0`) with environment **production**. That release is marked **Latest** again.

## What is in production?

- **Releases:** the release marked **Latest**, and its notes.
- **Deployments:** the repository's **Deployments → production** page shows history and approvals.
- **Compare:** `git log v2.2.0..v2.3.0 --oneline` shows what changed between two releases.

## Coordinating with flexforms-api

The two repos are released separately with their own versions. When a feature spans both:

- Cut both releases at the same time.
- Promote the API before the web app, unless the web change is backwards compatible.

## Rules and one-time setup

- Don't delete the newest `release/*` branch. `main` reads its version from it.
- Protect `main` and `release/**` with branch rules: PRs only, required checks and reviews.
- Configure the **production** environment with required reviewers.
- Optionally, add a `BACKPORT_TOKEN` secret (a GitHub App token or fine-grained PAT with contents and pull-requests write). Without it, backport PRs are opened by `GITHUB_TOKEN` and their CI checks don't start automatically. Close and reopen the PR to trigger them.
