# 보존 소스 출처 및 라이선스

현행 한국어 런타임은 기존 GCMod 구현과 gctrans의 확장을 포함한다. 런타임 DLL은 `source/GCMod.Candidate/`에서 빌드한다.

이 저장소의 자체 코드·도구·스크립트 및 독자 작성 번역 데이터는 **AGPL-3.0-only**로 제공한다([LICENSE](../LICENSE)). 게임 원문·고유 명칭 및 가공 UI 이미지의 원작 권리는 각 게임 권리자에게 있으며, 이 저장소의 오픈소스 라이선스로 재허가하지 않는다.

기존 GCMod source custody의 MIT License (Copyright 2024 Jitsu)는 [GCMod-upstream-LICENSE](../THIRD_PARTY_LICENSES/GCMod-upstream-LICENSE)에 원본 bytes 그대로 보존한다. 현행 빌드의 게임 참조 설정은 [GCMod.Candidate.csproj](../source/GCMod.Candidate/GCMod.Candidate.csproj)에 있다. 원본 게임·Utility·interop DLL과 font는 외부 입력이며 공개 소스에 포함하지 않는다.

과거 recovery wrapper와 구현·검수 기록은 Git history에서 확인한다. 정리 전 기준 commit은 `72f30ddc11ba3d522b3a9a6b4411e9caf065106f`다. 현행 운영은 [OPERATOR_KO.md](OPERATOR_KO.md), 외부 빌드 입력은 [EXTERNAL_DEPENDENCIES_KO.md](EXTERNAL_DEPENDENCIES_KO.md)를 따른다.
