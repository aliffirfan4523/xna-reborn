# Harvests public GitHub repos that look like XNA projects, with license info.
# Needs: gh CLI logged in. Output: xna-repos.csv next to this script.
# GitHub search returns at most 1000 results per query, so queries are split by topic and by year.
param([int]$Skip = 0)   # resume: skip the first N queries and reload the CSV
$ErrorActionPreference = 'Stop'
$out = Join-Path $PSScriptRoot 'xna-repos.csv'
$seen = @{}
$rows = New-Object System.Collections.Generic.List[object]
if ($Skip -gt 0 -and (Test-Path $out)) {
    foreach ($r in (Import-Csv $out)) { $seen[$r.repo] = $true; $rows.Add($r) }
}

function Invoke-Search([string]$q) {
    for ($page = 1; $page -le 10; $page++) {
        $json = $null
        for ($try = 0; $try -lt 4 -and -not $json; $try++) {
            $json = gh api -X GET search/repositories -f "q=$q" -f per_page=100 -f "page=$page" 2>$null
            if (-not $json) { Start-Sleep -Seconds 65 }
        }
        if (-not $json) { Write-Host "  failed page $page for: $q"; return }
        $res = $json | ConvertFrom-Json
        foreach ($r in $res.items) {
            if ($seen.ContainsKey($r.full_name)) { continue }
            $seen[$r.full_name] = $true
            $lic = if ($r.license) { $r.license.spdx_id } else { 'NONE' }
            $rows.Add([pscustomobject]@{
                repo     = $r.full_name
                url      = $r.html_url
                license  = $lic
                language = $r.language
                stars    = $r.stargazers_count
                pushed   = if ($r.pushed_at) { $r.pushed_at.Substring(0, 10) } else { '' }
                size_kb  = $r.size
                archived = $r.archived
                topics   = ($r.topics -join ' ')
                desc     = ($r.description -replace '[\r\n,"]', ' ')
            })
        }
        Write-Host ("  {0} page {1}: +{2} (total {3})" -f $q, $page, $res.items.Count, $rows.Count)
        if ($res.items.Count -lt 100 -or $page * 100 -ge $res.total_count) { return }
        Start-Sleep -Milliseconds 2200
    }
}

$queries = @(
    'topic:xna fork:false',
    'topic:xna-game-studio fork:false',
    'topic:xna-framework fork:false',
    'topic:xna4 fork:false',
    'topic:xna-game fork:false',
    'topic:xbox-live-indie-games fork:false',
    'topic:xblig fork:false',
    'topic:windows-phone-7 xna fork:false',
    'topic:zune xna fork:false'
)
# Split the wide text queries by year of creation to get past the 1000 cap.
foreach ($y in 2007..2026) {
    $queries += "xna game in:name,description,readme language:C# created:$y-01-01..$y-12-31 fork:false"
    $queries += "xna game studio in:name,description,readme created:$y-01-01..$y-12-31 fork:false"
    $queries += "xbox 360 xna in:name,description,readme created:$y-01-01..$y-12-31 fork:false"
}

foreach ($q in ($queries | Select-Object -Skip $Skip)) {
    Write-Host "QUERY: $q"
    Invoke-Search $q
    $rows | Export-Csv -Path $out -NoTypeInformation -Encoding UTF8
    Start-Sleep -Milliseconds 2200
}
Write-Host "DONE: $($rows.Count) repos -> $out"
