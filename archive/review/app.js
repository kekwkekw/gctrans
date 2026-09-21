const S={manifest:null,rounds:[],active:null,json:null,loadSeq:0};
const q=document.getElementById('q'),list=document.getElementById('list'),viewer=document.getElementById('viewer'),jsonView=document.getElementById('jsonView'),title=document.getElementById('title'),kind=document.getElementById('kind'),openBtn=document.getElementById('open'),dataBtn=document.getElementById('dataLink');
const jq=document.getElementById('jq'),jtype=document.getElementById('jtype'),jchanged=document.getElementById('jchanged'),jtbody=document.getElementById('jtbody'),jshown=document.getElementById('jshown'),jmeta=document.getElementById('jmeta'),jclear=document.getElementById('jclear');
const esc=x=>String(x??'');

function setMode(mode){
  const structured=mode==='json'||mode==='special';
  viewer.hidden=structured;
  jsonView.hidden=mode!=='json';
  specialView.hidden=mode!=='special';
  dataBtn.hidden=!structured;
  document.body.dataset.mode=mode;
}
function setKind(r,extra=''){
  kind.textContent=extra||r.kind;
  kind.className='status'+(r.detail_level==='summary'?' summary':'');
}
function selectRound(r,push=true){
  S.active=r.id;T=null;
  configureSpecialLinks(r);
  const seq=++S.loadSeq;
  title.textContent=`${r.id} · ${r.title}`;
  openBtn.href=r.fallback_path||r.source_path;
  openBtn.textContent=isStructuredPayload(r)?'원본 HTML':'새 창';
  dataBtn.href=payloadPath(r);
  dataBtn.textContent=r.payload_type==='embedded_reference'?'R3 JSON · 참조 원본':'JSON';
  for(const b of list.querySelectorAll('.item')) b.classList.toggle('active',b.dataset.id===r.id);
  if(push){const u=new URL(location.href);u.searchParams.set('r',r.id);history.replaceState(null,'',u)}
  if(r.payload_type==='review_json') loadJsonRound(r,seq);
  else if(Object.hasOwn(specialLoaders,r.payload_type)) specialLoaders[r.payload_type](r,seq);
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
    const k=document.createElement('small');k.textContent=r.kind+(r.payload_type==='embedded_reference'?' · embedded':isStructuredPayload(r)?' · JSON':'');k.className=r.detail_level==='full'?'kind-full':r.detail_level==='summary'?'kind-summary':'';b.appendChild(k);
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
    document.getElementById('jsonCount').textContent=S.rounds.filter(isStructuredPayload).length+' structured entries';
    renderList();
    const want=new URL(location.href).searchParams.get('r');
    const initial=S.rounds.find(r=>r.id===want)||S.rounds.find(r=>r.id==='R35')||S.rounds[0];
    if(initial)selectRound(initial,false);
  }catch(e){
    document.getElementById('error').hidden=false;viewer.hidden=true;jsonView.hidden=true;specialView.hidden=true;kind.textContent='LOAD ERROR';kind.className='status summary';title.textContent='manifest 로딩 실패';
  }
}
q.addEventListener('input',renderList);
for(const el of [jq,jtype,jchanged])el.addEventListener(el===jq?'input':'change',renderJson);
jclear.addEventListener('click',()=>{jq.value='';jtype.value='';jchanged.checked=false;renderJson()});
// Special archive payloads keep their original components and provenance.
const STRUCTURED_TYPES=new Set(['review_json','alignment_json','ledger_json','bundle_json','embedded_reference']);
const specialView=document.getElementById('specialView'),componentLinks=document.getElementById('componentLinks');
const sq=document.getElementById('sq'),sc=document.getElementById('scomponent'),sf=document.getElementById('scategory');
const sshown=document.getElementById('sshown'),snote=document.getElementById('snote'),srecords=document.getElementById('srecords'),sdocuments=document.getElementById('sdocuments');
let T=null;
const isStructuredPayload=r=>STRUCTURED_TYPES.has(r.payload_type);
const payloadPath=r=>r.payload_type==='embedded_reference'?r.embedded_source_path:r.source_path;
const outcomeLabels={prior_accept:'기존 수정안 채택 권고',prior_keep:'기존 번역 유지 / 대안만 기록',prior_refine:'수정안·근거 보완',prior_note_split:'역주 분리 권고',new_correction:'새 교정 제안',new_note_split:'새 역주 분리 제안'};
const contextLabels={terms:'term · 용어',profiles:'profile · 말투',relationships:'relationship · 관계',guards:'guard · 사실화 주의'};
function element(tag,value='',cls=''){
  const n=document.createElement(tag);if(value!==null&&value!==undefined)n.textContent=String(value);if(cls)n.className=cls;return n;
}
function requirePayload(ok){if(!ok)throw new Error('Unsupported or incomplete structured payload')}
function hasTextFields(r,keys){return r&&keys.every(k=>typeof r[k]==='string')}
function validateAlignment(p){
  requirePayload(p.schema==='gc-review-alignment-v0.9'&&p.round==='R1'&&Array.isArray(p.rows)&&Array.isArray(p.tables));
  requirePayload(p.rows.every(r=>hasTextFields(r,['novel_id','jp','ko','speaker_jp','speaker_ko'])&&Number.isInteger(r.message_index)&&r.message_index>0&&r.source_locator&&r.rationale===null&&r.review_status==='alignment_only'&&Array.isArray(r.proposals)&&r.proposals.length===0&&r.source_content));
}
function validateLedger(p){
  requirePayload(p.schema==='gc-review-ledger-v0.9'&&p.round==='R3'&&Array.isArray(p.components?.boundary_reviews)&&Array.isArray(p.components?.proposal_records));
  requirePayload(p.components.boundary_reviews.every(r=>hasTextFields(r,['novel_id','jp_sequence','judgment'])&&Array.isArray(r.grouping_before)&&Array.isArray(r.grouping_after)&&Array.isArray(r.ko_blocks)));
  const records=p.components.proposal_records;
  requirePayload(new Set(records.map(r=>r.id)).size===records.length);
  requirePayload(records.every(r=>hasTextFields(r,['id','lineage_round','novel_id','category','jp','before','after','note'])&&r.before.length>0&&(r.jp.length>0||r.id==='R2-W001')&&(r.after.length>0||r.id==='R1-L06')&&!Object.hasOwn(r,'raw_index')&&r.original_locator&&r.later_resolution&&r.applied===false&&r.source_content));
}
function validateBundle(p){
  requirePayload(p.schema==='gc-review-bundle-v0.9'&&p.round==='R4'&&Array.isArray(p.components?.main?.episodes)&&Array.isArray(p.components?.review?.records)&&p.components?.context);
  const validRow=r=>hasTextFields(r,['novel_id','jp','ko'])&&Number.isInteger(r.raw_index)&&Array.isArray(r.proposal_links)&&(r.rationale===null||typeof r.rationale==='string');
  requirePayload(p.components.main.episodes.every(e=>validRow(e.title)&&e.title.is_title===true&&e.title.raw_index===0&&Array.isArray(e.body_rows)&&e.body_rows.every(r=>validRow(r)&&r.is_title===false&&r.raw_index>0)));
  requirePayload(p.components.review.records.every(r=>hasTextFields(r,['id','novel_id','before','after','judgment'])&&Object.hasOwn(outcomeLabels,r.review_outcome)&&['new','prior'].includes(r.kind)&&Number.isInteger(r.raw_index)&&r.source_location&&r.applied===false&&r.source_content));
  for(const key of Object.keys(contextLabels))requirePayload(Array.isArray(p.components.context[key])&&p.components.context[key].every(r=>r.source_fields&&r.source_content&&r.scope==='MAIN_CHAPTER_1_ONLY'&&r.global_rule_approved===false));
}
function validateEmbedded(p,r){
  validateLedger(p);
  requirePayload(r.embedded_in==='R3'&&r.embedded_record_id==='R2-W001');
  const found=p.components.proposal_records.filter(x=>x.id===r.embedded_record_id);
  requirePayload(found.length===1&&found[0].lineage_round==='R2');
  return found[0];
}
function configureSpecialLinks(r){
  componentLinks.replaceChildren();componentLinks.hidden=!r.fallback_paths;
  for(const [name,path] of Object.entries(r.fallback_paths||{})){
    const a=element('a',name+' HTML');a.href=path;a.target='_blank';a.rel='noopener';componentLinks.appendChild(a);
  }
}
function specialFailure(r,seq){
  if(seq!==S.loadSeq)return;
  T=null;setKind(r,'JSON 실패 · HTML fallback');setMode('html');viewer.src=r.fallback_path;
}
async function fetchSpecial(r,seq,validate,render){
  setMode('special');setKind(r,'JSON 로딩 중');S.json=null;T=null;srecords.replaceChildren();sdocuments.replaceChildren();sshown.textContent='로딩 중';snote.textContent='';
  try{
    const response=await fetch(payloadPath(r),{cache:'no-store'});if(!response.ok)throw new Error('HTTP '+response.status);
    const payload=await response.json();if(seq!==S.loadSeq)return;
    requirePayload(payload.authoritative===false&&payload.runtime_verified===false&&payload.privacy==='public-sanitized');
    validate(payload,r);render(payload,r);setKind(r,r.kind+' · JSON');
  }catch(e){specialFailure(r,seq)}
}
function loadAlignment(r,seq){return fetchSpecial(r,seq,validateAlignment,renderAlignment)}
function loadLedger(r,seq){return fetchSpecial(r,seq,validateLedger,renderLedger)}
function loadBundle(r,seq){return fetchSpecial(r,seq,validateBundle,renderBundle)}
function loadEmbedded(r,seq){return fetchSpecial(r,seq,validateEmbedded,renderEmbedded)}
const specialLoaders={alignment_json:loadAlignment,ledger_json:loadLedger,bundle_json:loadBundle,embedded_reference:loadEmbedded};
function searchable(value){
  if(value===null||value===undefined)return '';
  if(typeof value!=='object')return String(value);
  if(Array.isArray(value))return value.map(searchable).join(' ');
  if(value.tag)return searchable(value.children);
  if(value.record_ref)return '';
  return Object.values(value).map(searchable).join(' ');
}
function item(type,record,extra={}){
  const locator=type==='alignment'?`message${record.message_index} message ${record.message_index}`:type==='main'||type==='review'?`raw${record.raw_index} raw ${record.raw_index}`:type==='boundary'?`run${record.run_index} run ${record.run_index}`:'';
  return {type,record,...extra,hay:(searchable([record,extra])+' '+locator).toLowerCase()};
}
function startSpecial(payload,entry,groups,note){
  T={payload,entry,groups};sq.value='';sf.value='';sc.replaceChildren();
  for(const group of groups){const o=element('option',group.label+' · '+group.items.length);o.value=group.id;sc.appendChild(o)}
  sc.hidden=groups.length===1;sc.setAttribute('aria-label','검수 component');snote.textContent=note;renderSpecial();
}
function renderAlignment(p,r){startSpecial(p,r,[{id:'alignment',label:'ALIGNMENT',items:p.rows.map(x=>item('alignment',x)),document:p.source_document}], '메시지 alignment · 행별 의미 판정과 제안 없음. 표에 표시된 message index와 원래 locator를 보존합니다.')}
function renderLedger(p,r){startSpecial(p,r,[{id:'proposals',label:'PROPOSALS',items:p.components.proposal_records.map(x=>item('ledger',x)),document:p.source_document},{id:'boundary',label:'BOUNDARY',items:p.components.boundary_reviews.map(x=>item('boundary',x)),document:p.source_document}], 'R3 역사적 ledger · 모든 제안 미적용. R4에서 확인한 raw 위치는 후대 출처로 따로 표시합니다.')}
function renderEmbedded(p,r){const record=validateEmbedded(p,r);startSpecial(p,r,[{id:'embedded',label:'R2 EMBEDDED',items:[item('ledger',record)]}], 'R3 embedded · R3 내부 기록 · 독립 R02 source 없음. R2-W001 한 건만 표시하며 빈 JP는 원본 상태입니다.')}
function renderBundle(p,r){
  const c=p.components;
  const main=c.main.episodes.flatMap(e=>[e.title,...e.body_rows].map(row=>item('main',row,{episode:e.source_label,episode_description:e.episode_review_description,title_warnings:row.is_title?e.title_warnings:[]})));
  const contexts=Object.keys(contextLabels).flatMap(category=>c.context[category].map(record=>item('context',record,{category})));
  startSpecial(p,r,[{id:'main',label:'MAIN',items:main,document:c.main.source_document},{id:'review',label:'REVIEW',items:c.review.records.map(x=>item('review',x)),document:c.review.source_document},{id:'context',label:'CONTEXT',items:contexts,document:c.context.source_document}], 'R4 checkpoint · MAIN / REVIEW / CONTEXT를 구분합니다. 채택은 수정 권고이며 실제 적용 승인이 아닙니다. CONTEXT는 메인 1장 한정 초안입니다. 원본 HTML의 과거 파일명 링크는 위의 세 원본 링크로 열 수 있습니다.');
}
function addField(parent,label,value,blankLabel='원본 칸 비어 있음'){
  const field=element('div','','specialField');field.appendChild(element('dt',label));
  const shown=value===null||value===undefined?'해당 원문 필드 없음':value===''?blankLabel:typeof value==='object'?JSON.stringify(value,null,2):String(value);
  field.appendChild(element('dd',shown));parent.appendChild(field);return field;
}
function addPair(parent,r){
  const pair=element('dl','','specialPair');addField(pair,'JP'+(r.speaker_jp?' · '+r.speaker_jp:''),r.jp);addField(pair,'KO'+(r.speaker_ko?' · '+r.speaker_ko:''),r.ko);parent.appendChild(pair);
}
function lazyDisclosure(parent,label,build){
  const d=element('details','','sourceDisclosure');d.appendChild(element('summary',label));let built=false;
  d.addEventListener('toggle',()=>{if(d.open&&!built){built=true;build(d)}});parent.appendChild(d);return d;
}
const sourceLinks={'REVIEW_R4.html':'./legacy/R04_REVIEW.html','MAIN01_REVIEW.html':'./legacy/R04_MAIN.html','CONTEXT_DRAFT.html':'./legacy/R04_CONTEXT.html'};
const sourceTags={b:'strong',strong:'strong',i:'em',em:'em',p:'p',div:'div',pre:'pre',code:'code',br:'br',small:'small',details:'details',summary:'summary',blockquote:'blockquote',h1:'h3',h2:'h4',h3:'h5',ul:'ul',ol:'ol',li:'li',th:'strong',td:'div',tr:'div',table:'div',thead:'div',tbody:'div'};
function sourceNode(value,payload,depth=0){
  if(depth>60)return element('span','원문 구조 깊이 제한');
  if(typeof value==='string')return document.createTextNode(value);
  if(!value||typeof value!=='object')return document.createTextNode('');
  if(value.record_ref){
    const keys=value.record_ref.split('/');requirePayload(keys.every(k=>k!=='__proto__'&&k!=='constructor'&&k!=='prototype'));
    let record=payload;for(const key of keys){requirePayload(record&&Object.hasOwn(record,key));record=record[key]}
    requirePayload(record.source_content);return sourceNode(record.source_content,payload,depth+1);
  }
  if(['script','style','iframe','object','embed'].includes(value.tag))return document.createTextNode('');
  const n=element(sourceTags[value.tag]||'div');
  if(value.tag==='a'){
    const href=value.attributes?.href;
    if(Object.hasOwn(sourceLinks,href)){const a=element('a');a.href=sourceLinks[href];a.target='_blank';a.rel='noopener';for(const c of value.children||[])a.appendChild(sourceNode(c,payload,depth+1));return a}
    n.title='원문 링크: '+(href||'');
  }
  for(const c of value.children||[])n.appendChild(sourceNode(c,payload,depth+1));
  return n;
}
function addSource(parent,record){
  if(!record.source_content)return;
  const payload=T.payload;lazyDisclosure(parent,'원문 표시 내용 전체',d=>d.appendChild(sourceNode(record.source_content,payload)));
}
function detailFields(parent,label,fields){lazyDisclosure(parent,label,d=>{const dl=element('dl');for(const [k,v] of fields)addField(dl,k,v);d.appendChild(dl)})}
function proposalCard(card,r,isReview){
  card.appendChild(element('h3',r.id+' · '+r.novel_id+(isReview?' / raw '+r.raw_index:'')));
  card.dataset.recordId=r.id;
  card.appendChild(element('p',isReview?`${r.kind} · ${r.review_outcome} · ${outcomeLabels[r.review_outcome]}`:`lineage ${r.lineage_round} · ${r.category}`,'recordBadge'));
  const dl=element('dl','','specialPair');
  if(!isReview)addField(dl,'JP',r.jp);
  addField(dl,'원문 보존 · before',r.before);addField(dl,'미적용 제안 / 대안 · after',r.after);card.appendChild(dl);
  const note=element('dl');addField(note,isReview?'판단':'제안 설명',isReview?r.judgment:r.note);card.appendChild(note);
  if(isReview){
    detailFields(card,'출처 위치 · 원문 · 인접 문맥',[['source location',r.source_location],['표시 label',r.source_label],['관찰 JP',r.observed.jp],['관찰 KO',r.observed.ko],...r.context_quotes.map(q=>[q.source_label,q.jp+'\n'+q.ko]),...r.warnings.map(w=>['주의',w])]);
  }else{
    const loc=element('dl','','specialPair');addField(loc,'R03 original locator',r.original_locator);addField(loc,'R04 later resolution',r.later_resolution);card.appendChild(loc);
  }
  addSource(card,r);
}
function renderSpecialCard(entry){
  const r=entry.record,card=element('article','','specialRecord');
  if(entry.type==='ledger'||entry.type==='review'){proposalCard(card,r,entry.type==='review');return card}
  if(entry.type==='alignment'){
    card.dataset.recordId=r.novel_id+':message:'+r.message_index;
    card.appendChild(element('h3',r.novel_id+' · message '+r.message_index));addPair(card,r);
    const dl=element('dl','','locator');addField(dl,'손번역 locator',r.source_locator.source_label);addField(dl,'원래 message group',r.original_message_group);card.appendChild(dl);addSource(card,r);
  }else if(entry.type==='boundary'){
    card.dataset.recordId=r.novel_id+':run:'+r.run_index;card.appendChild(element('h3',r.source_label));
    card.appendChild(element('p',r.grouping_label,'recordBadge'));const dl=element('dl');addField(dl,'경계 판정',r.judgment);addField(dl,'JP sequence',r.jp_sequence);for(const b of r.ko_blocks)addField(dl,b.block_id,b.ko);card.appendChild(dl);addSource(card,r);
  }else if(entry.type==='main'){
    card.dataset.recordId=r.novel_id+':'+r.raw_index;card.appendChild(element('h3',r.novel_id+' / raw '+r.raw_index+' · '+(r.is_title?'제목':'본문')));addPair(card,r);
    const dl=element('dl');addField(dl,'행별 판정',r.rationale,r.rationale===null?'행별 판정 없음':'원본 칸 비어 있음');
    if(r.rationale===null)dl.lastChild.lastChild.textContent='행별 판정 없음';
    for(const link of r.proposal_links){addField(dl,'proposal linkage',link.id+' · '+link.provenance.file);if(link.link_basis)addField(dl,'연결 출처',link.link_basis)}
    card.appendChild(dl);
    if(r.is_title)detailFields(card,'화 단위 설명 · 제목 출처',[['화',entry.episode],['화 단위 검수 설명',entry.episode_description],...entry.title_warnings.map(w=>['제목 warning',w]),['제목 위치',r.original_locator]]);
    addSource(card,r);
  }else if(entry.type==='context'){
    card.dataset.category=entry.category;card.appendChild(element('h3',r.source_label));card.appendChild(element('p',contextLabels[entry.category]+' · MAIN_CHAPTER_1_ONLY · 전역 승인 아님','recordBadge'));
    const dl=element('dl');for(const [k,v] of Object.entries(r.source_fields))addField(dl,k,v);card.appendChild(dl);
    lazyDisclosure(card,'근거 인용 · '+r.evidence.length,d=>{for(const q of r.evidence){const b=element('blockquote');b.appendChild(element('p',q.source_label));addPair(b,q);d.appendChild(b)}});addSource(card,r);
  }
  return card;
}
function renderSpecial(resetCategory=false){
  if(!T)return;
  const group=T.groups.find(g=>g.id===sc.value)||T.groups[0];
  const options=group.id==='context'?Object.entries(contextLabels):group.id==='review'?Object.entries(outcomeLabels):[];
  if(resetCategory||sf.dataset.component!==group.id){sf.replaceChildren();const all=element('option','전체 범주');all.value='';sf.appendChild(all);for(const [key,label] of options){const o=element('option',key+' · '+label);o.value=key;sf.appendChild(o)}sf.dataset.component=group.id}
  sf.hidden=!options.length;
  const needle=sq.value.trim().toLowerCase();const rows=group.items.filter(x=>(!needle||x.hay.includes(needle))&&(!sf.value||(group.id==='context'?x.category:x.record.review_outcome)===sf.value));
  sshown.textContent=`표시 ${rows.length} / ${group.items.length}`;
  srecords.replaceChildren();const fragment=document.createDocumentFragment();for(const row of rows)fragment.appendChild(renderSpecialCard(row));srecords.appendChild(fragment);
  if(!rows.length)srecords.appendChild(element('p','검색 결과 없음','empty'));
  sdocuments.replaceChildren();
  if(group.document){const payload=T.payload;lazyDisclosure(sdocuments,'문서 전체 원문 · '+group.document.title,d=>d.appendChild(sourceNode(group.document.content,payload)))}
  specialView.querySelector('.specialScroll').scrollTop=0;
}
sq.addEventListener('input',()=>renderSpecial());sf.addEventListener('change',()=>renderSpecial());
sc.addEventListener('change',()=>{sq.value='';renderSpecial(true)});
document.getElementById('sclear').addEventListener('click',()=>{sq.value='';sf.value='';renderSpecial()});

boot();