# Checks which candidates contain the text "Microsoft.Xna.Framework" (GitHub code search, default branch).
# Covers permissive and copyleft candidates only. Code search allows about 10 calls a minute.
# Output: xna-verified.csv (repo, hits, status). Safe to re-run: finished repos are skipped.
$ErrorActionPreference = 'Continue'
$out = Join-Path $PSScriptRoot 'xna-verified.csv'
$all = Import-Csv (Join-Path $PSScriptRoot 'xna-repos-classified.csv')
$cand = $all | Where-Object {
    [int]$_.tier -le 2 -and $_.language -eq 'C#' -and ($_.kind -eq 'game' -or $_.kind -eq 'unknown') -and
    ($_.license_class -eq 'permissive' -or $_.license_class -eq 'copyleft')
} | Sort-Object { $_.license_class }, { -[int]$_.stars }

$done = @{}
$rows = New-Object System.Collections.Generic.List[object]
if (Test-Path $out) { foreach ($r in (Import-Csv $out)) { if ($r.status -eq 'ok') { $done[$r.repo] = $true; $rows.Add($r) } } }

$n = 0
foreach ($c in $cand) {
    $n++
    if ($done.ContainsKey($c.repo)) { continue }
    $hits = -1; $status = 'error'
    for ($try = 0; $try -lt 3; $try++) {
        $json = gh api -X GET search/code -f "q=Microsoft.Xna.Framework repo:$($c.repo)" -f per_page=1 2>$null
        if ($json) { $hits = ($json | ConvertFrom-Json).total_count; $status = 'ok'; break }
        Start-Sleep -Seconds 30
    }
    $rows.Add([pscustomobject]@{ repo = $c.repo; hits = $hits; status = $status })
    Write-Host ("{0}/{1} {2} hits={3} {4}" -f $n, @($cand).Count, $c.repo, $hits, $status)
    if ($n % 10 -eq 0) { $rows | Export-Csv $out -NoTypeInformation -Encoding UTF8 }
    Start-Sleep -Seconds 7
}
$rows | Export-Csv $out -NoTypeInformation -Encoding UTF8
Write-Host "DONE: $($rows.Count) rows -> $out"
