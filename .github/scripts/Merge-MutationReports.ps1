<#
.SYNOPSIS
Aggregates per-project Stryker `mutation-report.md` files into a single combined
markdown report, mirroring the structure ReportGenerator produces for coverage
(overall summary block, then one collapsible per-project section).
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ReportsRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

$reportFiles = Get-ChildItem -Path $ReportsRoot -Filter 'mutation-report.md' -Recurse -File
if (-not $reportFiles) {
    Write-Error "No mutation-report.md files found under: $ReportsRoot"
    exit 1
}

$rootFullPath = (Resolve-Path -Path $ReportsRoot).Path

$totals = [ordered]@{
    Killed          = 0
    Survived        = 0
    Timeout         = 0
    NoCoverage      = 0
    Ignored         = 0
    CompileErrors   = 0
    RuntimeErrors   = 0
    TotalDetected   = 0
    TotalUndetected = 0
    TotalMutants    = 0
}

$projects = @()

foreach ($file in $reportFiles) {
    $relativePath = $file.FullName.Substring($rootFullPath.Length).TrimStart('\', '/')
    $artifactFolder = ($relativePath -split '[\\/]')[0]
    $projectName = $artifactFolder -replace '^mutation-report-', '' -replace '\.Tests$', ''

    $lines = Get-Content -Path $file.FullName

    $headerIndex = [array]::FindIndex($lines, [Predicate[string]] { param($line) $line.TrimStart().StartsWith('| File') })
    if ($headerIndex -lt 0) {
        Write-Error "Could not find file table header in: $($file.FullName)"
        exit 1
    }

    $tableLines = @()
    $i = $headerIndex
    while ($i -lt $lines.Count -and $lines[$i].TrimStart().StartsWith('|')) {
        $tableLines += $lines[$i]
        $i++
    }

    foreach ($row in $tableLines[2..($tableLines.Count - 1)]) {
        $cells = ($row.Trim() -replace '^\|', '' -replace '\|$', '') -split '\|' | ForEach-Object { $_.Trim() }
        $totals.Killed += [int]$cells[2]
        $totals.Survived += [int]$cells[3]
        $totals.Timeout += [int]$cells[4]
        $totals.NoCoverage += [int]$cells[5]
        $totals.Ignored += [int]$cells[6]
        $totals.CompileErrors += [int]$cells[7]
        $totals.RuntimeErrors += [int]$cells[8]
        $totals.TotalDetected += [int]$cells[9]
        $totals.TotalUndetected += [int]$cells[10]
        $totals.TotalMutants += [int]$cells[11]
    }

    $finalScoreMatch = $lines | Select-String -Pattern 'The final mutation score is ([\d.]+)%'
    $finalScore = if ($finalScoreMatch) { $finalScoreMatch.Matches[0].Groups[1].Value } else { 'N/A' }

    $projects += [PSCustomObject]@{
        Name       = $projectName
        FinalScore = $finalScore
        Table      = ($tableLines -join "`n")
    }
}

$projects = $projects | Sort-Object Name

$overallDetectedPlusUndetected = $totals.TotalDetected + $totals.TotalUndetected
$overallScoreText = if ($overallDetectedPlusUndetected -eq 0) {
    'N/A'
} else {
    '{0:N2}%' -f (100.0 * $totals.TotalDetected / $overallDetectedPlusUndetected)
}

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('# Mutation Testing Summary')
[void]$sb.AppendLine('<details open><summary>Summary</summary>')
[void]$sb.AppendLine()
[void]$sb.AppendLine('|||')
[void]$sb.AppendLine('|:---|:---|')
[void]$sb.AppendLine("| Generated on: | $(Get-Date -Format 'MM/dd/yyyy - HH:mm:ss') |")
[void]$sb.AppendLine("| Projects: | $($projects.Count) |")
[void]$sb.AppendLine("| **Mutation score:** | $overallScoreText ($($totals.TotalDetected) of $overallDetectedPlusUndetected) |")
[void]$sb.AppendLine("| Killed: | $($totals.Killed) |")
[void]$sb.AppendLine("| Survived: | $($totals.Survived) |")
[void]$sb.AppendLine("| Timeout: | $($totals.Timeout) |")
[void]$sb.AppendLine("| No Coverage: | $($totals.NoCoverage) |")
[void]$sb.AppendLine("| Ignored: | $($totals.Ignored) |")
[void]$sb.AppendLine("| Compile Errors: | $($totals.CompileErrors) |")
[void]$sb.AppendLine("| Runtime Errors: | $($totals.RuntimeErrors) |")
[void]$sb.AppendLine("| Total Detected: | $($totals.TotalDetected) |")
[void]$sb.AppendLine("| Total Undetected: | $($totals.TotalUndetected) |")
[void]$sb.AppendLine("| Total Mutants: | $($totals.TotalMutants) |")
[void]$sb.AppendLine()
[void]$sb.AppendLine('</details>')
[void]$sb.AppendLine()
[void]$sb.AppendLine('## Mutation score')

foreach ($project in $projects) {
    [void]$sb.AppendLine("<details><summary>$($project.Name) - $($project.FinalScore)%</summary>")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine($project.Table)
    [void]$sb.AppendLine()
    [void]$sb.AppendLine('</details>')
}

$outputDir = Split-Path -Path $OutputPath -Parent
if ($outputDir -and -not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

Set-Content -Path $OutputPath -Value $sb.ToString() -NoNewline
Write-Output "Combined mutation report written to: $OutputPath"
