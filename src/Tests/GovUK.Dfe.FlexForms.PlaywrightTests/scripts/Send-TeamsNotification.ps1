#!/usr/bin/env pwsh
<#
  .SYNOPSIS
    Posts a Playwright/.NET test run summary to a Microsoft Teams webhook.

  .DESCRIPTION
    Replaces scripts/send-teams-notification.js. Reads the TRX file produced by
    `dotnet test --logger trx` (the TS suite's html/json reporters read reports/report.json;
    dotnet test's trx logger is the .NET equivalent) instead of Playwright's JSON reporter
    output, but keeps the same Adaptive Card fields, failed-test limit (10, "...and N more"),
    error-message cleanup/truncation, and environment variables as the script it replaces.

  .NOTES
    Env vars (same names as the Node script): TEAMS_WEBHOOK_URL, ENVIRONMENT, INFORMATION_LINK.
    Also reads the GitHub Actions default env vars GITHUB_REF / GITHUB_WORKFLOW.
#>

$ErrorActionPreference = 'Stop'

function Get-ReportData {
    $reportPath = Join-Path (Get-Location) 'reports/report.trx'

    $stats = [ordered]@{ Tests = 0; Passes = 0; Failures = 0 }
    $failedTests = @()

    if (-not (Test-Path $reportPath)) {
        Write-Warning "Report file not found at: $reportPath"
        return [pscustomobject]@{ Stats = $stats; FailedTests = $failedTests }
    }

    [xml]$trx = Get-Content $reportPath

    $counters = $trx.TestRun.ResultSummary.Counters
    $stats.Tests = [int]$counters.total
    $stats.Passes = [int]$counters.passed
    $stats.Failures = [int]$counters.failed

    if ($stats.Failures -gt 0) {
        $classNameByTestId = @{}
        foreach ($unitTest in $trx.TestRun.TestDefinitions.UnitTest) {
            $classNameByTestId[$unitTest.id] = $unitTest.TestMethod.className
        }

        foreach ($result in $trx.TestRun.Results.UnitTestResult) {
            if ($result.outcome -ne 'Failed') {
                continue
            }

            $className = $classNameByTestId[$result.testId]
            $fullTitle = if ($className) { "$className > $($result.testName)" } else { $result.testName }
            $errorMessage = $result.Output.ErrorInfo.Message
            if (-not $errorMessage) {
                $errorMessage = 'No error message available'
            }

            $failedTests += [pscustomobject]@{
                FullTitle    = $fullTitle
                ErrorMessage = (Format-ErrorMessage $errorMessage)
            }
        }
    }

    return [pscustomobject]@{ Stats = $stats; FailedTests = $failedTests }
}

function Format-ErrorMessage {
    param([string]$ErrorMessage)

    if (-not $ErrorMessage) {
        return 'No error message available'
    }

    $lines = $ErrorMessage -split "`n"
    $relevantLines = @()

    foreach ($line in $lines) {
        if ($line -match '^\s*at ') {
            break
        }
        if ($line.Trim() -ne '') {
            $relevantLines += $line.Trim()
        }
    }

    if ($relevantLines.Count -gt 0) {
        return ($relevantLines -join ' ').Trim()
    }

    return $lines[0]
}

function Limit-Text {
    param([string]$Text, [int]$MaxLength)

    if (-not $Text -or $Text.Length -le $MaxLength) {
        return $Text
    }

    return $Text.Substring(0, $MaxLength) + '...'
}

function New-CardBody {
    param($Stats, $FailedTests)

    $hasFailures = $Stats.Failures -gt 0
    $statusText = if ($hasFailures) { '**Playwright Test Run Failed**' } else { '**Playwright Test Run Passed**' }

    $cardBody = [System.Collections.Generic.List[object]]::new()
    $cardBody.Add([ordered]@{ type = 'TextBlock'; wrap = $true; text = $statusText; size = 'large'; horizontalAlignment = 'center' })
    $cardBody.Add([ordered]@{ type = 'TextBlock'; wrap = $true; text = "**Branch:** $($env:GITHUB_REF)" })
    $cardBody.Add([ordered]@{ type = 'TextBlock'; wrap = $true; text = "**Workflow:** $($env:GITHUB_WORKFLOW)" })
    $cardBody.Add([ordered]@{ type = 'TextBlock'; wrap = $true; text = "**Environment:** $($env:ENVIRONMENT)" })
    $cardBody.Add([ordered]@{
        type  = 'FactSet'
        facts = @(
            [ordered]@{ title = 'Total Tests:'; value = "$($Stats.Tests)" }
            [ordered]@{ title = 'Passed:'; value = "$($Stats.Passes)" }
            [ordered]@{ title = 'Failed:'; value = "$($Stats.Failures)" }
        )
    })

    if ($hasFailures -and $FailedTests.Count -gt 0) {
        Add-FailedTestDetails -CardBody $cardBody -FailedTests $FailedTests
    }

    $informationLink = $env:INFORMATION_LINK
    $cardBody.Add([ordered]@{
        type    = 'TextBlock'
        wrap    = $true
        text    = "**See more information:** [$informationLink]($informationLink)"
        spacing = 'medium'
    })

    return $cardBody
}

function Add-FailedTestDetails {
    param($CardBody, $FailedTests)

    $CardBody.Add([ordered]@{ type = 'TextBlock'; wrap = $true; text = '**Failed Tests:**'; weight = 'bolder'; spacing = 'medium' })

    for ($i = 0; $i -lt $FailedTests.Count; $i++) {
        if ($i -ge 10) {
            break
        }

        $test = $FailedTests[$i]
        $CardBody.Add([ordered]@{ type = 'TextBlock'; wrap = $true; text = "**$($i + 1).** $($test.FullTitle)"; weight = 'bolder'; spacing = 'small' })
        $CardBody.Add([ordered]@{ type = 'TextBlock'; wrap = $true; text = "*Error:* $(Limit-Text $test.ErrorMessage 500)"; spacing = 'none'; isSubtle = $true })
    }

    if ($FailedTests.Count -gt 10) {
        $remaining = $FailedTests.Count - 10
        $CardBody.Add([ordered]@{
            type    = 'TextBlock'
            wrap    = $true
            text    = "*... and $remaining more failed tests. See full report for details.*"
            spacing = 'small'
            isSubtle = $true
        })
    }
}

function New-TeamsMessage {
    param($CardBody, [bool]$HasFailures)

    $style = if ($HasFailures) { 'attention' } else { 'good' }

    return [ordered]@{
        type        = 'message'
        attachments = @(
            [ordered]@{
                contentType = 'application/vnd.microsoft.card.adaptive'
                contentUrl  = $null
                content     = [ordered]@{
                    '$schema' = 'https://adaptivecards.io/schemas/adaptive-card.json'
                    type      = 'AdaptiveCard'
                    version   = '1.2'
                    body      = @(
                        [ordered]@{
                            type  = 'Container'
                            style = $style
                            items = $CardBody
                        }
                    )
                }
            }
        )
    }
}

try {
    $report = Get-ReportData
    $cardBody = New-CardBody -Stats $report.Stats -FailedTests $report.FailedTests
    $message = New-TeamsMessage -CardBody $cardBody -HasFailures ($report.Stats.Failures -gt 0)

    $json = $message | ConvertTo-Json -Depth 20
    Invoke-RestMethod -Uri $env:TEAMS_WEBHOOK_URL -Method Post -ContentType 'application/json' -Body $json | Out-Null

    Write-Host 'Message sent to Teams successfully'
}
catch {
    Write-Error "Error sending notification to Teams: $_"
    exit 1
}
