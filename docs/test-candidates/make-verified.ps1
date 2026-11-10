# Builds VERIFIED.md from xna-verified.csv, xna-repos-classified.csv and analyzer-summary.jsonl.
$v = Import-Csv (Join-Path $PSScriptRoot 'xna-verified.csv')
$c = Import-Csv (Join-Path $PSScriptRoot 'xna-repos-classified.csv')
$map = @{}; foreach ($r in $c) { $map[$r.repo] = $r }
$an = @{}
$sumPath = Join-Path $PSScriptRoot 'analyzer-summary.jsonl'
if (Test-Path $sumPath) { foreach ($l in (Get-Content $sumPath)) { if ($l.Trim()) { $o = $l | ConvertFrom-Json; $an[$o.repo] = $o } } }

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('# Verified: code mentions Microsoft.Xna.Framework')
[void]$sb.AppendLine()
[void]$sb.AppendLine('GitHub code search found the text `Microsoft.Xna.Framework` in these repos (default branch).')
[void]$sb.AppendLine('MonoGame and FNA keep the same namespace, so a hit does NOT prove the project is old XNA.')
[void]$sb.AppendLine('The last column shows the XNA Reborn analyzer on a shallow clone. "not XNA" means its detector found no XNA project.')
[void]$sb.AppendLine()
foreach ($cls in 'permissive', 'copyleft') {
    $items = $v | Where-Object { [int]$_.hits -gt 0 -and $map[$_.repo].license_class -eq $cls } | Sort-Object { -[int]$map[$_.repo].stars }
    [void]$sb.AppendLine("## $cls license ($(@($items).Count) repos)")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine('| Repo | License | Stars | Code hits | Analyzer | Description |')
    [void]$sb.AppendLine('|------|---------|-------|-----------|----------|-------------|')
    foreach ($i in $items) {
        $m = $map[$i.repo]; $name = $i.repo.Split('/')[1]
        $a = ''
        if ($an.ContainsKey($name)) {
            $o = $an[$name]
            if ($o.noProject) { $a = 'not XNA' } else { $a = "score $($o.score) -> $($o.projected), $($o.findings) findings" }
        }
        $d = $m.desc; if ($d.Length -gt 70) { $d = $d.Substring(0, 70) + '...' }; $d = $d -replace '\|', '/'
        [void]$sb.AppendLine("| [$($i.repo)]($($m.url)) | $($m.license) | $($m.stars) | $($i.hits) | $a | $d |")
    }
    [void]$sb.AppendLine()
}
$zero = $v | Where-Object { [int]$_.hits -eq 0 }
[void]$sb.AppendLine("## No hit ($(@($zero).Count) repos)")
[void]$sb.AppendLine()
[void]$sb.AppendLine('These permissive and copyleft candidates have no `Microsoft.Xna.Framework` text in the indexed code. See `xna-verified.csv`. Code search skips large files and may miss some.')
Set-Content -Path (Join-Path $PSScriptRoot 'VERIFIED.md') -Value $sb.ToString() -Encoding UTF8
