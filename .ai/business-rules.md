# Business rules (Web)

API enforces persistence rules. Web enforces **navigation and UX** rules.

## Navigation (`SaveFormPageService` / `NavigateStandardPageAsync`)

**Verified** in `SaveFormPageService`:

- Save-and-continue uses `SaveFormPageService`, **not** a simpler helper that ignores conditionals.
- `returnToSummaryPage: true` still goes to the next **visible** page if a show-page conditional fires.
- Last question in a task returns to summary even if `returnToSummaryPage` is false.

## Live templates

Saving a template version in admin does not change in-flight applications. Users only see live templates they can access. Admins can preview non-live. Template managers see every template but can only choose the ones they have been given; the rest are greyed out (see `.ai/features/admin-and-studio.md`).

## Contributors

Shown when template JSON `contributorPattern` is true (`ContributorPatternService`). Invites go through the API.

## File summary display

Task summary filenames for complex uploads require tenant FormEngine `ComplexFields` `AllowMultiple: true`. Missing config → blank names though upload succeeded. Refresh tenant settings after changing FormEngine JSON.

## Submit

Web calls API submit. File-validation banners update via SignalR; preview/submit pages may need reload if the gate still sees `Pending`.

## Custom statuses

Labels from API; Web dashboard columns are template-driven (`DashboardColumnResolver`).
