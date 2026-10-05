'use strict';
(() => {
 const bridge=window.chrome?.webview,send=(action,extra={})=>bridge?.postMessage({action,...extra});
 const $=id=>document.getElementById(id);let state=null,mode='bookmarks',confirmClear=false,lastStamp='';
 const make=(tag,cls,text)=>{const el=document.createElement(tag);if(cls)el.className=cls;if(text!==undefined)el.textContent=text;return el;};
 const titles={bookmarks:'Bookmark',history:'Riwayat',downloads:'Unduhan',settings:'Pengaturan',about:'Tentang browser'};
 const empty=(title,body)=>{const el=make('div','empty');el.append(make('div','empty-symbol','◌'),make('strong','',title),make('span','',body));$('content').append(el);};
 const actionButton=(label,action,extra,cls='text-button')=>{const button=make('button',cls,label);button.addEventListener('click',()=>send(action,extra));return button;};
 const host=url=>{try{return new URL(url).hostname;}catch{return url||'';}};
 const card=(title,description)=>{const el=make('section','card');el.append(make('h2','',title));if(description)el.append(make('p','',description));return el;};
 const formatBytes=value=>{if(!Number.isFinite(value)||value<0)return '';return value>=1048576?(value/1048576).toFixed(1)+' MB':value>=1024?(value/1024).toFixed(0)+' KB':value+' B';};
 const render=()=>{
  if(!state)return;
  const filter=$('filter').value.toLowerCase();const content=$('content');content.replaceChildren();
  $('footer').textContent='';
  if(mode==='bookmarks'||mode==='history'){
   const all=state[mode]||[],items=all.filter(item=>(item.title+' '+item.url).toLowerCase().includes(filter));
   if(!items.length)empty(filter?'Tidak ada hasil.':mode==='bookmarks'?'Simpan yang penting.':'Perjalananmu dimulai di sini.',filter?'Coba kata pencarian lain.':mode==='bookmarks'?'Buka website lalu tekan Ctrl+D untuk menambahkan bookmark.':'Website yang kamu buka akan tampil di sini.');
   let lastDay='';
   for(const item of items){
    if(mode==='history'){
     const day=new Date(item.time).toLocaleDateString('id-ID',{day:'numeric',month:'long',year:'numeric'});
     if(day!==lastDay){content.append(make('div','group-date',day));lastDay=day;}
    }
    const row=make('div','item');row.append(make('span','item-badge',(item.title||host(item.url)||'?').slice(0,1).toUpperCase()));
    const button=make('button','item-main');button.append(make('span','item-title',item.title||host(item.url)),make('span','item-sub',host(item.url)));
    button.title=item.url;button.addEventListener('click',()=>send('navigate',{value:item.url}));row.append(button);
    if(mode==='bookmarks'){const remove=make('button','icon-button');remove.innerHTML=morientesIcon('close');remove.setAttribute('aria-label','Hapus bookmark '+item.title);remove.addEventListener('click',()=>send('removeBookmark',{id:item.id}));row.append(remove);}
    content.append(row);
   }
   $('footer').textContent=all.length+(mode==='bookmarks'?' bookmark':' kunjungan');
   if(mode==='history'&&all.length){
    if(confirmClear){const block=make('div','confirm','Hapus seluruh riwayat? Bookmark dan login website tetap tersimpan.');const actions=make('div','confirm-actions');const yes=actionButton('Hapus riwayat','clearHistory',{},'text-button danger');yes.addEventListener('click',()=>{confirmClear=false;});const no=make('button','text-button','Batal');no.addEventListener('click',()=>{confirmClear=false;render();});actions.append(no,yes);block.append(actions);content.append(block);}
    else {const clear=make('button','clear-button','Hapus semua riwayat');clear.addEventListener('click',()=>{confirmClear=true;render();});content.append(clear);}
   }
  }
  else if(mode==='downloads'){
   if(!state.downloads.length)empty('Belum ada unduhan.','Unduhan dari sesi browser ini akan tampil di sini.');
   for(const item of state.downloads){
    const box=make('div','download-card');box.append(make('div','download-title',item.name||'Unduhan'));
    const status=item.status==='Completed'?'Selesai':item.status==='Interrupted'?'Dihentikan':'Mengunduh';
    const meta=make('div','download-meta');meta.append(make('span','',status),make('span','',formatBytes(item.received)+(item.total>0?' / '+formatBytes(item.total):'')));box.append(meta);
    const progress=make('div','progress'),fill=make('div','progress-fill');fill.style.width=(item.total>0?Math.min(100,Math.round(100*item.received/item.total)):item.status==='Completed'?100:20)+'%';progress.append(fill);box.append(progress);
    const actions=make('div','download-actions');if(item.status==='Completed')actions.append(actionButton('Tampilkan di folder','showDownload',{id:item.id}));else if(item.status==='InProgress')actions.append(actionButton('Batalkan','cancelDownload',{id:item.id}));box.append(actions);content.append(box);
   }
  }
  else if(mode==='settings'){
   const search=card('Mesin pencarian','Pilih layanan pencarian untuk kolom alamat dan halaman awal.');const select=make('select');select.setAttribute('aria-label','Mesin pencarian');for(const [value,label]of [['google','Google'],['duckduckgo','DuckDuckGo'],['bing','Bing']]){const option=make('option','',label);option.value=value;select.append(option);}select.value=state.settings.engine;select.addEventListener('change',()=>send('setting',{id:'engine',value:select.value}));search.append(select);content.append(search);
   const zoom=card('Zoom halaman','Ukuran otomatis untuk semua website. Disimpan saat browser ditutup.');const zoomSelect=make('select');zoomSelect.setAttribute('aria-label','Zoom default');for(const percent of [25,50,67,75,80,90,100,110,125,150,175,200,250,300]){const option=make('option','',percent+'%');option.value=String(percent);zoomSelect.append(option);}zoomSelect.value=String(state.settings.defaultZoom||80);zoomSelect.addEventListener('change',()=>send('setting',{id:'defaultZoom',value:zoomSelect.value}));zoom.append(zoomSelect);content.append(zoom);
   const theme=card('Tampilan','Pilih suasana yang nyaman untuk kamu.');const choices=make('div','theme-choice');for(const [value,label]of [['light','☀  Terang'],['dark','☾  Gelap']])choices.append(actionButton(label,'setting',{id:'theme',value},state.settings.theme===value?'chosen':''));theme.append(choices);content.append(theme);
   const session=card('Saat browser dibuka');const label=make('label','checkbox-label'),input=make('input');input.type='checkbox';input.checked=state.settings.restoreSession;input.addEventListener('change',()=>send('setting',{id:'restoreSession',value:String(input.checked)}));const text=make('span','','Lanjutkan sesi sebelumnya');text.append(make('small','','Buka kembali tab terakhir. Tab latar dimuat ketika dipilih.'));label.append(input,text);session.append(label);content.append(session);
   const updates=card('Pembaruan browser',state.updates?.status||'Memeriksa versi dari GitHub Releases.');const autoLabel=make('label','checkbox-label'),auto=make('input');auto.type='checkbox';auto.checked=state.settings.autoUpdates!==false;auto.addEventListener('change',()=>send('setting',{id:'autoUpdates',value:String(auto.checked)}));autoLabel.append(auto,make('span','','Unduh pembaruan otomatis'));updates.append(autoLabel);const check=actionButton('Cek pembaruan','checkUpdates',{});check.disabled=!!state.updates?.busy;check.style.marginTop='12px';updates.append(check);content.append(updates);
   const privacy=card('Data di perangkatmu','Bookmark dan riwayat disimpan pada profil Windows kamu. Login website dikelola oleh WebView2.');privacy.append(actionButton('Lihat riwayat','sidebar',{value:'history'}));content.append(privacy);
  }
  else if(mode==='about'){
   const about=make('div','about'),logo=make('img');logo.src='mark.svg';logo.alt='Morientes';about.append(logo,make('h2','','Morientes'),make('p','version','Browser pribadi · v'+state.version));
   const details=card('Tentang aplikasi','Aplikasi browser Windows dengan tampilan ala macOS.');for(const [label,value]of [['Platform','Windows 10 / 11 · x64'],['Mesin web','Microsoft WebView2'],['Versi aplikasi',state.version+' · Preview']]){const row=make('div','detail-row');row.append(make('span','',label),make('span','',value));details.append(row);}about.append(details);
   const author=make('p','author','Initiator & Developer');author.append(document.createElement('br'),make('strong','','Ramzi Wijaya / El Morientes'));about.append(author);content.append(about);
  }
 };
 bridge?.addEventListener('message',event=>{
  const next=event.data;if(next?.type!=='state')return;
  const changed=mode!==next.sidebarMode;state=next;mode=next.sidebarMode;
  document.body.classList.toggle('dark',next.settings.theme==='dark');$('heading').textContent=titles[mode]||'Bookmark';
  $('filterWrap').hidden=!['bookmarks','history'].includes(mode);$('filter').placeholder=mode==='history'?'Cari di riwayat…':'Cari di bookmark…';
  document.querySelectorAll('[data-mode]').forEach(el=>el.classList.toggle('selected',el.dataset.mode===mode));
  if(changed){$('filter').value='';confirmClear=false;}
  const stamp=JSON.stringify([mode,next.settings,next[mode],next.version,next.updates]);
  if(changed||stamp!==lastStamp){lastStamp=stamp;render();}
 });
 $('filter').addEventListener('input',render);$('close').addEventListener('click',()=>send('sidebar',{value:'close'}));
 document.querySelectorAll('[data-mode]').forEach(el=>el.addEventListener('click',()=>send('sidebar',{value:el.dataset.mode})));
 send('ready');
})();
