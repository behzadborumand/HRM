$ErrorActionPreference = 'Stop'
$dotnetRoot = Join-Path $env:USERPROFILE '.dotnet'
$dotnetExe = Join-Path $dotnetRoot 'dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetExe -PathType Leaf)) {
    throw "User-local .NET SDK not found at $dotnetExe"
}
$env:DOTNET_ROOT = $dotnetRoot
$env:DOTNET_ROOT_X64 = $dotnetRoot
$env:PATH = "$dotnetRoot;$env:PATH"
& $dotnetExe @args
exit $LASTEXITCODE
