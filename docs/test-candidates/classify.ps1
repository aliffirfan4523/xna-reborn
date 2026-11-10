# Adds tier, kind and license class to xna-repos.csv. Output: xna-repos-classified.csv
# tier 1 = XNA/XBLIG/Zune topic on the repo; tier 2 = "xna" in name or description; tier 3 = only found through README text.
# kind is a guess from the name, description and topics. It is not verified.
$permissive = 'MIT','Apache-2.0','BSD-2-Clause','BSD-3-Clause','Zlib','Unlicense','CC0-1.0','MS-PL','ISC','0BSD','BSL-1.0','WTFPL'
$copyleft = 'GPL-2.0','GPL-3.0','LGPL-2.1','LGPL-3.0','AGPL-3.0','MPL-2.0','MS-RL','EPL-2.0'
$topicHit = 'xna','xna-game-studio','xna-framework','xna4','xna-game','xbox-live-indie-games','xblig','zune'
$tutorialRx = 'tutorial|learn|lesson|course|sample|example|book'
$toolRx = 'engine|framework|library|toolkit|tool|editor|pipeline|extension|wrapper|api|importer'
$gameRx = 'game|shooter|platformer|puzzle|rpg|arcade|clone|tetris|pong|snake|roguelike|racing|tower defense|invaders|asteroids|breakout'

$rows = Import-Csv (Join-Path $PSScriptRoot 'xna-repos.csv')
$out = foreach ($r in $rows) {
    $text = ($r.repo + ' ' + $r.desc + ' ' + $r.topics).ToLower()
    $tops = $r.topics -split ' '
    if ($tops | Where-Object { $topicHit -contains $_ }) { $tier = 1 }
    elseif ($text -match 'xna') { $tier = 2 }
    else { $tier = 3 }
    if ($text -match $tutorialRx) { $kind = 'tutorial/sample' }
    elseif ($text -match $toolRx) { $kind = 'engine/tool' }
    elseif ($text -match $gameRx) { $kind = 'game' }
    else { $kind = 'unknown' }
    if ($permissive -contains $r.license) { $lc = 'permissive' }
    elseif ($copyleft -contains $r.license) { $lc = 'copyleft' }
    elseif ($r.license -eq 'NONE') { $lc = 'none (all rights reserved)' }
    else { $lc = 'other' }
    [pscustomobject]@{
        tier = $tier; kind = $kind; license_class = $lc; license = $r.license
        repo = $r.repo; url = $r.url; stars = [int]$r.stars; pushed = $r.pushed
        language = $r.language; archived = $r.archived; size_kb = $r.size_kb
        topics = $r.topics; desc = $r.desc
    }
}
$out | Sort-Object tier, @{ e = 'stars'; Descending = $true } | Export-Csv (Join-Path $PSScriptRoot 'xna-repos-classified.csv') -NoTypeInformation -Encoding UTF8

'total: ' + $out.Count
'--- by tier'; $out | Group-Object tier | ForEach-Object { '{0}: {1}' -f $_.Name, $_.Count }
$t12 = $out | Where-Object { $_.tier -le 2 }
'--- tier 1+2 by kind'; $t12 | Group-Object kind | ForEach-Object { '{0}: {1}' -f $_.Name, $_.Count }
'--- tier 1+2 games by license class'; $t12 | Where-Object { $_.kind -eq 'game' } | Group-Object license_class | ForEach-Object { '{0}: {1}' -f $_.Name, $_.Count }
'--- tier 1+2 by language (top 6)'; $t12 | Group-Object language | Sort-Object Count -Descending | Select-Object -First 6 | ForEach-Object { '{0}: {1}' -f $_.Name, $_.Count }
