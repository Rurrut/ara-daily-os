// Loaded as part of mobile.js; data never carries executable instructions.
function notionDraftKey(){return 'ara.diary.'+A.date}
async function notionCall(input){return request('/api/notion','POST',{date:A.date,...input})}
function notionSheet(){
 showSheet('노션 · AI 하루 정리',root=>{
  const state=document.createElement('p');state.className='form-note';state.textContent='연결 상태 확인 중';root.append(state);
  const [queryLabel,query]=input('페이지 검색','text');root.append(queryLabel);
  const list=document.createElement('div');list.className='category-grid';
  const [urlLabel,url]=input('노션 링크 또는 ID','text');const kind=document.createElement('select');kind.className='form-input';kind.setAttribute('aria-label','노션 위치 종류');for(const [value,label] of [['page','페이지'],['data_source','데이터 원본'],['database','데이터베이스']]){const option=document.createElement('option');option.value=value;option.textContent=label;kind.append(option)}
  const preview=document.createElement('pre');preview.className='notion-preview';preview.textContent='기상일 '+A.date+' 기준으로 저장합니다. 실제 기록·집중·만족도·메모·과제가 포함됩니다.';
  const draft=document.createElement('textarea');draft.className='form-input';draft.rows=7;draft.maxLength=30000;draft.setAttribute('aria-label','확인하고 수정할 일기 초안');draft.value=saved(notionDraftKey(),'');draft.oninput=()=>persist(notionDraftKey(),draft.value);
  let working=false,config=null;const buttons=[];
  const run=async action=>{if(working)return;working=true;buttons.forEach(b=>b.disabled=true);try{await action()}catch(e){toast(e.message);state.textContent=e.message}finally{working=false;buttons.forEach(b=>b.disabled=false)}};
  const add=(text,action)=>{const b=button(text,()=>run(action),'wide secondary');buttons.push(b);root.append(b);return b};
  const showStatus=value=>{config=value;state.textContent=(value.connected?'노션 연결됨':'PC 앱의 노션 · AI에서 먼저 연결해 주세요')+'\n저장: '+(value.target?.title||'미지정')+'\n참고: '+(value.reference?.title||'미지정')+'\nAI: '+(value.model?'PC 로컬 · '+value.model:'로컬 모델 미설정 · 일기 직접 입력 가능')};
  add('페이지 검색',async()=>{const result=await notionCall({op:'search',text:query.value});list.replaceChildren();for(const item of result.results){const b=button(item.title+' · '+(item.kind==='page'?'페이지':'데이터 원본'),()=>{url.value=item.id;kind.value=item.kind;toast('위치를 골랐습니다. 저장 위치 또는 참고 위치로 지정하세요')},'');list.append(b)}if(!result.results.length)list.textContent='결과 없음 · 노션에서 ARA 연결에 접근을 허용했는지 확인하세요'});root.append(list,urlLabel,kind);
  add('이 위치에 하루 기록 저장',async()=>showStatus(await notionCall({op:'target',id:url.value,kind:kind.value})));
  add('이 위치를 AI 참고 자료로',async()=>showStatus(await notionCall({op:'reference',id:url.value,kind:kind.value})));
  add('참고 자료 불러오기',async()=>{if(!config?.reference)throw Error('참고 위치를 먼저 선택해 주세요');const result=await notionCall({op:'read',id:config.reference.id,kind:config.reference.kind});preview.textContent=result.text});
  add('내보낼 기록 미리보기',async()=>{preview.textContent=(await notionCall({op:'preview'})).markdown});root.append(preview);
  const label=document.createElement('label');label.className='form-label';label.textContent='일기 · AI 초안은 확인하고 수정하세요';root.append(label,draft);
  add('PC 로컬 AI로 초안 작성',async()=>{const result=await notionCall({op:'draft'});draft.value=result.draft;persist(notionDraftKey(),draft.value);toast('초안을 확인한 뒤 노션에 저장하세요')});
  add('기록과 일기를 노션에 저장',async()=>{const result=await notionCall({op:'publish',text:draft.value});state.textContent='저장 완료 · '+result.date+' 기상일\n'+result.url});
  add('저장한 일기 불러오기',async()=>{const result=await notionCall({op:'daily'});preview.textContent=result.page.text;draft.value=result.draft;persist(notionDraftKey(),draft.value);toast('노션 내용을 불러왔습니다. 실제 행동 기록은 유지됩니다')});
  run(async()=>showStatus(await notionCall({op:'status'})));
 })
}
function updateSheet(){showSheet('앱 업데이트',root=>{
 const note=document.createElement('p');note.className='form-note';root.append(note);
 const paint=()=>{if(!native?.updateStatus){note.textContent='Android 1.6을 한 번 설치하면 이후 PC 연결로 화면 업데이트를 받을 수 있습니다. APK 변경은 앱 안에서 설치를 요청합니다.';return}try{const s=JSON.parse(native.updateStatus());note.textContent='설치 버전 '+s.installedName+' · PC 버전 '+s.availableName+'\n'+(s.uiReady?'새 화면 준비됨 · 화면 다시 열기를 누르세요':s.busy?'업데이트 확인·다운로드 중':'화면 업데이트는 PC 연결 시 자동으로 준비됩니다')+(s.error?'\n'+s.error:'')}catch{note.textContent='업데이트 상태를 읽지 못했습니다'}};
 paint();root.append(button('PC에서 업데이트 확인',()=>{native?.checkUpdates?.();paint()},'wide secondary'),button('준비된 화면 다시 열기',()=>{if(A.busy||A.gesture||A.conflict){toast('입력 또는 충돌 확인을 먼저 마쳐 주세요');return}native?.reloadUi?.()},'wide secondary'),button('새 APK 설치',()=>{native?.installUpdate?.();paint()},'wide secondary'));
 const timer=setInterval(()=>{if($('sheet').hidden||$('sheetTitle').textContent!=='앱 업데이트'){clearInterval(timer);return}paint()},1000);
})}
