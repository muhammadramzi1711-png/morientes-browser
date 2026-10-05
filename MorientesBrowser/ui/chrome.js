'use strict';
(() => {
  const bridge=window.chrome?.webview;
  const send=(action,extra={})=>bridge?.postMessage({action,...extra});
  const $=id=>document.getElementById(id);
  let state=null, toastTimer;
  const address=$('address');
  const render=(next)=>{
    state=next;document.body.classList.toggle('dark',next.settings.theme==='dark');
    const current=next.tabs.find(t=>t.id===next.activeId);
    if(document.activeElement!==address) address.value=current?.url==='morientes:home'?'':(current?.url||'');
    $('back').disabled=!next.canBack;$('forward').disabled=!next.canForward;
    $('reload').innerHTML=morientesIcon(current?.loading?'stop':'reload');
    $('reload').title=current?.loading?'Hentikan pemuatan':'Muat ulang · Ctrl+R';
    $('loadLine').classList.toggle('visible',!!current?.loading);
    $('bookmark').classList.toggle('saved',next.bookmarked);
    $('bookmark').setAttribute('aria-label',next.bookmarked?'Hapus bookmark':'Tambahkan bookmark');
    $('protocol').innerHTML=morientesIcon(current?.url?.startsWith('https://')?'lock':'globe');
    $('protocol').title=current?.url?.startsWith('https://')?'Koneksi HTTPS':'Alamat website';
    document.querySelectorAll('[data-panel]').forEach(el=>el.classList.toggle('selected',next.sidebarOpen&&el.dataset.panel===next.sidebarMode));
    const tabList=$('tabs');
    // Keep keyed tab nodes so title/progress updates preserve focus and scroll.
    const existing=new Map([...tabList.children].map(el=>[el.dataset.id,el]));
    for(const tab of next.tabs){
      let node=existing.get(tab.id);
      if(!node){
        node=document.createElement('div');node.className='tab';node.dataset.id=tab.id;
        const choose=document.createElement('button');choose.className='tab-select';choose.setAttribute('role','tab');
        const badge=document.createElement('span');badge.className='tab-badge';badge.setAttribute('aria-hidden','true');
        const title=document.createElement('span');title.className='tab-title';
        choose.append(badge,title);choose.addEventListener('click',()=>send('activateTab',{id:tab.id}));
        const close=document.createElement('button');close.className='tab-close';close.innerHTML=morientesIcon('close');
        close.addEventListener('click',()=>send('closeTab',{id:tab.id}));
        node.addEventListener('auxclick',e=>{if(e.button===1){e.preventDefault();send('closeTab',{id:tab.id});}});
        node.append(choose,close);tabList.append(node);
      }
      const selected=tab.id===next.activeId;
      node.classList.toggle('active',selected);node.classList.toggle('loading',tab.loading);
      node.querySelector('.tab-select').setAttribute('aria-selected',String(selected));
      node.querySelector('.tab-select').setAttribute('aria-label',tab.title||'Tab Baru');
      node.querySelector('.tab-title').textContent=tab.title||'Tab Baru';
      node.querySelector('.tab-select').title=tab.title+'\n'+tab.url;
      node.querySelector('.tab-close').setAttribute('aria-label','Tutup '+tab.title);
      const badge=node.querySelector('.tab-badge');
      if(tab.url==='morientes:home'){if(!badge.querySelector('img')){const icon=document.createElement('img');icon.src='mark.svg';icon.alt='';badge.replaceChildren(icon);}}
      else badge.textContent=(tab.title||'?').slice(0,1).toUpperCase();
      existing.delete(tab.id);
    }
    existing.forEach(node=>node.remove());
    $('tabCounter').textContent=next.tabs.length>1?next.tabs.length+' tabs':'';
  };
  bridge?.addEventListener('message',event=>{
    const data=event.data;if(!data||typeof data!=='object')return;
    if(data.type==='state')render(data);
    if(data.type==='focusAddress'){address.focus();address.select();}
    if(data.type==='toast'){$('toast').textContent=data.text;$('toast').classList.add('visible');clearTimeout(toastTimer);toastTimer=setTimeout(()=>$('toast').classList.remove('visible'),3500);}
  });
  $('addressForm').addEventListener('submit',e=>{e.preventDefault();send('navigate',{value:address.value});address.blur();});
  address.addEventListener('focus',()=>address.select());
  address.addEventListener('keydown',e=>{if(e.key==='Escape'){address.blur();if(state)render(state);}});
  for(const action of ['back','forward','reload','home','bookmark']) $(action).addEventListener('click',()=>send(action));
  $('newTab').addEventListener('click',()=>send('newTab'));
  document.querySelectorAll('[data-panel]').forEach(el=>el.addEventListener('click',()=>send('sidebar',{value:el.dataset.panel})));
  document.querySelectorAll('[data-window]').forEach(el=>el.addEventListener('click',()=>send('window',{value:el.dataset.window})));
  $('titlebar').addEventListener('mousedown',e=>{if(e.button===0&&!e.target.closest('button,input,form')&&e.detail<2)send('window',{value:'drag'});});
  $('titlebar').addEventListener('dblclick',e=>{if(!e.target.closest('button,input,form'))send('window',{value:'maximize'});});
  send('ready');
})();
