# Builds the readable lists from xna-repos-classified.csv.
# Output: LIST-permissive.md, LIST-copyleft.md, LIST-no-license.md, LIST-other.md
$all = Import-Csv (Join-Path $PSScriptRoot 'xna-repos-classified.csv')
$cand = $all | Where-Object { [int]$_.tier -le 2 -and $_.language -eq 'C#' -and ($_.kind -eq 'game' -or $_.kind -eq 'unknown') }

function Write-List([string]$file, [string]$title, [string]$note, $items) {
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("# $title")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine($note)
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("Count: $(@($items).Count). Sorted by stars. Generated from GitHub search metadata only; nothing was cloned or built.")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine('| # | Repo | License | Stars | Last push | Tier | Description | Result |')
    [void]$sb.AppendLine('|---|------|---------|-------|-----------|------|-------------|--------|')
    $i = 0
    foreach ($r in ($items | Sort-Object { [int]$_.stars } -Descending)) {
        $i++
        $d = $r.desc
        if ($d.Length -gt 90) { $d = $d.Substring(0, 90) + '...' }
        $d = $d -replace '\|', '/'
        [void]$sb.AppendLine("| $i | [$($r.repo)]($($r.url)) | $($r.license) | $($r.stars) | $($r.pushed) | $($r.tier) | $d | |")
    }
    Set-Content -Path (Join-Path $PSScriptRoot $file) -Value $sb.ToString() -Encoding UTF8
}

Write-List 'LIST-permissive.md' 'XNA test candidates: permissive license' 'MIT, BSD, Apache, zlib, MS-PL and similar. Safest to use as samples with attribution.' ($cand | Where-Object { $_.license_class -eq 'permissive' })
Write-List 'LIST-copyleft.md' 'XNA test candidates: copyleft license' 'GPL, LGPL, MPL and similar. Fine to test locally. Do not bundle without checking the terms.' ($cand | Where-Object { $_.license_class -eq 'copyleft' })
Write-List 'LIST-no-license.md' 'XNA test candidates: no license' 'No license file means all rights reserved. Clone and test locally only. Never bundle or redistribute.' ($cand | Where-Object { $_.license_class -like 'none*' })
Write-List 'LIST-other.md' 'XNA test candidates: other license' 'License is not in the permissive or copyleft groups (for example CC licenses or custom terms). Read it first.' ($cand | Where-Object { $_.license_class -eq 'other' })

'candidates: ' + @($cand).Count
$cand | Group-Object license_class | ForEach-Object { '{0}: {1}' -f $_.Name, $_.Count }
