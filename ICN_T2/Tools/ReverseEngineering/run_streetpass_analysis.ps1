param(
    [string]$AssetRoot = "C:\Users\home\Desktop\ICN_T2\Yw2Asset",
    [string]$OutputDir = "md/04_Tech_Task/reports",
    [string]$SaveB = "",
    [string]$SaveC = "",
    [string]$SaveD = ""
)

$scriptPath = Join-Path $PSScriptRoot "analyze_yw2_streetpass.py"
if (-not (Test-Path $scriptPath)) {
    throw "Analyzer script not found: $scriptPath"
}

$args = @(
    $scriptPath,
    "--asset-root", $AssetRoot,
    "--output-dir", $OutputDir
)

if ($SaveB) { $args += @("--save-b", $SaveB) }
if ($SaveC) { $args += @("--save-c", $SaveC) }
if ($SaveD) { $args += @("--save-d", $SaveD) }

python @args
