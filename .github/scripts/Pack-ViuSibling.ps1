[CmdletBinding()]
param([Parameter(Mandatory)][string] $Ref)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$sibling = Join-Path (Split-Path $repository -Parent) 'viu'
if (Test-Path -LiteralPath $sibling) { throw "CI sibling destination already exists: $sibling" }
git clone --no-checkout --filter=blob:none https://github.com/assimalign/viu.git $sibling
Push-Location $sibling
try {
    git fetch --depth 1 origin $Ref
    git checkout --detach FETCH_HEAD
    & ./scripts/Install-Local.ps1 -Configuration Release -SkipCachePrune
    if ($LASTEXITCODE -ne 0) { throw "Viu installer failed: $LASTEXITCODE" }
}
finally { Pop-Location }
Write-Host "Viu sibling feed: $sibling/_out/packages (ref $Ref)"
