using System.Text.Json;
using GCMod.Patches;
var ui=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(args[0]))!;
if (args.Length == 4 && args[1] == "--probe")
{
    var texts=JsonSerializer.Deserialize<string[]>(File.ReadAllText(args[2]))!;
    var rows=texts.Distinct().Select(jp => new { jp, matched=UiTextLookup.TryTranslate(ui,jp,out var ko), ko }).ToArray();
    File.WriteAllText(args[3],JsonSerializer.Serialize(new {
        schema="gc-ui-lookup-probe-v1",
        ui_sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(args[0]))).ToLowerInvariant(),
        rows },new JsonSerializerOptions { WriteIndented=true }));
    Console.WriteLine($"PROBED: {rows.Length} labels; {rows.Count(r=>r.matched)} matched");
    return;
}
var cases=new Dictionary<string,string> {
 ["炎"]="염", ["雷"]="번개", ["月"]="월", ["日"]="일", ["第9章"]="제9장", ["第10章"]="제10장", ["第11章"]="제11장",
 ["メインクエスト"]="메인 퀘스트",
 ["  メインクエスト\n"]="  메인 퀘스트\n",
 ["<size=20><color=red>スキップ</color></size>"]="<size=20><color=red>건너뛰기</color></size>",
 ["シーズン27"]="시즌27",
 ["現在：1周目フロア4"]="현재: 1회차 4층",
 ["現在： 2周目フロア10"]="현재: 2회차 10층",
 ["1箱目"]="1번째 박스",["ボックス：1箱目"]="박스: 1번째",
 ["1回 創作完了"]="1회 창작 완료",["3回創作完了"]="3회 창작 완료",
 ["1回  創作完了"]="1회 창작 완료",
 ["<color=#99928a>好物：</color>イヴェール地区"]="<color=#99928a>선호 선물:</color>이베르 지구",
 ["回開封"]="회 개봉", ["クエスト"]="퀘스트",
 ["1回開封"]="1회 개봉",["10回開封"]="10회 개봉",["あと7日"]="7일 남음",
 ["2026/11/01 03:59 まで"]="2026/11/01 03:59까지",
 ["2026/11/06 12:59まで"]="2026/11/06 12:59까지",
 ["完成まで"]="완성까지",["好物：イヴェール地区"]="선호 선물: 이베르 지구",
 ["あと87"]="87 남음",["あと1:59:59"]="1:59:59 남음",["3ターン"]="3턴",
 ["포토피 22個で解放"]="포토피 22개로 해제",
 ["덴푸라 22個で解放"]="덴푸라 22개로 해제",
 ["당신의 부탁 の閲覧が必要"]="당신의 부탁 열람 필요",
 ["하얗게 흩날리는 솔직한 마음 の閲覧が必要"]="하얗게 흩날리는 솔직한 마음 열람 필요",
 ["<color=#99928a>好物：</color>ヴォルカン地区"]="<color=#99928a>선호 선물:</color>볼칸 지구",
 ["アテネスレコード"]="아테네스 레코드",["装備中"]="장착 중",["イマージュを選択してください"]="이마쥬를 선택해 주세요",
 ["チュートリアル"]="튜토리얼",
 ["すれ違いバフ"]="스쳐가기 버프",
 ["マップレベルアップ"]="맵 레벨 상승",
 ["ログインボーナス確認"]="로그인 보너스 확인",
 ["1日目"]="1일차", ["60日目"]="60일차", ["365日目"]="365일차",
 ["リセット：11/01 04:00"]="초기화: 11/01 04:00",
 ["次回リセット：2026/11/01 04:00"]="다음 초기화: 2026/11/01 04:00",
 ["<color=red>次回リセット：2027/02/01 04:00</color>"]="<color=red>다음 초기화: 2027/02/01 04:00</color>",
 ["1/月"]="1/월", ["15/月"]="15/월", ["/月"]="/월",
 ["フロア"]="<size=80%>FLOOR</size>", ["フロアマップ"]="층 지도", ["次のフロアへ"]="다음 층으로",
 ["イマージュの獲得・限界突破により以下のマップレベルがアップしました"]="이마쥬 획득·한계 돌파로 아래 맵의 레벨이 상승했습니다",
};
foreach(var c in cases)if(!UiTextLookup.TryTranslate(ui,c.Key,out var ko)||ko!=c.Value)throw new Exception("Lookup mismatch: "+c.Key+" -> "+ko);
var negative=new[]{"更新まで08:47","開封期限：2026/11/08 12:59 まで","文章2026/11/01 03:59まで","シーズン1234","現在：1周目フロア4の話","シーズン27.5","あと7日以内",
 "1日目の話", "10000日目", "本文 次回リセット：2026/11/01 04:00", "リセット：2026/11/01 04:00",
 "次回リセット：2026/11/01 04:00 まで", "次回リセット：2026/11/01", "15/月の話", "満月", "FLOOR", "FLOOR 4",
 "日本語 22個で解放","日本語 の閲覧が必要","제목 の閲覧が必要という説明","덴푸라 22個で解放する","あと1000000000","3ターン終了","덴푸라 <b>22</b>個で解放"};
foreach(var jp in negative)
 if(UiTextLookup.TryTranslate(ui,jp,out _))throw new Exception("Unexpected translation: "+jp);
foreach(var jp in new[]{"第1000章", "第10章の話", "前第10章", "第十章", "あ"})
 if(UiTextLookup.TryTranslate(ui,jp,out _))throw new Exception("Unexpected chapter/glyph translation: "+jp);
var glossary = GlossaryIndex.Sort(new[]{"아테네스","산소비노","관장","공방","미술관"}, x=>x).ToArray();
var sections = GlossaryIndex.Sections(glossary).ToArray();
if (!glossary.SequenceEqual(new[]{"공방","관장","미술관","산소비노","아테네스"})
    || !sections.SequenceEqual(new[]{("가",0),("마",2),("사",3),("아",4)}))
 throw new Exception("Glossary ordering/index target mismatch");
foreach(var c in ui)if(!UiTextLookup.TryTranslate(ui,c.Key,out var ko)||ko!=c.Value)throw new Exception("Exact regression: "+c.Key);
Console.WriteLine($"PASS: {ui.Count} exact pairs; {cases.Count} wrappers/numeric/date labels; {negative.Length} negative/HOLD boundaries");
