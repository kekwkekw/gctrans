# Geometry closeout 제품 검사

`dotnet build tests/GeometryCloseoutTests/GeometryCloseoutTests.csproj -c Release` 후 `dotnet tests/GeometryCloseoutTests/bin/Release/net9.0/GeometryCloseoutTests.dll translations/ko/ui/bindings.json`으로 실행한다.

실제 candidate library의 exact SelectAll/Shop-help 정책과 full quad 좌표 검사, 원래 preferred-axis helper, PE/IL의 실제 호출 연결을 검사한다. 관리형 geometry 입력과 boolean negative는 명시 mock이며 native proof가 아니다. 실제 패키지의 두 binding을 직접 읽는 case가 있으므로 synthetic role만 통과하고 제품 source kind가 달라 경로를 타지 않는 경우를 놓치지 않는다. Shop current-model을 static-asset으로 잘못 제한한 첫 구현은 이 case를 추가해 수정했다.

SelectAll은 Reset/Deselect/Mission과 구분된 실제 `_selectAllButton` field·직접 TMP/path/owner와 native driver를 검증한다. width 숫자나 비율을 새 정답으로 넣지 않는다. Shop help는 Footer ink를 줄이지 않고 현재 Exchange view/tab의 정확한 header row 안에서 모든 full rendered quads·문자·channel·clip·material·control overlap을 검사한다. unknown shader clip/stencil은 허용하지 않는다. 실제 native 정착과 재열기 근거는 별도 실행 보고를 따른다.

캐시된 Shop 탭 복귀는 기존 탭 callback→현재 page/model/tab 확인→기존 current-visible batch 재준비의 실제 PE/IL 연결을 검사한다. native gate·현재 선택 page와 실제 interop 필드 읽기, 기존 bounded batch 사용, native setter/producer 재호출 부재를 확인한다. 이것은 실제 화면의 재열기 정착을 대체하지 않는다.
