# 보존 소스 출처

현행 한국어 런타임은 기존 GCMod 구현과 gctrans의 확장을 포함한다. 런타임 DLL은 `source/GCMod.Candidate/`에서 빌드한다.

기존 GCMod source custody의 MIT License (Copyright 2024 Jitsu)는 [GCMod-upstream-LICENSE](../THIRD_PARTY_LICENSES/GCMod-upstream-LICENSE)에 원본 bytes 그대로 보존한다. 현행 빌드의 게임 참조 설정은 [GCMod.Candidate.csproj](../source/GCMod.Candidate/GCMod.Candidate.csproj)에 있다. 원본 게임·Utility·interop DLL과 font는 외부 입력이며 공개 소스에 포함하지 않는다.

과거 recovery wrapper와 구현·검수 기록은 Git history에서 확인한다. 정리 전 기준 commit은 `72f30ddc11ba3d522b3a9a6b4411e9caf065106f`다. 현행 운영은 [OPERATOR_KO.md](OPERATOR_KO.md), 외부 빌드 입력은 [EXTERNAL_DEPENDENCIES_KO.md](EXTERNAL_DEPENDENCIES_KO.md)를 따른다.
