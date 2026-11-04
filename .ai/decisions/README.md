# Decisions (Web)

This repo has **no** `docs/adrs/` folder. Platform ADRs live in **flexforms-api** `docs/adrs/` (templates, File Share, ClamAV, Service Bus/SignalR, temporal tables).

Web-specific decisions evident in code:

| Decision | Notes |
|----------|--------|
| Clean Architecture + use-case quartet | CHANGELOG 2.1.0; NetArchTest guards. Rationale: keep PageModels thin after the form engine grew. |
| Platform bootstrap from API | Replaces `APPLICATION_NAME` folders. See API dual-database ADR-equivalent. |
| Fail-loud Redis session | After data-loss incident; CoreLibs `SessionDistributedCacheAdapter`. |
| Dual scan consumer | UX vs API storage delete. See API integrations. |

Do not invent alternatives. If you make a significant Web-only decision, add a markdown file here and mention it in `AGENTS.md`.
