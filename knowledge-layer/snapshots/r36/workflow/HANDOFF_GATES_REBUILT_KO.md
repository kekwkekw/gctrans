# 전달 게이트

WORK_COMPLETE, PACKAGE_BUILT, VALIDATION_PASS, PACKAGING_PASS, HANDOFF_VERIFIED를 분리한다. 처음 네 단계가 성공해도 전달 확인을 대체하지 않는다.

파일명은 재생성판/재검수판임을 명시하고, sealed 파일은 이후 덮어쓰지 않는다. ZIP은 임시파일에서 완성한 후 원자적 rename, 고정 메타데이터·경로순서로 만든다. 외부 SHA는 최종 bytes를 대상으로 계산한다.

Files 목록에서 생성 ZIP의 ID를 실제로 찾고 같은 bytes를 다시 읽어 해시를 대조할 수 있을 때만 HANDOFF_VERIFIED로 기록한다. 현재 도구가 답변 전 등록을 지원하지 않거나 목록에 나타나지 않으면 PENDING으로 남긴다. sandbox 링크/작업경로 존재는 전달 확인 증거가 아니다. 다음 배치는 새로 확보한 ZIP의 SHA/CRC/PROVENANCE와 검증기를 확인한 뒤 시작한다.

이전 장애의 정확한 플랫폼 내부 원인과 발생 시점은 확인되지 않았다. 일시 저장·등록 타이밍·크기 제한은 가능한 원인이지만 단정하지 않는다. 동일 파일명 재사용 방지와 새 ZIP/구ZIP의 SHA 구분은 이 확인 여부와 무관하게 적용한다.
