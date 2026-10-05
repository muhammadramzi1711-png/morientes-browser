'use strict';
const fs=require('node:fs');
const path=require('node:path');
const http=require('node:http');
const assert=require('node:assert/strict');
const {chromium}=require(process.env.CODEX_PRIMARY_RUNTIME_NODE_MODULES?path.join(process.env.CODEX_PRIMARY_RUNTIME_NODE_MODULES,'playwright'):'playwright');

const root=process.env.MORIENTES_UI_ROOT?path.resolve(process.env.MORIENTES_UI_ROOT):path.resolve(__dirname,'../MorientesBrowser/ui');
const output=path.resolve(__dirname,'../deliverables');fs.mkdirSync(output,{recursive:true});
const outputPrefix=process.env.MORIENTES_PREVIEW_PREFIX||'Morientes_Browser';
const initial={type:'state',version:'0.1.0',activeId:'home',tabs:[{id:'home',title:'Tab Baru',url:'morientes:home',loading:false},{id:'google',title:'Google',url:'https://www.google.com/',loading:false},{id:'drive',title:'Google Drive',url:'https://drive.google.com/',loading:false}],canBack:false,canForward:false,bookmarked:false,settings:{engine:'google',theme:'light',restoreSession:true,defaultZoom:80},bookmarks:[],history:[],downloads:[],sidebarOpen:false,sidebarMode:'bookmarks'};
const preview=`<!doctype html><html><head><meta charset="utf-8"><style>*{box-sizing:border-box}body{margin:0;min-height:100vh;display:grid;place-items:center;background:radial-gradient(ellipse at 20% 20%,#c9d6e9,#8b9bb2);font-family:'Segoe UI',sans-serif}.window{width:1280px;height:850px;padding:5px;background:#e9e9eb;border-radius:15px;box-shadow:0 28px 75px #2e432331,0 5px 16px #2e432322;overflow:hidden}.chrome{width:100%;height:92px;border:0;display:block}.body{display:flex;height:748px}.home{height:100%;border:0;flex:1;min-width:0}.sidebar{width:340px;height:100%;border:0;display:none;flex-shrink:0}</style></head><body><div class="window"><iframe title="Chrome" class="chrome" src="/chrome.html"></iframe><div class="body"><iframe title="Halaman awal" class="home" src="/home/home.html"></iframe><iframe title="Panel" class="sidebar" src="/sidebar.html"></iframe></div></div></body></html>`;
const types={'.html':'text/html','.css':'text/css','.js':'text/javascript','.svg':'image/svg+xml'};
const server=http.createServer((req,res)=>{
 const pathname=decodeURIComponent(new URL(req.url,'http://localhost').pathname);
 if(pathname==='/preview'){res.writeHead(200,{'Content-Type':'text/html'});res.end(preview);return;}
 const file=path.resolve(root,'.'+pathname);
 if(!file.startsWith(root+path.sep)||!fs.existsSync(file)){res.writeHead(404);res.end();return;}
 res.writeHead(200,{'Content-Type':types[path.extname(file)]||'application/octet-stream'});res.end(fs.readFileSync(file));
});
let passed=0;const results=[];
const check=(condition,name)=>{assert.ok(condition,name);passed++;results.push('PASS: '+name);};
async function main(){
 await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));const base='http://127.0.0.1:'+server.address().port;
 const browser=await chromium.launch({executablePath:process.env.MORIENTES_TEST_CHROME||undefined,headless:true,args:['--no-sandbox']});
 try{
  const context=await browser.newContext({viewport:{width:1280,height:850},locale:'id-ID',timezoneId:'Asia/Jakarta'});
  await context.addInitScript(({initial})=>{
   window.chrome=window.chrome||{};const listeners=[];window.__outbound=[];
   window.__dispatch=data=>listeners.forEach(handler=>handler({data}));
   window.chrome.webview={addEventListener:(name,handler)=>{if(name==='message')listeners.push(handler);},postMessage:message=>{window.__outbound.push(message);if(message.action==='ready')queueMicrotask(()=>window.__dispatch(location.pathname.includes('/home/')?{type:'homeState',settings:initial.settings,bookmarks:initial.bookmarks}:initial));}};
  },{initial});
  const errors=[];
  const chrome=await context.newPage();chrome.on('pageerror',e=>errors.push(e.message));await chrome.goto(base+'/chrome.html');
  const messages=page=>page.evaluate(()=>window.__outbound);
  const last=async page=>(await messages(page)).at(-1);
  check(await chrome.getByRole('tab').count()===3,'chrome renders native tab state');
  check(await chrome.locator('#back').isDisabled(),'back is unavailable without navigation history');
  await chrome.locator('#address').fill('contoh pencarian');await chrome.locator('#address').press('Enter');
  assert.deepEqual(await last(chrome),{action:'navigate',value:'contoh pencarian'});check(true,'omnibox submits typed input to native host');
  await chrome.locator('#newTab').click();check((await last(chrome)).action==='newTab','new-tab button sends native action');
  await chrome.getByRole('tab',{name:'Google',exact:true}).click();check((await last(chrome)).id==='google','selecting a tab sends its stable ID');
  await chrome.getByRole('button',{name:'Tutup Google',exact:true}).click();check((await last(chrome)).action==='closeTab','closing a tab sends native action');
  await chrome.locator('#bookmark').click();check((await last(chrome)).action==='bookmark','bookmark button sends native action');
  await chrome.getByRole('button',{name:'Buka pengaturan',exact:true}).click();assert.deepEqual(await last(chrome),{action:'sidebar',value:'settings'});check(true,'settings opens a native sidebar');
  const poison='<img src=x onerror="window.__injected=true">';
  await chrome.evaluate(({initial,poison})=>window.__dispatch({...initial,tabs:[{...initial.tabs[0],title:poison}]}),{initial,poison});
  check(await chrome.locator('.tab-title').textContent()===poison&&!(await chrome.evaluate(()=>window.__injected)),'untrusted website title is rendered as text');
  for(const width of [1280,900,820]){await chrome.setViewportSize({width,height:92});check(await chrome.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'chrome fits '+width+'px');}
  await chrome.evaluate(initial=>window.__dispatch({...initial,activeId:'google',bookmarked:true,settings:{...initial.settings,theme:'dark'}}),initial);
  check(await chrome.locator('body').evaluate(el=>el.classList.contains('dark')),'chrome changes to dark theme');
  check(await chrome.locator('#protocol').getAttribute('title')==='Koneksi HTTPS','transport indicator follows current URL');
  const home=await context.newPage();home.on('pageerror',e=>errors.push(e.message));await home.goto(base+'/home/home.html');
  check(await home.locator('.shortcut').count()===6,'home renders six quick destinations');
  await home.locator('#searchInput').fill('ramzi browser');await home.locator('#searchInput').press('Enter');assert.deepEqual(await last(home),{action:'navigate',value:'ramzi browser'});check(true,'home search submits to native navigation');
  await home.getByRole('button',{name:'YouTube',exact:true}).click();check((await last(home)).value==='https://www.youtube.com','quick destination opens the correct URL');
  await home.getByRole('button',{name:'Gmail',exact:true}).click({modifiers:['Control']});check((await last(home)).action==='newTab','Ctrl-click shortcut opens a separate tab');
  await home.evaluate(poison=>window.__dispatch({type:'homeState',settings:{engine:'duckduckgo',theme:'dark'},bookmarks:[{title:poison,url:'https://example.com/'}]}),poison);
  check(await home.locator('#engine').textContent()==='DuckDuckGo','home reflects the search preference');
  check(await home.locator('.saved-link').textContent()===poison&&!(await home.evaluate(()=>window.__injected)),'saved bookmark title cannot inject markup');
  for(const width of [1270,820,480]){await home.setViewportSize({width,height:748});check(await home.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'home fits '+width+'px including an open sidebar');}
  const sidebar=await context.newPage();sidebar.on('pageerror',e=>errors.push(e.message));await sidebar.setViewportSize({width:340,height:748});await sidebar.goto(base+'/sidebar.html');
  await sidebar.evaluate(initial=>window.__dispatch({...initial,sidebarMode:'settings'}),initial);
  await sidebar.getByRole('combobox',{name:'Mesin pencarian'}).selectOption('bing');assert.deepEqual(await last(sidebar),{action:'setting',id:'engine',value:'bing'});check(true,'search setting sends an allowlisted preference');
  if(await sidebar.getByRole('combobox',{name:'Zoom default'}).count()){
   check(await sidebar.getByRole('combobox',{name:'Zoom default'}).inputValue()==='80','default zoom shows the saved 80 percent preference');
   await sidebar.getByRole('combobox',{name:'Zoom default'}).selectOption('100');assert.deepEqual(await last(sidebar),{action:'setting',id:'defaultZoom',value:'100'});check(true,'default zoom changes send the native setting');
  }
  if(await sidebar.getByRole('button',{name:'Cek pembaruan'}).count()){
   await sidebar.getByRole('button',{name:'Cek pembaruan'}).click();assert.deepEqual(await last(sidebar),{action:'checkUpdates'});check(true,'manual update check reaches the native updater');
   await sidebar.getByRole('checkbox',{name:'Unduh pembaruan otomatis'}).uncheck();assert.deepEqual(await last(sidebar),{action:'setting',id:'autoUpdates',value:'false'});check(true,'automatic update preference can be disabled');
   await sidebar.evaluate(initial=>window.__dispatch({...initial,sidebarMode:'settings',updates:{status:'Mengunduh pembaruan…',busy:true,ready:false}}),initial);
   check(await sidebar.getByRole('button',{name:'Cek pembaruan'}).isDisabled(),'download state disables overlapping manual checks');
  }
  await sidebar.getByRole('button',{name:'☾ Gelap'}).click();check((await last(sidebar)).value==='dark','theme setting sends native action');
  const history=[{id:'one',title:'Google Drive',url:'https://drive.google.com/',time:'2026-10-02T13:00:00+07:00'},{id:'two',title:'Google',url:'https://www.google.com/',time:'2026-10-02T12:00:00+07:00'}];
  await sidebar.evaluate(({initial,history})=>window.__dispatch({...initial,sidebarMode:'history',history}),{initial,history});
  await sidebar.locator('#filter').fill('drive');check(await sidebar.locator('.item').count()===1,'history filter selects matching visits');
  await sidebar.getByRole('button',{name:'Hapus semua riwayat',exact:true}).click();check(await sidebar.locator('.confirm').count()===1&&(await last(sidebar)).action!=='clearHistory','history deletion waits for an explicit confirmation');
  await sidebar.getByRole('button',{name:'Hapus riwayat',exact:true}).click();check((await last(sidebar)).action==='clearHistory','confirmed history deletion reaches native host');
  await sidebar.evaluate(initial=>window.__dispatch({...initial,sidebarMode:'bookmarks',bookmarks:[{id:'safe',title:'<script>alert(1)</script>',url:'https://example.com/'}]}),initial);
  check(await sidebar.locator('.item-title').textContent()==='<script>alert(1)</script>','sidebar treats bookmark names as plain text');
  await sidebar.getByRole('button',{name:'Hapus bookmark <script>alert(1)</script>',exact:true}).click();assert.deepEqual(await last(sidebar),{action:'removeBookmark',id:'safe'});check(true,'bookmark deletion uses its stable ID');
  await sidebar.evaluate(initial=>window.__dispatch({...initial,sidebarMode:'downloads',downloads:[{id:'file',name:'contoh.pdf',received:512,total:1024,status:'InProgress'}]}),initial);
  check(await sidebar.locator('.progress-fill').evaluate(el=>el.style.width)==='50%','download progress uses actual received bytes');
  await sidebar.getByRole('button',{name:'Batalkan',exact:true}).click();check((await last(sidebar)).action==='cancelDownload','download cancellation targets the selected download');
  await sidebar.evaluate(initial=>window.__dispatch({...initial,sidebarMode:'about'}),initial);
  check(await sidebar.locator('.version').textContent()==='Browser pribadi · v0.1.0','about displays the application version');
  check(errors.length===0,'all UI pages run without JavaScript errors');
  const previewPage=await context.newPage();await previewPage.setViewportSize({width:1440,height:1020});await previewPage.goto(base+'/preview');await previewPage.waitForTimeout(350);
  await previewPage.screenshot({path:path.join(output,outputPrefix+'_Preview.png')});
  for(const frame of previewPage.frames())if(frame!==previewPage.mainFrame())await frame.evaluate(initial=>window.__dispatch(location.pathname.includes('/home/')?{type:'homeState',settings:{...initial.settings,theme:'dark'},bookmarks:[]}:{...initial,settings:{...initial.settings,theme:'dark'},sidebarOpen:true,sidebarMode:'settings'}),initial);
  await previewPage.locator('.sidebar').evaluate(el=>el.style.display='block');await previewPage.screenshot({path:path.join(output,outputPrefix+'_Dark_Preview.png')});
  results.push(passed+' UI checks passed.');fs.writeFileSync(path.join(output,outputPrefix==='Morientes_Browser'?'ui-check-results.txt':outputPrefix+'_ui-check-results.txt'),results.join('\n')+'\n');console.log(results.join('\n'));
  await context.close();
 }finally{await browser.close();}
}
main().catch(error=>{console.error(error);process.exitCode=1;}).finally(()=>server.close());
