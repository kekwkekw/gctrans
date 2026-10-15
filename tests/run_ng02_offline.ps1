param(
    [Parameter(Mandatory=$true)][string]$GameDependencyRoot,
    [string]$Dotnet = 'dotnet',
    [string]$Python = 'python'
)
$ErrorActionPreference = 'Stop'
$packageRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $packageRoot
try {
    $runRoot = Join-Path $packageRoot ('tests/scratch-repro-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $runRoot | Out-Null
    & $Python -X utf8 tests/prepare_ng02_fixtures.py translations/ko (Join-Path $runRoot 'fixtures') --report (Join-Path $runRoot 'fixtures.json')
    if ($LASTEXITCODE) { throw 'Fixture preparation failed' }
    $projects = @('source/GCMod.Candidate/GCMod.Candidate.csproj', 'tests/RuntimeTests/RuntimeTests.csproj', 'tests/HostTests/HostTests.csproj', 'tests/SpecialTests/SpecialTests.csproj', 'tests/CandidateCpuTests/CandidateCpuTests.csproj')
    foreach ($project in $projects) {
        & $Dotnet restore $project --configfile NuGet.Config "-p:GameDependencyRoot=$GameDependencyRoot"
        if ($LASTEXITCODE) { throw "Restore failed: $project" }
        & $Dotnet build $project --no-restore -c Release -m:1 -p:UseSharedCompilation=false "-p:GameDependencyRoot=$GameDependencyRoot" -v:minimal
        if ($LASTEXITCODE) { throw "Build failed: $project" }
    }
    & $Dotnet tests/RuntimeTests/bin/Release/net9.0/RuntimeTests.dll (Join-Path $runRoot 'runtime.json') tests/shared-validator-cases.json
    if ($LASTEXITCODE) { throw 'Runtime tests failed' }
    & $Dotnet tests/HostTests/bin/Release/net9.0/HostTests.dll (Join-Path $runRoot 'host.json') translations/ko (Join-Path $runRoot 'fixtures/full-ko-correction')
    if ($LASTEXITCODE) { throw 'Host tests failed' }
    & $Dotnet tests/SpecialTests/bin/Release/net9.0/SpecialTests.dll (Join-Path $runRoot 'special.json') translations/ko
    if ($LASTEXITCODE) { throw 'Special tests failed' }
    & $Dotnet tests/CandidateCpuTests/bin/Release/net9.0/CandidateCpuTests.dll (Join-Path $runRoot 'candidate.json') tests/shared-validator-cases.json translations/ko (Join-Path $runRoot 'fixtures/full-ko-correction') (Join-Path $runRoot 'fixtures/full-binding-addition')
    if ($LASTEXITCODE) { throw 'Actual candidate CPU tests failed' }
    & $Python -X utf8 tests/data_tools_tests.py
    if ($LASTEXITCODE) { throw 'Data tools tests failed' }
    Write-Output "PASS: offline results in $runRoot. Native hooks and game behavior NOT_RUN."
} finally { Pop-Location }
