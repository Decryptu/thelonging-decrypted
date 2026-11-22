param(
    [Parameter(Mandatory)][string]$Type,
    [ValidateSet('Assembly-CSharp.dll', 'Assembly-CSharp-firstpass.dll')]
    [string]$Assembly = 'Assembly-CSharp.dll',
    [string]$GameDir = $(if ($env:LONGING_GAME_DIR) { $env:LONGING_GAME_DIR } else { 'C:\Program Files (x86)\Steam\steamapps\common\The Longing' }),
    [string]$IlspyPath = $(if ($env:ILSPYCMD_PATH) { $env:ILSPYCMD_PATH } else { 'C:\Users\gurva\Documents\GitHub\spamgg\tools\local\ilspycmd.exe' })
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (!(Test-Path $IlspyPath)) {
    throw "ilspycmd not found at '$IlspyPath'. Set ILSPYCMD_PATH or install ICSharpCode.Decompiler CLI."
}
$dll = Join-Path $GameDir "The Longing_Data/Managed/$Assembly"
if (!(Test-Path $dll)) {
    throw "Game assembly not found at '$dll'. Set LONGING_GAME_DIR to your Steam install."
}
# Decompiled output stays local and is ignored by Git.
$out = Join-Path $repo 'local/code'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$dest = Join-Path $out ($Type + '.cs')
& $IlspyPath --disable-updatecheck -t $Type $dll | Set-Content -Encoding utf8 $dest
if ($LASTEXITCODE) { throw "Decompilation failed for $Type in $Assembly" }
Write-Host "Wrote $dest"
