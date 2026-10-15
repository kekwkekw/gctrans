param(
    [Parameter(Mandatory=$true)][string]$GameDependencyRoot,
    [string]$Dotnet='dotnet',
    [string]$Python='python'
)
$ErrorActionPreference='Stop'
$packageRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$referenceRoot=[IO.Path]::GetFullPath($GameDependencyRoot)
if (-not (Test-Path -LiteralPath (Join-Path $referenceRoot 'BepInEx/interop/Assembly-CSharp.dll') -PathType Leaf)) { throw 'Existing read-only compile references required; game binaries are not bundled.' }
function Invoke-Checked([string]$Executable,[string[]]$Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Offline check failed: $Executable $($Arguments[0])" }
}
Push-Location -LiteralPath $packageRoot
try {
    # Fail early if the selected Python lacks the existing mock-HTTP dependency.
    Invoke-Checked $Python @('-X','utf8','-c','import httpx; print("Offline HTTP dependency: httpx " + httpx.__version__)')
    $runRoot=Join-Path $packageRoot ('tests/scratch-ng05-repro-'+[guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $runRoot | Out-Null
    Invoke-Checked $Python @('-X','utf8','tests/data_tools_tests.py')
    Invoke-Checked $Python @('-X','utf8','tests/ng04_master_tests.py')
    Invoke-Checked $Python @('-X','utf8','tests/ng03_apply_tests.py')
    Invoke-Checked $Python @('-X','utf8','tests/ng04_apply_tests.py','--skip-chain')
    Invoke-Checked $Python @('-X','utf8','tests/ng05_operator_tests.py')
    Invoke-Checked $Python @('-X','utf8','tests/ng05_chain.py')
    Invoke-Checked $Python @('-X','utf8','-m','unittest','discover','-s','tests','-p','test_ng03_source.py','-v')
    Invoke-Checked $Python @('-X','utf8','tests/test_ng04_source.py')
    Invoke-Checked $Python @('-X','utf8','tests/prepare_ng02_fixtures.py','translations/ko',(Join-Path $runRoot 'fixtures'),'--report','tests/NG05_FULL_CANDIDATE_JSON_FIXTURES.json')
    $projects=@(
        'source/GCMod.Candidate/GCMod.Candidate.csproj',
        'tests/Ng04CandidateTests/Ng04CandidateTests.csproj',
        'tests/Ng05OperationsTests/Ng05OperationsTests.csproj',
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
        if ($project -eq 'source/GCMod.Candidate/GCMod.Candidate.csproj') {
            New-Item -ItemType Directory -Path 'candidate' -Force | Out-Null
            Copy-Item -LiteralPath 'source/GCMod.Candidate/bin/Release/net6.0/GCMod.dll' -Destination 'candidate/GCMod.dll'
        }
    }
    Invoke-Checked $Dotnet @('tests/Ng05OperationsTests/bin/Release/net9.0/Ng05OperationsTests.dll','tests/NG05_CHAIN_FIXTURES.json','tools/UpdateCli/bin/Release/net9.0/UpdateCli.dll',(Join-Path $runRoot 'local-operations'),'tests/NG05_LOCAL_OPERATIONS_CPU.json')
    Invoke-Checked $Python @('-X','utf8','tools/update/cli.py','preflight','--candidate-root','.', '--dependency-root',$referenceRoot,'--report','audit/NG05_PREFLIGHT.json')
    $compileJson=& $Dotnet msbuild source/GCMod.Candidate/GCMod.Candidate.csproj -getItem:Compile "-p:GameDependencyRoot=$referenceRoot"
    if ($LASTEXITCODE -ne 0) { throw 'Compile item inventory failed' }
    $compileJson | Set-Content -LiteralPath 'audit/NG05_COMPILE_ITEMS.json' -Encoding utf8
    Invoke-Checked $Dotnet @('tools/CompileAudit/bin/Release/net9.0/CompileAudit.dll','audit/NG05_COMPILE_ITEMS.json','candidate/GCMod.dll','audit/NG05_COMPILE_AUDIT.json')
    Invoke-Checked $Dotnet @('tests/Ng04CandidateTests/bin/Release/net9.0/Ng04CandidateTests.dll','translations/ko','audit/NG05_ACTUAL_CANDIDATE_MASTER_CPU.json','tests/NG05_CHAIN_FIXTURES.json')
    Invoke-Checked $Dotnet @('tests/Ng03RuntimeTests/bin/Release/net9.0/Ng03RuntimeTests.dll','tests/NG05_RUNTIME_TEST_RESULTS.json')
    Invoke-Checked $Dotnet @('tests/Ng03SupplyTests/bin/Release/net9.0/Ng03SupplyTests.dll','tests/NG05_SUPPLY_CPU_RESULTS.json')
    Invoke-Checked $Dotnet @('tests/Ng03SemanticStoreTests/bin/Release/net9.0/Ng03SemanticStoreTests.dll','tests/NG03_SEMANTIC_FIXTURES.json','tests/NG05_SEMANTIC_STORE_RESULTS.json')
    Invoke-Checked $Dotnet @('tests/RuntimeTests/bin/Release/net9.0/RuntimeTests.dll','tests/NG05_PORTABLE_RUNTIME_REGRESSION_RESULTS.json','tests/shared-validator-cases.json')
    Invoke-Checked $Dotnet @('tests/HostTests/bin/Release/net9.0/HostTests.dll','tests/NG05_HOST_REGRESSION_RESULTS.json','translations/ko',(Join-Path $runRoot 'fixtures/full-ko-correction'))
    Invoke-Checked $Dotnet @('tests/SpecialTests/bin/Release/net9.0/SpecialTests.dll','tests/NG05_SPECIAL_REGRESSION_RESULTS.json','translations/ko')
    Invoke-Checked $Dotnet @('tests/CandidateCpuTests/bin/Release/net9.0/CandidateCpuTests.dll','tests/NG05_CANDIDATE_REGRESSION_RESULTS.json','tests/shared-validator-cases.json','translations/ko',(Join-Path $runRoot 'fixtures/full-ko-correction'),(Join-Path $runRoot 'fixtures/full-binding-addition'))
    Invoke-Checked $Dotnet @('tools/UpdateCli/bin/Release/net9.0/UpdateCli.dll','status','--store',(Join-Path $runRoot 'empty-store'))
    $reports=@('audit/NG05_ACTUAL_CANDIDATE_MASTER_CPU.json','audit/NG05_COMPILE_AUDIT.json','audit/NG04_MASTER_PRODUCT_TESTS.json','audit/NG04_SOURCE_CPU_EVIDENCE.json','tests/NG04_APPLY_TEST_RESULTS.json','tests/NG05_OPERATOR_TEST_RESULTS.json','tests/NG05_LOCAL_OPERATIONS_CPU.json','tests/NG03_APPLY_TEST_RESULTS.json','tests/NG05_RUNTIME_TEST_RESULTS.json','tests/NG05_SUPPLY_CPU_RESULTS.json','tests/NG05_SEMANTIC_STORE_RESULTS.json','tests/NG05_CANDIDATE_REGRESSION_RESULTS.json','tests/NG05_PORTABLE_RUNTIME_REGRESSION_RESULTS.json','tests/NG05_HOST_REGRESSION_RESULTS.json','tests/NG05_SPECIAL_REGRESSION_RESULTS.json','tests/DATA_TOOL_TEST_RESULTS.json')
    foreach ($report in $reports) { if ((Get-Content -LiteralPath $report -Raw | ConvertFrom-Json).status -ne 'PASS') { throw "Report not PASS: $report" } }
    [ordered]@{schema='gc-ng05-offline-reproduction-v1';status='PASS';candidate_sha256=(Get-FileHash -LiteralPath 'candidate/GCMod.dll' -Algorithm SHA256).Hash.ToLowerInvariant();reports=$reports;run_root=$runRoot;historical_activation_receipt_required=$false;native_hook_install='NOT_RUN';game='NOT_RUN';publication='NOT_PERFORMED';game_operations=0;install_operations=0} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath 'audit/NG05_OFFLINE_RUN.json' -Encoding utf8
    Write-Output 'PASS: offline product and actual candidate CPU; native/game NOT_RUN, installation/publication0.'
} finally { Pop-Location }
