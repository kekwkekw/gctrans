param(
    [Parameter(Mandatory=$true)][string]$GameDependencyRoot,
    [string]$Dotnet = 'dotnet',
    [string]$Python = 'python',
    [switch]$ResumeAfterPythonReview
)
$ErrorActionPreference = 'Stop'
$packageRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$referenceRoot = [System.IO.Path]::GetFullPath($GameDependencyRoot)
if (-not (Test-Path -LiteralPath (Join-Path $referenceRoot 'BepInEx/interop/Assembly-CSharp.dll') -PathType Leaf)) { throw 'Read-only existing compile references are required.' }
function Invoke-Checked([string]$Executable, [string[]]$Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Offline step failed: $Executable $($Arguments[0])" }
}
Push-Location -LiteralPath $packageRoot
try {
    $runRoot = Join-Path $packageRoot ('tests/scratch-ng03-repro-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $runRoot | Out-Null
    # Product Python tests also regenerate four disposable semantic fixtures for the actual candidate store reader.
    if ($ResumeAfterPythonReview) {
        foreach ($passedReport in @('tests/NG03_APPLY_TEST_RESULTS.json','tests/DATA_TOOL_TEST_RESULTS.json')) {
            $previous = Get-Content -LiteralPath $passedReport -Raw | ConvertFrom-Json
            if ($previous.status -ne 'PASS') { throw "No completed Python review evidence: $passedReport" }
        }
        if (-not (Test-Path -LiteralPath 'tests/NG03_SEMANTIC_FIXTURES.json')) { throw 'Semantic fixtures from the completed Python review run are required for this test-only resume.' }
    } else {
        Invoke-Checked $Python @('-X','utf8','tests/ng03_apply_tests.py')
        Invoke-Checked $Python @('-X','utf8','tests/data_tools_tests.py')
    }
    Invoke-Checked $Python @('-X','utf8','-m','unittest','discover','-s','tests','-p','test_ng03_source.py','-v')
    Invoke-Checked $Python @('-X','utf8','tests/prepare_ng02_fixtures.py','translations/ko',(Join-Path $runRoot 'fixtures'),'--report','tests/NG03_FULL_CANDIDATE_JSON_FIXTURES.json')
    $projects = @(
        'source/GCMod.Candidate/GCMod.Candidate.csproj',
        'tests/Ng03RuntimeTests/Ng03RuntimeTests.csproj',
        'tests/Ng03SupplyTests/Ng03SupplyTests.csproj',
        'tests/Ng03SemanticStoreTests/Ng03SemanticStoreTests.csproj',
        'tests/RuntimeTests/RuntimeTests.csproj',
        'tests/HostTests/HostTests.csproj',
        'tests/SpecialTests/SpecialTests.csproj',
        'tests/CandidateCpuTests/CandidateCpuTests.csproj',
        'tools/UpdateCli/UpdateCli.csproj',
        'tools/CompileAudit/CompileAudit.csproj'
    )
    foreach ($project in $projects) {
        Invoke-Checked $Dotnet @('restore',$project,'--configfile','NuGet.Config',"-p:GameDependencyRoot=$referenceRoot")
        Invoke-Checked $Dotnet @('build',$project,'--no-restore','-c','Release','-m:1','-p:UseSharedCompilation=false',"-p:GameDependencyRoot=$referenceRoot",'-v:minimal')
    }
    $compileJson = & $Dotnet msbuild source/GCMod.Candidate/GCMod.Candidate.csproj -getItem:Compile "-p:GameDependencyRoot=$referenceRoot"
    if ($LASTEXITCODE -ne 0) { throw 'Actual compile inventory inspection failed' }
    $compileJson | Set-Content -LiteralPath 'audit/NG03_FINAL_COMPILE_ITEMS.json' -Encoding utf8
    Invoke-Checked $Dotnet @('tools/CompileAudit/bin/Release/net9.0/CompileAudit.dll','audit/NG03_FINAL_COMPILE_ITEMS.json','source/GCMod.Candidate/bin/Release/net6.0/GCMod.dll','audit/NG03_COMPILE_AUDIT.json')
    Invoke-Checked $Dotnet @('tests/Ng03RuntimeTests/bin/Release/net9.0/Ng03RuntimeTests.dll','tests/NG03_RUNTIME_TEST_RESULTS.json')
    Invoke-Checked $Dotnet @('tests/Ng03SupplyTests/bin/Release/net9.0/Ng03SupplyTests.dll','tests/NG03_SUPPLY_CPU_RESULTS.json')
    Invoke-Checked $Dotnet @('tests/Ng03SemanticStoreTests/bin/Release/net9.0/Ng03SemanticStoreTests.dll','tests/NG03_SEMANTIC_FIXTURES.json','tests/NG03_SEMANTIC_STORE_RESULTS.json')
    Invoke-Checked $Dotnet @('tests/RuntimeTests/bin/Release/net9.0/RuntimeTests.dll','tests/NG03_PORTABLE_RUNTIME_REGRESSION_RESULTS.json','tests/shared-validator-cases.json')
    Invoke-Checked $Dotnet @('tests/HostTests/bin/Release/net9.0/HostTests.dll','tests/NG03_HOST_REGRESSION_RESULTS.json','translations/ko',(Join-Path $runRoot 'fixtures/full-ko-correction'))
    Invoke-Checked $Dotnet @('tests/SpecialTests/bin/Release/net9.0/SpecialTests.dll','tests/NG03_SPECIAL_REGRESSION_RESULTS.json','translations/ko')
    Invoke-Checked $Dotnet @('tests/CandidateCpuTests/bin/Release/net9.0/CandidateCpuTests.dll','tests/NG03_CANDIDATE_REGRESSION_RESULTS.json','tests/shared-validator-cases.json','translations/ko',(Join-Path $runRoot 'fixtures/full-ko-correction'),(Join-Path $runRoot 'fixtures/full-binding-addition'))
    Invoke-Checked $Python @('-X','utf8','tools/summarize_ng03_verification.py')
    $summary = [ordered]@{
        schema='gc-ng03-offline-reproduction-v1'; status='PASS'; candidate_sha256=(Get-FileHash -LiteralPath 'source/GCMod.Candidate/bin/Release/net6.0/GCMod.dll' -Algorithm SHA256).Hash.ToLowerInvariant();
        resumed_after_completed_python_review=[bool]$ResumeAfterPythonReview;
        fixture_scope='disposable CPU inputs, not semantic acceptance'; run_root=$runRoot;
        native_hook_install='NOT_RUN'; game_validation='NOT_RUN'; real_HTTP_supply='NOT_RUN'; publication='NOT_PERFORMED'; game_operations=0; installation_changes=0;
        reports=@('NG03_APPLY_TEST_RESULTS.json','DATA_TOOL_TEST_RESULTS.json','NG03_RUNTIME_TEST_RESULTS.json','NG03_SUPPLY_CPU_RESULTS.json','NG03_SEMANTIC_STORE_RESULTS.json','NG03_PORTABLE_RUNTIME_REGRESSION_RESULTS.json','NG03_HOST_REGRESSION_RESULTS.json','NG03_SPECIAL_REGRESSION_RESULTS.json','NG03_CANDIDATE_REGRESSION_RESULTS.json');
        compile_audit='audit/NG03_COMPILE_AUDIT.json'; packaging_excludes=@('scratch-*','fixtures-*','raw assets','original game DLLs','fonts','credentials')
    }
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath 'audit/NG03_OFFLINE_RUN.json' -Encoding utf8
    Write-Output 'PASS: actual candidate offline CPU/mock HTTP, portable parity, compile metadata. Native/game/publication NOT_RUN/NOT_PERFORMED.'
} finally { Pop-Location }
