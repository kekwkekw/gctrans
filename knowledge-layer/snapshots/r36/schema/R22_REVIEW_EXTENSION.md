# R22 검수 부가 필드

기존 schema v1 / lineage 1.0.1과 status enum은 바꾸지 않는다.
`audience_refs`는 실제 확정 단일 청자가 아니라 `audience_resolution`과 함께 읽는 문맥 한정 청자 후보일 수 있다. `heard_by_target=null`은 청취 확정을 하지 않았다는 뜻이다. private_self_monologue의 referenced_person_not_addressee는 생각의 대상이며 청자가 아니다.

보류는 workflow/coverage에만 있으며 새 status enum이 아니다. source/observed는 불변이고 제안만 별도 누적한다. 제목 proposal은 title_rows_reviewed.csv의 arca_title_ko 문자열 안 Unicode offset을 사용한다.
