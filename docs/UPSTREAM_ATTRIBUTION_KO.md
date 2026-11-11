# 보존 소스 출처

기존 GCMod 및 회수된 scoped UI 구현의 source, lifecycle, 계약을 이어 사용한다. `source/GCMod.Candidate/Legacy/`와 `source/GCMod.Nextgen/`에 현재 빌드 소스를 유지한다.

현행 `source/GCMod.Candidate`에는 `kristina1Q/gc-mod-local@0cdcd9e84fffa438dfdac63176685a6c47a07318`에서 유래해 수정된 코드가 포함된다. 해당 소스의 MIT License (Copyright 2026 kristina1Q)는 [gc-mod-local-LICENSE.txt](../THIRD_PARTY_LICENSES/gc-mod-local-LICENSE.txt)에 보존하고 배포 패키지에도 포함한다. 이 고지는 코드 provenance를 위한 것이며, 현행 유지보수 기준이나 외부 upstream 추적 관계를 뜻하지 않는다.

기존 GCMod source custody의 MIT License (Copyright 2024 Jitsu)는 [GCMod-upstream-LICENSE](../THIRD_PARTY_LICENSES/GCMod-upstream-LICENSE)에 원본 bytes 그대로 보존한다. 읽기 전용 game reference 빌드 설정은 [Common.props](../source/build/Common.props)다. 원본 게임·Utility·interop DLL과 font는 외부 입력이며 공개 소스에 포함하지 않는다.

과거 recovery wrapper와 구현·검수 기록은 Git history에서 확인한다. 정리 전 기준 commit은 `72f30ddc11ba3d522b3a9a6b4411e9caf065106f`다. 현행 운영은 [OPERATOR_KO.md](OPERATOR_KO.md), 외부 빌드 입력은 [EXTERNAL_DEPENDENCIES_KO.md](EXTERNAL_DEPENDENCIES_KO.md)를 따른다.
