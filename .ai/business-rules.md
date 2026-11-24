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

Invite page (`Contributors-Invite`) lets the lead applicant choose direct Application / ApplicationFiles grants for that invite (**Verified**). **View this {singular}** is always on (singular from template display copy, else Organisation `ApplicationTerminology`). Defaults follow the template `contributorRoleId` (`ContributorInviteDefaultAccess`): User / omitted → Change and all file options selected; any other role → view-only (Change off; View uploaded files on when the form has uploads). Labels use `ITemplateDisplayCopy.Singular` after `ITemplateCopyContext.Use` on the loaded template. The contributors list (`Contributors`) loads those grants with `GetContributorsAsync(..., includePermissionDetails: true)` and shows each invitee as a GOV.UK summary card (`ContributorAccessLabels` maps Application Read/Write and ApplicationFiles Read/Write/Delete to the same labels). File options are shown only when the template has an upload field (`TemplateUploadFieldDetector`, including pages under `Summary.Flows` / `Summary.DerivedFlows`). Upload or delete without “view files” still sends files Read. The API treats these flags as the effective grants for that application (re-invite replaces prior Application / ApplicationFiles grants; inviting the owner’s own email is rejected; role `Any` file read does not fill unticked boxes). GetMyPermissions returns `InviteScopedApplicationKeys` so Web claim reconstruction re-emits `ContributorInvite:{key}:Scope` markers. Template `contributorRoleId` still sets tenant membership only.

## File summary display

Task summary filenames for complex uploads require tenant FormEngine `ComplexFields` `AllowMultiple: true`. Missing config → blank names though upload succeeded. Refresh tenant settings after changing FormEngine JSON.

## Who can change an application

**Verified** (`BaseFormPageModel.IsApplicationEditable`, `BaseFormPageModelTests`). Admins and SuperAdmins can change an application in any status; an admin save to a deleted application moves it back to In progress. Everyone else needs Write (or to be the lead applicant) and only while it is Created or In progress. A service-authenticated caller with an Admin role is not an admin. The API enforces the same rule (`ApplicationAmendmentPolicy`), so a stale page that posts anyway gets a 403, which `ExternalApiExceptionFilter` shows as the form with "You do not have permission to make changes to this application." (or the upload/delete equivalent), never a logout. On the standalone upload page (`/applications/{ref}/…/upload-file`) the filter instead sends the user back to the page's local return URL with the upload error saved in `IFormErrorStore`, and does so even when the 403 message mentions a token (`ExternalApiExceptionFilterTests`).

## Submit

Web calls API submit. File-validation banners update via SignalR; preview/submit pages may need reload if the gate still sees `Pending`.

## Custom statuses

Labels from API; Web dashboard columns are template-driven (`DashboardColumnResolver`).

## Complex field name

A search question can set `complexField.label`. That replaces the tenant `FormEngine:ComplexFields` Label in summary item labels and in the `{name}` part of the confirmation heading. Blank keeps the tenant Label. Template Studio edits it as **Name in a sentence**. `confirmationTitle` replaces the whole heading. Blank keeps "Is this the right {name}?".
