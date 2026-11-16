'use strict';
const { app, BrowserWindow, Menu, shell, session, ipcMain, screen, dialog } = require('electron');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const indexPath = path.join(__dirname, 'app', 'index.html');
const indexURL = pathToFileURL(indexPath).href;
const guidePath=path.join(__dirname,'app','guide.html'),guideURL=pathToFileURL(guidePath).href;
const {encodePlaylist,readPlaylist}=require('./playlists.cjs');
const fs=require('node:fs/promises');
const guides=new Map();const topics=new Set(['vu','level','waveform','spectrum','equalizer','about']);
const externalHosts = new Set(['play.google.com','www.itu.int','www.w3.org','www.nationalacademies.org','resource.npl.co.uk','www.mq.edu.au','fab2m.fr']);
function openReference(url) {
  try { const parsed = new URL(url); if (parsed.protocol === 'https:' && externalHosts.has(parsed.hostname)) shell.openExternal(parsed.href).catch(console.error); } catch {}
}
function createWindow() {
  const area=screen.getPrimaryDisplay().workArea;
  const win = new BrowserWindow({width:Math.min(1280,area.width),height:Math.min(900,area.height),minWidth:360,minHeight:480,backgroundColor:'#090e12',title:'Signal Studio',show:false,frame:false,autoHideMenuBar:true,webPreferences:{preload:path.join(__dirname,'preload.cjs'),nodeIntegration:false,contextIsolation:true,sandbox:true,webSecurity:true}});
  installWindowState(win);win.setMenuBarVisibility(false);
  win.webContents.setWindowOpenHandler(({url})=>{openReference(url);return {action:'deny'};});
  win.webContents.on('will-navigate',(event,url)=>{if(url!==indexURL){event.preventDefault();openReference(url);}});
  win.webContents.on('will-attach-webview',event=>event.preventDefault());
  const ownerId=win.id;win.on('closed',()=>{const child=guides.get(ownerId);if(child&&!child.isDestroyed())child.destroy();guides.delete(ownerId)});
  win.once('ready-to-show',()=>win.show());
  win.loadFile(indexPath);
}
ipcMain.handle('signal-studio:guide',async(event,topic,anchor)=>{
  if(event.senderFrame!==event.sender.mainFrame||event.senderFrame.url!==indexURL||!topics.has(topic))return false;
  const owner=BrowserWindow.fromWebContents(event.sender);if(!owner)return false;
  let child=guides.get(owner.id);
  if(child&&!child.isDestroyed()){
    await child.loadFile(guidePath,{hash:topic});child.show();child.focus();return true;
  }
  const parent=owner.getBounds(),area=screen.getDisplayMatching(parent).workArea;
  const width=Math.min(560,area.width),height=Math.min(760,area.height);
  let x=parent.x+parent.width+12;
  if(x+width>area.x+area.width)x=parent.x-width-12;
  if(x<area.x)x=parent.x+Math.min(parent.width-width,Number.isFinite(anchor?.x)?anchor.x:0);
  x=Math.round(Math.max(area.x,Math.min(x,area.x+area.width-width)));
  const y=Math.round(Math.max(area.y,Math.min(parent.y+(Number.isFinite(anchor?.y)?anchor.y:0),area.y+area.height-height)));
  child=new BrowserWindow({parent:owner,modal:false,x,y,width,height,minWidth:Math.min(360,area.width),minHeight:Math.min(400,area.height),show:false,frame:false,autoHideMenuBar:true,backgroundColor:'#101a20',title:'Signal Studio · Guide',webPreferences:{preload:path.join(__dirname,'preload.cjs'),nodeIntegration:false,contextIsolation:true,sandbox:true,webSecurity:true}});
  installWindowState(child);const ownerId=owner.id;guides.set(ownerId,child);child.setMenuBarVisibility(false);
  child.webContents.setWindowOpenHandler(({url})=>{openReference(url);return {action:'deny'}});
  child.webContents.on('will-navigate',(e,url)=>{if(url.split('#')[0]!==guideURL){e.preventDefault();openReference(url)}});
  child.webContents.on('will-attach-webview',e=>e.preventDefault());
  child.on('closed',()=>{guides.delete(ownerId);if(!owner.isDestroyed())owner.focus()});
  child.once('ready-to-show',()=>child.show());
  await child.loadFile(guidePath,{hash:topic});return true;
});
function trusted(event,playerOnly=false){return event.senderFrame===event.sender.mainFrame&&(event.senderFrame.url===indexURL||(!playerOnly&&event.senderFrame.url.split('#')[0]===guideURL))}
function state(win){return {maximized:win.isMaximized(),fullscreen:win.isFullScreen()}}
function installWindowState(win){for(const event of ['maximize','unmaximize','enter-full-screen','leave-full-screen'])win.on(event,()=>win.webContents.send('signal-studio:window-state',state(win)))}
ipcMain.handle('signal-studio:window-state',event=>trusted(event)?state(BrowserWindow.fromWebContents(event.sender)):{});
ipcMain.handle('signal-studio:window',(event,action)=>{
 if(!trusted(event))return;const win=BrowserWindow.fromWebContents(event.sender);
 if(action==='minimize')win.minimize();else if(action==='maximize'){if(win.isMaximized())win.unmaximize();else win.maximize()}else if(action==='close')win.close();
});
ipcMain.handle('signal-studio:playlist-save',async(event,paths)=>{
 if(!trusted(event,true))return {error:'Unavailable'};
 try{
  encodePlaylist(paths,path.join(app.getPath('documents'),'playlist.signalplaylist'));
  const result=await dialog.showSaveDialog(BrowserWindow.fromWebContents(event.sender),{title:'Save playlist',defaultPath:'My playlist.signalplaylist',filters:[{name:'Signal Studio playlist',extensions:['signalplaylist']}]});
  if(result.canceled||!result.filePath)return {canceled:true};
  await fs.writeFile(result.filePath,encodePlaylist(paths,result.filePath),'utf8');return {name:path.basename(result.filePath)};
 }catch(e){return {error:e.message}}
});
ipcMain.handle('signal-studio:playlist-open',async event=>{
 if(!trusted(event,true))return {error:'Unavailable'};
 try{
  const result=await dialog.showOpenDialog(BrowserWindow.fromWebContents(event.sender),{title:'Open playlist',properties:['openFile'],filters:[{name:'Signal Studio playlist',extensions:['signalplaylist']}]});
  if(result.canceled||!result.filePaths.length)return {canceled:true};
  return {...await readPlaylist(result.filePaths[0]),name:path.basename(result.filePaths[0])};
 }catch(e){return {error:e.message}}
});
app.setName('Signal Studio');
app.whenReady().then(()=>{
  session.defaultSession.setPermissionRequestHandler((_webContents,_permission,callback)=>callback(false));
  session.defaultSession.setPermissionCheckHandler(()=>false);
  app.setAboutPanelOptions({applicationName:'Signal Studio',applicationVersion:app.getVersion(),authors:['Mauricio Mendivelso-Villaquirán'],credits:'Implementation assistance: OpenAI Codex. Cassette inspiration: Casse-o-player by Valentyn Vieshchunov.'});
  const menu=[...(process.platform==='darwin'?[{role:'appMenu'}]:[]),{label:'File',submenu:[{role:process.platform==='darwin'?'close':'quit'}]},{role:'editMenu'},{label:'View',submenu:[{role:'reload'},{role:'resetZoom'},{role:'zoomIn'},{role:'zoomOut'},{type:'separator'},{role:'togglefullscreen'}]},{role:'windowMenu'}];
  Menu.setApplicationMenu(Menu.buildFromTemplate(menu));
  createWindow();
  app.on('activate',()=>{if(!BrowserWindow.getAllWindows().length)createWindow();});
});
app.on('window-all-closed',()=>{if(process.platform!=='darwin')app.quit();});
