# 수락 Master 문법의 실제 후보 DLL CPU 검사

`fixtures/accepted_master_fields.json`은 수락 dictionary의 101개 field에서 exact JP/KO 한 쌍씩을 사용한 합성 record다. 현재 PC 원본 record나 native proof가 아니다. 숫자·다른 언어·pipe 이후 suffix·무관 field 보존과 table opt-in/out을 실제 후보의 기존 `MasterDataTranslator`로 검사한다.

저장소 루트에서:

```powershell
dotnet build tests/ComprehensiveMasterTests/ComprehensiveMasterTests.csproj -c Release
dotnet tests/ComprehensiveMasterTests/bin/Release/net9.0/ComprehensiveMasterTests.dll translations/ko tests/ComprehensiveMasterTests/fixtures/accepted_master_fields.json master-cpu.json
```

검사 assembly는 `candidate/GCMod.dll`을 직접 참조한다. 기존 `PackageLoader` → `LocalPackageTranslationStore` → `TranslationSession` → `MasterDataTranslator`와 다섯 MasterRef를 사용하며 Native Initialize·게임·운영 store를 호출하지 않는다. report의 실제 candidate SHA를 함께 확인한다. Novel 본문 read 수는 별도로 계측한다. 합성 record PASS는 62 table 전체의 게임 표시 PASS가 아니다.
