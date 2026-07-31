# FlexForms JSON Template Designer Manual

A practical guide for designing application forms as JSON. Written for product/content designers and developers who are new to the FlexForms engine. Examples follow the Transfer Applications template patterns.

---

## 1. What you are building

A FlexForms template is a **JSON document** that describes:

1. The **structure** of an application (sections → tasks → pages → questions)
2. The **controls** users fill in (text, radios, dates, uploads, search, etc.)
3. **Rules** that show, hide, or skip questions based on answers
4. Optional **repeatable lists** (add many trusts, academies, members…)
5. Optional **derived lists** (e.g. one declaration per trust already entered)

Users see a **GOV.UK-style task list**. Completing tasks moves status from Not started → In progress → Completed. Answers are stored by **`fieldId`**.

Think of the JSON as a blueprint. The engine renders it; you do not write HTML for each question.

---

## 2. Big picture hierarchy

```
FormTemplate
 └── taskGroups[]          ← major sections on the task list
      └── tasks[]          ← rows under each section
           ├── pages[]     ← normal question pages (linear task)
           └── summary     ← OR collection / declaration flows
                ├── flows[]          (multiCollectionFlow)
                └── derivedFlows[]   (derivedCollectionFlow)
```

| Layer | User sees | You define |
|--------|-----------|------------|
| **Template** | Whole service form | Name, description, global rules |
| **Task group** | Heading on task list | e.g. “About transferring academies” |
| **Task** | Clickable task row | e.g. “Risks”, “Members” |
| **Page** | One screen / question | Title, fields, save behaviour |
| **Field** | One input | Type, label, validation |

**Rule of thumb**

- Use **`pages`** for a fixed sequence of questions.
- Use **`summary.mode = multiCollectionFlow`** when users must **add 0..N items**, each with its own mini-wizard.
- Use **`summary.mode = derivedCollectionFlow`** when items come from **another collection** (e.g. declarations for each outgoing trust).

In Transfer: “Trust details” uses a collection; “Reason and benefits” uses plain pages; “Declaration” uses derived flows.

---

## 3. Root template object

```json
{
  "templateId": "form-001",
  "templateName": "Transfer Applications",
  "description": "A dynamic form for Transfer Applications",
  "taskGroups": [ ],
  "conditionalLogic": [ ],
  "defaultFieldRequirementPolicy": "required",
  "hideFieldLabelWhenOnlyOneField": true,
  "contributorPattern": true
}
```

| Property | Purpose |
|----------|---------|
| `templateId` | Logical id in the JSON (not the DB GUID). Keep stable across versions when possible. |
| `templateName` | Human name. |
| `description` | Short summary. |
| `taskGroups` | Required. At least one group with one task. |
| `conditionalLogic` | Optional. Show/hide/skip rules. |
| `defaultFieldRequirementPolicy` | `"required"` or `"optional"`. If a field has no `required` validation and no `required` flag, this policy applies. Transfer uses `"required"`. |
| `hideFieldLabelWhenOnlyOneField` | When `true` (default), a page with **exactly one** normal field hides that field’s label so the **page title** is the question. Complex fields are excluded from this behaviour. |
| `contributorPattern` | When `true` (default), invite-contributor features are available. Set `false` to hide them. |

---

## 4. Task groups

```json
{
  "groupId": "transferring-academies-group",
  "groupName": "About transferring academies",
  "groupOrder": 2,
  "groupStatus": "Incomplete",
  "tasks": [ ]
}
```

| Property | Notes |
|----------|--------|
| `groupId` | Unique id. Use kebab-case. |
| `groupName` | Shown as a section heading. |
| `groupOrder` | Sort order (1, 2, 3…). |
| `groupStatus` | Initial status text; engine updates as users progress. |
| `tasks` | Tasks in this section. |

Transfer groups: joining trust → transferring academies → leaving trusts → declaration.

---

## 5. Tasks

Two shapes:

### A. Linear task (pages only)

```json
{
  "taskId": "reason-and-benefits-trust",
  "taskName": "Reason and benefits",
  "caption": "{detailsOfIncomingTrust.incomingTrustsSearch-field-flow.name ?? Reason and benefits}",
  "taskOrder": 3,
  "taskStatus": "Incomplete",
  "pages": [ ],
  "summary": null,
  "startAtFirstPageWhenNotStarted": null,
  "visibleInTaskList": null
}
```

### B. Collection / derived task (summary, pages usually null)

```json
{
  "taskId": "incoming-trust-details",
  "taskName": "Trust details",
  "caption": null,
  "taskOrder": 1,
  "taskStatus": "Incomplete",
  "pages": null,
  "summary": {
    "mode": "multiCollectionFlow",
    "flows": [ ]
  },
  "visibleInTaskList": true
}
```

| Property | Notes |
|----------|--------|
| `taskId` | Unique. |
| `taskName` | Task list label. |
| `caption` | Optional text above page titles. Supports **bindings** (see §12). |
| `taskOrder` | Order within the group. |
| `taskStatus` | `"NotStarted"`, `"InProgress"`, `"Completed"`, `"CannotStartYet"`, or legacy `"Incomplete"`. |
| `pages` | Linear pages, or `null` if the task is collection-driven. |
| `summary` | Collection / derived configuration, or `null`. |
| `startAtFirstPageWhenNotStarted` | If `true`, opens first page instead of summary when not started. |
| `visibleInTaskList` | Set `true` for collection tasks that should appear on the task list. |

---

## 6. Pages

```json
{
  "pageId": "proposedTransferDate-page",
  "slug": "proposed-transfer-date",
  "title": "What is the proposed transfer date?",
  "description": "",
  "pageOrder": 2,
  "fields": [ ],
  "returnToSummaryPage": false,
  "saveButtonLabel": null
}
```

| Property | Notes |
|----------|--------|
| `pageId` | Unique id. Conditional logic often targets this. |
| `slug` | URL-friendly segment. |
| `title` | Main question / heading. |
| `description` | Extra body text (Markdown-friendly). Can include links and `{displayName}` in derived flows. |
| `pageOrder` | Order in the task or collection wizard. |
| `fields` | One or more fields. |
| `returnToSummaryPage` | After save: return to task/collection summary (`true`) or continue the wizard (`false`). Transfer often uses `false` mid-wizard and `true` on the last page of a sub-flow. |
| `saveButtonLabel` | Override button text, e.g. `"Sign the declaration"`. |

**Design tip:** Prefer **one question per page** (GOV.UK pattern). Put related short fields (name / phone / email) on one page when they form one “contact details” block.

---

## 7. Fields — common properties

Every field shares this shape:

```json
{
  "fieldId": "incomingTrustAccountingOfficerFullName",
  "type": "text",
  "label": {
    "value": "Full name",
    "isVisible": true,
    "validationLabelValue": null
  },
  "placeholder": "Full name",
  "tooltip": "",
  "required": null,
  "order": 1,
  "visibility": { "default": true },
  "validations": [ ],
  "options": null,
  "complexField": null,
  "Value": null
}
```

| Property | Notes |
|----------|--------|
| `fieldId` | **Answer key**. Must be unique in the template. Never reuse casually; changing it breaks existing answers. |
| `type` | Control type (next section). |
| `label.value` | Label text. |
| `label.isVisible` | Show label (`true`) or hide it (`false`) when the page title is enough. |
| `label.validationLabelValue` | Name used in errors (useful for dates: “Proposed transfer date”). |
| `placeholder` | Grey hint inside empty inputs. |
| `tooltip` | Hint under the label. Supports Markdown and links, e.g. `[text](https://...)`. |
| `required` | Optional bool; usually prefer a `required` **validation** instead. |
| `order` | Display order on the page. |
| `visibility.default` | Starting visibility before conditional logic runs. |
| `validations` | Rules (required, regex, maxLength…). |
| `options` | For radios / checkboxes / select. |
| `complexField` | Only for `type: "complexField"`. |
| `Value` | Leave `null` in the template (runtime answers live elsewhere). |

---

## 8. Field types (with examples)

### 8.1 `text` — single line

```json
{
  "fieldId": "memberName",
  "type": "text",
  "label": { "value": "Name", "isVisible": true },
  "validations": [
    { "type": "required", "rule": "", "message": "Enter the full name of the member" },
    { "type": "maxLength", "rule": 100, "message": "Full name must be 100 characters or less" }
  ]
}
```

Use for names, roles, short free text. For phone numbers Transfer still uses `text` + a **regex** validation (UK numbers).

---

### 8.2 `email`

```json
{
  "fieldId": "incomingTrustMainContactEmailAddress",
  "type": "email",
  "label": { "value": "Email address", "isVisible": true },
  "tooltip": "We'll only use it to contact them about this application",
  "validations": [
    { "type": "required", "rule": true, "message": "Enter an email address" }
  ]
}
```

Engine also checks email format even without an extra regex.

---

### 8.3 `character-count` — long answers with a limit

```json
{
  "fieldId": "reasonAndBenefitsTrustStrategicNeeds",
  "type": "character-count",
  "label": {
    "value": "What are the strategic needs of the trust?",
    "isVisible": false
  },
  "validations": [
    { "type": "maxLength", "rule": 2000, "message": "You must enter 2000 characters or less" },
    { "type": "required", "rule": true, "message": "Enter the strategic needs of the trust." }
  ]
}
```

- Pair with `maxLength` (characters) or `maxWords`.
- Transfer often hides the field label (`isVisible: false`) because the **page title** is the question.

---

### 8.4 `text-area`

Multi-line without the character-count widget. Use `character-count` when you need a visible limit (GOV.UK pattern).

---

### 8.5 `radios` — pick one

```json
{
  "fieldId": "incomingTrustTypeOfTrust",
  "type": "radios",
  "label": { "value": "What is the type of trust?", "isVisible": true },
  "options": [
    { "value": "Single academy trust", "label": "Single academy trust" },
    { "value": "Multi-academy trust", "label": "Multi-academy trust" }
  ],
  "validations": [
    {
      "type": "required",
      "rule": "",
      "message": "Select if it is a single academy trust or a multi academy trust"
    }
  ]
}
```

**Critical:** Conditional logic compares against **`options[].value`**, not `label`.  
Yes/No in Transfer is usually `"yes"` / `"no"` (lowercase). Elsewhere you may see `"Yes"` / `"No"` — be consistent and match rules exactly.

---

### 8.6 `checkboxes` — pick many

Same `options` shape as radios. Values are multi-select. Useful for “select all that apply”.

---

### 8.7 `select` — dropdown

Same `options` shape. Prefer radios for short lists (GOV.UK); use select for long lists.

---

### 8.8 `date`

```json
{
  "fieldId": "proposedTransferDate",
  "type": "date",
  "label": {
    "value": "What is the proposed transfer date?",
    "isVisible": false,
    "validationLabelValue": "Proposed transfer date"
  },
  "tooltip": "For example, 27 3 2026",
  "validations": [
    { "type": "required", "rule": true, "message": "Enter the proposed transfer date" }
  ]
}
```

Renders day / month / year. Engine checks completeness and that the date is real. Use `validationLabelValue` so errors read well when the visible label is hidden.

---

### 8.9 `autocomplete` (simple)

A built-in autocomplete control. For Trusts/Academies search, Transfer uses **`complexField`** instead (API-backed). Prefer complex fields when search hits an external API.

---

### 8.10 `complexField` — search or upload

Template side only references a **config id** defined in tenant **FormEngine:ComplexFields** settings (not inside the template JSON).

#### Trust search

```json
{
  "fieldId": "incomingTrustsSearch-field-flow",
  "type": "complexField",
  "label": { "value": "Trusts", "isVisible": false },
  "placeholder": "Start typing to search for Trusts...",
  "tooltip": "Enter at least 3 characters to search by name, UKPRN, or Companies House number",
  "complexField": { "id": "TrustComplexField" },
  "validations": [
    { "type": "regex", "rule": ".{3,}", "message": "You must enter 3 characters or more to search for a trust" },
    { "type": "required", "rule": true, "message": "Enter 3 characters or more to search for a trust" }
  ]
}
```

Stored value is typically a rich object (e.g. with `.name`). Bindings like  
`{detailsOfIncomingTrust.incomingTrustsSearch-field-flow.name}` read nested properties.

#### Academy search

```json
"complexField": { "id": "EstablishmentComplexField" }
```

#### File upload

```json
{
  "fieldId": "incomingTrustUploadBoardResolution",
  "type": "complexField",
  "label": { "value": "Board resolution", "isVisible": false },
  "tooltip": "This is the minutes from the meeting where...",
  "complexField": { "id": "UploadDocumentsComplexField" },
  "validations": [
    { "type": "required", "rule": true, "message": "Select a file" }
  ]
}
```

**You cannot invent a new complex field id** in JSON alone — ops must register it under FormEngine settings (`autocomplete` or `upload`, API URL, keys, etc.).

---

## 9. Validation rules

```json
{
  "type": "required",
  "rule": true,
  "message": "Enter the full name",
  "condition": null
}
```

| `type` | `rule` | Meaning |
|--------|--------|---------|
| `required` | often `true` or `""` | Must answer |
| `regex` | pattern string | Must match |
| `maxLength` | number | Max characters |
| `maxWords` | number | Max words |

`condition` can attach a rule only when another condition is true (advanced; Transfer mostly uses top-level `conditionalLogic` instead).

**UK phone pattern (from Transfer):**

```text
^(?:0|\+?44)\s?(\d\s?){9,10}$
```

(In JSON this is escaped as needed.)

---

## 10. Collection flows (`multiCollectionFlow`)

Use when users add **many similar items**.

### Summary shell

```json
"summary": {
  "mode": "multiCollectionFlow",
  "title": null,
  "description": null,
  "flows": [ ],
  "derivedFlows": null
}
```

One task can have **several flows** (Transfer “Members”: members after transfer + members leaving).

### One flow

```json
{
  "flowId": "detailsOfAcademies",
  "title": "",
  "description": "",
  "fieldId": "detailsOfAcademies",
  "addButtonLabel": "Add an academy",
  "minItems": 1,
  "maxItems": 50,
  "itemKind": "Academy",
  "itemKindPlural": "Academies",
  "itemTitleBinding": "academiesSearch.name",
  "summaryColumns": [
    { "label": "Academy name", "field": "academiesSearch" },
    { "label": "Proposed transfer date", "field": "proposedTransferDate" }
  ],
  "addItemMessage": "{academiesSearch.name} has been added",
  "updateItemMessage": "{academiesSearch.name} has been updated",
  "deleteItemMessage": "{academiesSearch.name} has been removed",
  "tableType": "card",
  "pages": [ ]
}
```

| Property | Meaning |
|----------|---------|
| `flowId` | Unique flow id. |
| `fieldId` | Where the **array of items** is stored in answers. |
| `addButtonLabel` | Button on the summary. |
| `minItems` / `maxItems` | Limits (e.g. trusts joining: max 1; members: min 3). |
| `itemKind` / `itemKindPlural` | Wording (“Trust”, “Academies”). |
| `itemTitleBinding` | Path for card title (`name`, `academiesSearch.name`, …). |
| `summaryColumns` | Rows shown on each card / list. `field` = fieldId inside the item. |
| `tableType` | `"card"` (Transfer) or `"list"`. |
| `pages` | Pages users walk when adding/editing **one** item. |
| Messages | Support `{fieldId}`, `{flowTitle}`, nested paths. |

**Incoming trust with max 1** still uses a collection so the UX is “add trust” + check-answers card, not a plain linear task.

---

## 11. Derived flows (`derivedCollectionFlow`)

Creates one item **per entry already captured** in another collection.

Transfer declarations:

- Source: `detailsOfIncomingTrust` → one declaration for the joining trust
- Source: `detailsOfOutgoingTrusts` → one declaration per leaving trust

```json
"summary": {
  "mode": "derivedCollectionFlow",
  "title": "Declaration from all chairs of trustees",
  "description": "The chair of trustees from each trust involved...",
  "flows": null,
  "derivedFlows": [
    {
      "flowId": "trust-declarations-joining",
      "title": "Declaration for the trust that academies are joining",
      "sourceFieldId": "detailsOfIncomingTrust",
      "sourceType": "collection",
      "fieldId": "trustDeclarations",
      "itemTitleBinding": "name",
      "sectionOrder": 1,
      "signedMessage": "Declaration for {displayName} has been signed",
      "statusField": "status",
      "pages": [
        {
          "pageId": "declaration-form-joining",
          "slug": "declaration-form",
          "title": "Declaration form",
          "description": "I hereby certify...\n\n## Name of trust\n\n{displayName}",
          "saveButtonLabel": "Sign the declaration",
          "fields": [ ]
        }
      ]
    }
  ]
}
```

| Property | Meaning |
|----------|---------|
| `sourceFieldId` | Collection `fieldId` to expand. |
| `sourceType` | How to read source: `"collection"`, `"autocomplete"`, `"checkboxes"`, `"select"`. |
| `fieldId` | Storage for derived answers / status. |
| `itemTitleBinding` | Display name property. |
| `signedMessage` | Banner after signing; `{displayName}` / `{name}`. |
| `pages` | Form each derived item must complete. |

Page `description` can include Markdown and `{displayName}` for the current trust/academy name.

---

## 12. Binding and captions

### Syntax

| Pattern | Meaning |
|---------|---------|
| `{fieldId}` | Insert answer |
| `{fieldId.property}` | Nested property (e.g. trust `.name`) |
| `{path ?? Fallback text}` | Use fallback if empty |

### Examples from Transfer

**Task caption showing trust name:**

```json
"caption": "{detailsOfIncomingTrust.incomingTrustsSearch-field-flow.name ?? Reason and benefits}"
```

**Collection success message:**

```json
"addItemMessage": "{memberName} has been added to {flowTitle}"
```

**Item title on cards:**

```json
"itemTitleBinding": "incomingTrustsSearch-field-flow.name"
```

Use captions sparingly — they personalise later tasks once the joining trust exists.

---

## 13. Conditional logic

Rules live at the **template root**, not on each field.

### Anatomy

```json
{
  "id": "hide-pupil-forecast-for-no-risk",
  "name": "Hide pupil forecast page when no pupil number risk",
  "priority": 60,
  "enabled": true,
  "conditionGroup": {
    "logicalOperator": "AND",
    "conditions": [
      {
        "triggerField": "risksPupilNumbers",
        "operator": "equals",
        "value": "no",
        "dataType": "string"
      }
    ]
  },
  "affectedElements": [
    {
      "elementId": "risks-upload-pupil-numbers",
      "elementType": "page",
      "action": "skip"
    },
    {
      "elementId": "risksUploadPupilNumbers",
      "elementType": "field",
      "action": "hide"
    }
  ],
  "executeOn": ["change", "load"],
  "debounce": 300
}
```

### How to think about it

1. **Trigger** — which field’s answer changes the form (`triggerField`).
2. **Condition** — e.g. equals `"no"`.
3. **Effects** — usually **both**:
   - `page` + `skip` / `show` (navigation)
   - `field` + `hide` / `show` (answers / validation)

Transfer always pairs page skip with field hide (and show with show).

### Useful operators

`equals`, `notEquals`, `in`, `notIn`, `contains`, `isEmpty`, `isNotEmpty`, `greaterThan`, `lessThan`, …

### Actions you will use most

| Action | On | Effect |
|--------|-----|--------|
| `show` / `hide` | field, page | Visibility |
| `skip` | page | Skip in navigation when condition true |
| `require` / `makeOptional` | field | Requirement |
| `enable` / `disable` | field | Interaction |

### Priority

Lower `priority` runs first. Transfer often uses **60 = hide/skip** and **61 = show** for the opposite rule.

### AND / OR groups

Example: hide a page if **either** “no SAP academies” **or** “no LA agreements”:

```json
"conditionGroup": {
  "logicalOperator": "OR",
  "conditions": [
    {
      "triggerField": "financeAndOperationsLocalAuthorityAgreements",
      "operator": "equals",
      "value": "no",
      "dataType": "string"
    },
    {
      "triggerField": "financeAndOperationsHaveSAPAcademies",
      "operator": "equals",
      "value": "no",
      "dataType": "string"
    }
  ]
}
```

### Classic follow-up pattern

1. Radios: “Does X apply?” → `yes` / `no`
2. Next page: details / upload
3. Rules: if `no` → skip + hide follow-up; if `yes` → show both

Used throughout Transfer (growth plan, diocesan consent, pupil forecast, etc.).

**Gotcha:** Option `value` strings must match conditional logic `value` strings **exactly**, or rules will not fire. Prefer aligning option values with rule values (e.g. always `"yes"` / `"no"`).

---

## 14. Minimal starter template

```json
{
  "templateId": "my-first-form",
  "templateName": "My first form",
  "description": "A simple starter application",
  "defaultFieldRequirementPolicy": "optional",
  "hideFieldLabelWhenOnlyOneField": true,
  "contributorPattern": false,
  "taskGroups": [
    {
      "groupId": "about-you-group",
      "groupName": "About you",
      "groupOrder": 1,
      "groupStatus": "NotStarted",
      "tasks": [
        {
          "taskId": "your-details",
          "taskName": "Your details",
          "taskOrder": 1,
          "taskStatus": "NotStarted",
          "pages": [
            {
              "pageId": "full-name-page",
              "slug": "full-name",
              "title": "What is your full name?",
              "description": "",
              "pageOrder": 1,
              "fields": [
                {
                  "fieldId": "fullName",
                  "type": "text",
                  "label": { "value": "Full name", "isVisible": false },
                  "order": 1,
                  "validations": [
                    { "type": "required", "rule": true, "message": "Enter your full name" }
                  ]
                }
              ],
              "returnToSummaryPage": true
            }
          ]
        }
      ]
    }
  ],
  "conditionalLogic": []
}
```

Grow by adding pages, then radios + follow-up logic, then a small collection flow.

---

## 15. Naming conventions (recommended)

| Thing | Convention | Example |
|-------|------------|---------|
| Ids | kebab-case or camelCase, unique | `risks-pupil-numbers`, `risksPupilNumbers` |
| `pageId` | often ends with `-page` | `proposedTransferDate-page` |
| `slug` | kebab-case URL | `proposed-transfer-date` |
| `fieldId` | camelCase, descriptive | `proposedTransferDate` |
| Collection `fieldId` | plural / flow name | `detailsOfAcademies` |
| Complex field config ids | PascalCase, shared | `TrustComplexField` |

Keep **pageId** and **fieldId** distinct: conditional logic targets both.

---

## 16. Design checklist

1. Sketch the **task list** (groups → tasks) before JSON.
2. Decide per task: **linear pages** vs **collection** vs **derived**.
3. One clear **question per page** where possible.
4. Give every field a stable **`fieldId`**.
5. Add **required** messages that sound like GOV.UK (“Enter…”, “Select…”).
6. For Yes/No follow-ups, add **paired** show/hide rules.
7. For collections, set **min/max**, **columns**, and **itemTitleBinding**.
8. Validate JSON (commas, quotes).
9. Upload via **Template Manager** as a new version; test Live vs Not live.
10. Remember: **complexField** ids must exist in tenant FormEngine settings.

---

## 17. How Transfer maps to features

| Feature | Where in Transfer |
|---------|-------------------|
| Linear pages | Reason and benefits, Risks, Finance… |
| Radios + character-count | Almost every section |
| Dates | Proposed transfer date, declaration signed date |
| Complex search | Trust / academy finders |
| Uploads | Board resolutions, growth plans, forecasts |
| Collection max 1 | Incoming trust details |
| Collection many | Academies, outgoing trusts, members, trustees |
| Multiple flows in one task | Members after / leaving; Trustees after / leaving |
| Derived declarations | Chairs of trustees |
| Captions with bindings | Tasks under joining trust |
| Conditional skip/show | Growth plan, diocesan consent, deficits, “worked together”, etc. |
| Custom save label | “Sign the declaration” |
| Markdown tooltips / descriptions | Guidance links and multi-line hints |

---

## 18. What this JSON does *not* include

- Database template GUID / tenant ownership
- FormEngine complex field API URLs and keys (tenant settings)
- Email templates, auth, hostnames
- HTML/CSS — the engine renders GOV.UK components

---