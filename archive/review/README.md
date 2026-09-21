# Girls' Creation 공개 검수 아카이브 Viewer v0.3

기존 걸크리 CDN과 같은 `main` Pages 배포 안에서 `/archive/review/` 경로로 제공되는 공개 검수 아카이브다.

## JSON 전환 완료
- R17: 694행 / 신규 58 / 기존 재확인 7
- R18: 653행 / 신규 57 / 기존 재확인 1
- R35: 839행 / proposal 47

각 라운드는 `data/Rxx.json`을 직접 렌더링하고, JSON 로딩 실패 시 `legacy/Rxx.html`로 fallback한다.

R17/R18은 구형 상세 HTML의 `article.row` DOM을 구조화 JSON으로 변환했으며, 행 수·제안 수·제목 수·JP/KO/판정 누락 검증을 통과한 것만 게시한다.

이 아카이브는 공개 탐색/공유용이며 authoritative translation checkpoint를 대체하지 않는다.
