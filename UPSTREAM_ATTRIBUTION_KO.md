# 보존 소스 출처

기존 GCMod 및 회수된 scoped UI 구현의 source/lifecycle/계약을 이어 사용한다. local base는 kekwkekw/gctrans의 조사 시점 main이고, 차세대 safe source는 NG09 통합 운영 후보에서 선택했다. donor recovery와 attribution을 크기 때문에 제거하지 않는다.

기존 GCMod source custody의 MIT License (Copyright 2024 Jitsu)를 donor_recovery/LICENSE에 그대로 보존했다. source/GCMod.Candidate/Legacy와 공통 donor_recovery/reproduction/Common.props는 기존 parser/session 및 read-only game reference build를 위한 실제 source다. 원본 게임 DLL과 font는 이 source의 일부로 반환하지 않는다.
