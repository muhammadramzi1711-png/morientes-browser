'use strict';
(() => {
  const bridge=window.chrome?.webview;
  const send=(action,extra={})=>bridge?.postMessage({action,...extra});
  const links=[['Google','G','google','https://www.google.com'],['YouTube','▶','youtube','https://www.youtube.com'],['Gmail','M','gmail','https://mail.google.com'],['Drive','△','drive','https://drive.google.com'],['WhatsApp','◔','whatsapp','https://web.whatsapp.com'],['ChatGPT','✺','chatgpt','https://chatgpt.com']];
  const quick=document.getElementById('quickLinks');
  for(const [title,glyph,color,url] of links){
    const button=document.createElement('button');button.className='shortcut';button.title=url;button.setAttribute('aria-label',title);
    const icon=document.createElement('span');icon.className='shortcut-icon '+color;icon.textContent=glyph;icon.setAttribute('aria-hidden','true');
    const caption=document.createElement('span');caption.textContent=title;
    button.append(icon,caption);button.addEventListener('click',e=>send(e.ctrlKey?'newTab':'navigate',{value:url}));quick.append(button);
  }
  document.getElementById('searchForm').addEventListener('submit',e=>{e.preventDefault();const value=document.getElementById('searchInput').value.trim();if(value)send('navigate',{value});});
  bridge?.addEventListener('message',event=>{
    const data=event.data;if(data?.type!=='homeState')return;
    document.body.classList.toggle('dark',data.settings.theme==='dark');
    document.getElementById('engine').textContent=({google:'Google',duckduckgo:'DuckDuckGo',bing:'Bing'})[data.settings.engine]||'Google';
    const list=document.getElementById('savedLinks');list.replaceChildren();
    document.getElementById('savedSection').hidden=!data.bookmarks?.length;
    for(const bookmark of data.bookmarks||[]){
      const button=document.createElement('button');button.className='saved-link';button.textContent=bookmark.title;button.title=bookmark.url;
      button.addEventListener('click',()=>send('navigate',{value:bookmark.url}));list.append(button);
    }
  });
  send('ready');
})();
