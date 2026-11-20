param(
    [string]$Game = 'C:\Apps\steam\steamapps\common\Probably Stolen'
)

$ErrorActionPreference = 'Stop'
$managed = Join-Path $Game 'Probably Stolen_Data\Managed'
$stage = Join-Path $PSScriptRoot 'dist\ProbablyStolenHellMode'
$sdkLine = (& dotnet --list-sdks | Select-Object -Last 1)
if ($sdkLine -notmatch '^([^ ]+) \[(.+)\]$') { throw 'Could not find the .NET SDK.' }
$compiler = Join-Path (Join-Path $Matches[2] $Matches[1]) 'Roslyn\bincore\csc.dll'
$assemblyNames = @('mscorlib', 'System', 'System.Core', 'netstandard', 'Assembly-CSharp', '0Harmony',
    'UnityEngine', 'UnityEngine.CoreModule', 'UnityEngine.UIModule', 'UnityEngine.UI', 'Unity.TextMeshPro', 'Unity.Localization')
$references = $assemblyNames | ForEach-Object { '/r:' + (Join-Path $managed ($_ + '.dll')) }
$sources = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName
New-Item -ItemType Directory -Path $stage -Force | Out-Null
$required = Join-Path $stage 'ProbablyStolenHellMode.dll'
& dotnet $compiler /nologo /nostdlib /target:library /optimize+ /deterministic+ "/out:$required" @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Could not compile Hell Mode.' }
foreach ($file in 'manifest.xml', 'README.md') {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $stage -Force
}
Write-Output $stage
