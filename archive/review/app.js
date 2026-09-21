const S={manifest:null,rounds:[],active:null,json:null,loadSeq:0};
const q=document.getElementById('q'),list=document.getElementById('list'),viewer=document.getElementById('viewer'),jsonView=document.getElementById('jsonView'),title=document.getElementById('title'),kind=document.getElementById('kind'),openBtn=document.getElementById('open'),dataBtn=document.getElementById('dataLink');
const jq=document.getElementById('jq'),jtype=document.getElementById('jtype'),jchanged=document.getElementById('jchanged'),jtbody=document.getElementById('jtbody'),jshown=document.getElementById('jshown'),jmeta=document.getElementById('jmeta'),jclear=document.getElementById('jclear');
const esc=x=>String(x??'');

function setMode(mode){
  const isJson=mode==='json';
  viewer.hidden=isJson;
  jsonView.hidden=!isJson;
  dataBtn.hidden=!isJson;
}
function setKind(r,extra=''){
  kind.textContent=extra||r.kind;
  kind.className='status'+(r.detail_level==='summary'?' summary':'');
}
function selectRound(r,push=true){
  S.active=r.id;
  const seq=++S.loadSeq;
  title.textContent=`${r.id} · ${r.title}`;
  openBtn.href=r.fallback_path||r.source_path;
  openBtn.textContent=r.payload_type==='review_json'?'원본 HTML':'새 창';
  dataBtn.href=r.source_path;
  for(const b of list.querySelectorAll('.item')) b.classList.toggle('active',b.dataset.id===r.id);
  if(push){const u=new URL(location.href);u.searchParams.set('r',r.id);history.replaceState(null,'',u)}
  if(r.payload_type==='review_json') loadJsonRound(r,seq);
  else {
    S.json=null; setMode('html'); setKind(r); viewer.src=r.source_path;
  }
}
function renderList(){
  const needle=q.value.trim().toLowerCase();
  const rows=S.rounds.filter(r=>!needle||r.searchable_text.toLowerCase().includes(needle));
  list.replaceChildren();
  for(const r of rows){
    const b=document.createElement('button');b.className='item';b.dataset.id=r.id;
    const rb=document.createElement('b');rb.textContent=r.id;b.appendChild(rb);
    const st=document.createElement('span');st.textContent=r.title;b.appendChild(st);
    const k=document.createElement('small');k.textContent=r.kind+(r.payload_type==='review_json'?' · JSON':'');k.className=r.detail_level==='full'?'kind-full':r.detail_level==='summary'?'kind-summary':'';b.appendChild(k);
    const s=document.createElement('small');s.textContent=[r.scope,r.status].filter(Boolean).join(' · ');b.appendChild(s);
    b.addEventListener('click',()=>selectRound(r));list.appendChild(b);
  }
  if(!rows.length){const x=document.createElement('div');x.className='emptyList';x.textContent='검색 결과 없음';list.appendChild(x)}
}
function proposalText(p){return [p.id,p.kind,p.field,p.before,p.after].filter(Boolean).join(' ')}
function rowHay(r){return [r.novel_id,'raw'+r.raw_index,r.jp,r.speaker_jp,r.speaker_ko,r.ko,r.rationale,...(r.proposals||[]).map(proposalText)].join(' ').toLowerCase()}
function jsonMatches(r){
  const needle=jq.value.trim().toLowerCase();
  if(needle&&!rowHay(r).includes(needle)) return false;
  if(jtype.value==='title'&&!r.is_title)return false;
  if(jtype.value==='body'&&r.is_title)return false;
  if(jtype.value==='speaker'&&!(r.proposals||[]).some(p=>p.field==='speaker_ko'))return false;
  if(jchanged.checked&&!(r.proposals||[]).length)return false;
  return true;
}
function addTd(tr,text,cls=''){
  const td=document.createElement('td');td.textContent=esc(text);if(cls)td.className=cls;tr.appendChild(td);return td;
}
function renderJson(){
  if(!S.json)return;
  const rows=(S.json.rows||[]).filter(jsonMatches);
  jshown.textContent=`표시 ${rows.length} / ${S.json.rows.length}`;
  jtbody.replaceChildren();
  for(const r of rows){
    const tr=document.createElement('tr');
    if(r.is_title)tr.classList.add('titleRow');
    if((r.proposals||[]).length)tr.classList.add('changed');
    addTd(tr,`${r.novel_id}\nraw${r.raw_index}`,'idcell');
    addTd(tr,r.is_title?'제목':'본문');
    addTd(tr,r.jp,'pre');
    addTd(tr,[r.speaker_jp,r.speaker_ko].filter(Boolean).join(' / '),'pre');
    addTd(tr,r.ko,'pre');
    const pc=document.createElement('td');
    if(!(r.proposals||[]).length){pc.textContent='—';pc.className='muted'}
    else for(const p of r.proposals){
      const box=document.createElement('div');box.className='proposal';
      const head=document.createElement('div');head.className='pid';head.textContent=[p.id,p.kind,p.field].filter(Boolean).join(' · ');box.appendChild(head);
      const body=document.createElement('div');body.textContent=(p.before||p.after)?`${p.before||''} → ${p.after||''}`:'기존 후보 재확인';box.appendChild(body);pc.appendChild(box);
    }
    tr.appendChild(pc);
    addTd(tr,r.rationale,'pre');
    jtbody.appendChild(tr);
  }
  if(!rows.length){const tr=document.createElement('tr');const td=document.createElement('td');td.colSpan=7;td.className='empty';td.textContent='검색 결과 없음';tr.appendChild(td);jtbody.appendChild(tr)}
}
function renderMeta(meta){
  const pairs=[['검수',meta.reviewed],['본문',meta.body],['제목',meta.titles],['신규 제안',meta.new_proposals],['교정',meta.corrections],['제목 후보',meta.title_candidates],['재확인',meta.reconfirmed],['high',meta.high],['medium',meta.medium],['남은 큐',meta.remaining_queue]].filter(([,v])=>v!==undefined&&v!==null);
  jmeta.replaceChildren();
  for(const [k,v] of pairs){const s=document.createElement('span');s.className='jsonPill';s.textContent=`${k} ${v??'—'}`;jmeta.appendChild(s)}
}
async function loadJsonRound(r,seq){
  setMode('json');setKind(r,'JSON 로딩 중');S.json=null;jtbody.replaceChildren();jshown.textContent='로딩 중';
  try{
    const res=await fetch(r.source_path,{cache:'no-store'});if(!res.ok)throw new Error('HTTP '+res.status);
    const payload=await res.json();if(seq!==S.loadSeq)return;
    if(!Array.isArray(payload.rows))throw new Error('rows missing');
    S.json=payload;renderMeta(payload.meta||{});setKind(r,r.kind+' · JSON');renderJson();
  }catch(e){
    if(seq!==S.loadSeq)return;
    setKind(r,'JSON 실패 · HTML fallback');setMode('html');viewer.src=r.fallback_path||r.source_path;
  }
}
async function boot(){
  try{
    const res=await fetch('./manifest.json',{cache:'no-store'});if(!res.ok)throw new Error('HTTP '+res.status);
    S.manifest=await res.json();S.rounds=S.manifest.rounds||[];
    document.getElementById('range').textContent=S.manifest.range+' · v'+S.manifest.version;
    document.getElementById('count').textContent=S.rounds.length+' rounds';
    document.getElementById('summaryCount').textContent=S.rounds.filter(r=>r.detail_level==='summary').length+' summary';
    document.getElementById('jsonCount').textContent=S.rounds.filter(r=>r.payload_type==='review_json').length+' JSON';
    renderList();
    const want=new URL(location.href).searchParams.get('r');
    const initial=S.rounds.find(r=>r.id===want)||S.rounds.find(r=>r.id==='R35')||S.rounds[0];
    if(initial)selectRound(initial,false);
  }catch(e){
    document.getElementById('error').hidden=false;viewer.hidden=true;jsonView.hidden=true;kind.textContent='LOAD ERROR';kind.className='status summary';title.textContent='manifest 로딩 실패';
  }
}
q.addEventListener('input',renderList);
for(const el of [jq,jtype,jchanged])el.addEventListener(el===jq?'input':'change',renderJson);
jclear.addEventListener('click',()=>{jq.value='';jtype.value='';jchanged.checked=false;renderJson()});
boot();