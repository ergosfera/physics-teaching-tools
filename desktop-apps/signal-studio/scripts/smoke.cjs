const {app,BrowserWindow}=require('electron');
const path=require('node:path');
const fs=require('node:fs');
app.commandLine.appendSwitch('autoplay-policy','no-user-gesture-required');
require('../main.cjs');
app.whenReady().then(async()=>{
 const win=BrowserWindow.getAllWindows().find(w=>!w.getParentWindow());win.webContents.setBackgroundThrottling(false);win.hide();
 win.webContents.setAudioMuted(true);
 const errors=[];
 win.webContents.on('console-message',event=>{if(event.level==='error')errors.push(event.message)});
 try{
 if(win.webContents.isLoading())await new Promise(r=>win.webContents.once('did-finish-load',r));
 win.webContents.setZoomFactor(1);await new Promise(r=>setTimeout(r,200));
 await win.webContents.executeJavaScript('document.fonts.ready');
 const result=await win.webContents.executeJavaScript(`(async()=>{
 const wait=ms=>new Promise(r=>setTimeout(r,ms));
 if(!document.querySelector('.keys svg'))throw Error('Transport icons missing');
 const rate=48000,n=rate*3,bytes=new ArrayBuffer(44+n*2),d=new DataView(bytes);const str=(at,s)=>[...s].forEach((v,i)=>d.setUint8(at+i,v.charCodeAt(0)));str(0,'RIFF');d.setUint32(4,36+n*2,true);str(8,'WAVE');str(12,'fmt ');d.setUint32(16,16,true);d.setUint16(20,1,true);d.setUint16(22,1,true);d.setUint32(24,rate,true);d.setUint32(28,rate*2,true);d.setUint16(32,2,true);d.setUint16(34,16,true);str(36,'data');d.setUint32(40,n*2,true);for(let i=0;i<n;i++)d.setInt16(44+i*2,Math.sin(i*2*Math.PI*440/rate)*8192,true);
 const transfer=new DataTransfer();transfer.items.add(new File([bytes],'Desktop smoke test.wav',{type:'audio/wav'}));const input=document.querySelector('.audio-file');input.files=transfer.files;input.dispatchEvent(new Event('change'));
 await wait(400);document.querySelector('[data-action="play"]').click();await wait(600);
 const media=document.querySelector('.media-source');if(media.paused||media.currentTime<=0)throw Error('WAV playback failed '+JSON.stringify({paused:media.paused,time:media.currentTime,ready:media.readyState,error:media.error?.message,status:document.querySelector('.mode').textContent,detail:document.querySelector('.track').textContent}));
 const slider=document.querySelector('.master-volume');slider.value='37';slider.dispatchEvent(new Event('input'));slider.dispatchEvent(new Event('change'));
 if(window.signalStudioState.widgetState.privateContent.masterVolume!==.37)throw Error('Volume persistence failed');
 document.querySelector('.fader-grid input').value='6';document.querySelector('.fader-grid input').dispatchEvent(new Event('input'));
 const outcome={title:document.title,playedSeconds:media.currentTime,volume:slider.value,icons:document.querySelectorAll('svg').length,fontLoaded:document.fonts.check('16px Caveat'),overflow:document.documentElement.scrollWidth>innerWidth};
 media.pause();return outcome;
})()`);
 const fixtureDir=fs.mkdtempSync(path.join(require('node:os').tmpdir(),'signal-integration-'));
 const wavPath=path.join(fixtureDir,'Saved song.wav'),playlistPath=path.join(fixtureDir,'Round trip.signalplaylist'),savedPath=path.join(fixtureDir,'Saved.signalplaylist');
 const data=Buffer.alloc(44+48000*2);data.write('RIFF');data.writeUInt32LE(data.length-8,4);data.write('WAVEfmt ',8);data.writeUInt32LE(16,16);data.writeUInt16LE(1,20);data.writeUInt16LE(1,22);data.writeUInt32LE(48000,24);data.writeUInt32LE(96000,28);data.writeUInt16LE(2,32);data.writeUInt16LE(16,34);data.write('data',36);data.writeUInt32LE(data.length-44,40);fs.writeFileSync(wavPath,data);
 fs.writeFileSync(playlistPath,require('../playlists.cjs').encodePlaylist([wavPath],playlistPath));
 const dialog=require('electron').dialog;dialog.showOpenDialog=async()=>({canceled:false,filePaths:[playlistPath]});dialog.showSaveDialog=async()=>({canceled:false,filePath:savedPath});
 await win.webContents.executeJavaScript(`document.querySelector('.open-playlist').click()`);await new Promise(r=>setTimeout(r,350));
 await win.webContents.executeJavaScript(`document.querySelector('[data-action="play"]').click()`);await new Promise(r=>setTimeout(r,350));
 if(!await win.webContents.executeJavaScript(`document.querySelector('.media-source').currentTime>0`))throw Error('Saved playlist playback failed');
 win.webContents.debugger.attach('1.3');
 const dom=await win.webContents.debugger.sendCommand('DOM.getDocument');
 const inputNode=await win.webContents.debugger.sendCommand('DOM.querySelector',{nodeId:dom.root.nodeId,selector:'.audio-file'});
 await win.webContents.debugger.sendCommand('DOM.setFileInputFiles',{nodeId:inputNode.nodeId,files:[wavPath]});win.webContents.debugger.detach();
 await new Promise(r=>setTimeout(r,150));
 await win.webContents.executeJavaScript(`document.querySelector('.save-playlist').click()`);await new Promise(r=>setTimeout(r,350));
 if(JSON.parse(fs.readFileSync(savedPath)).tracks.length!==2)throw Error('Save playlist failed');
 dialog.showOpenDialog=async()=>({canceled:true,filePaths:[]});await win.webContents.executeJavaScript(`document.querySelector('.open-playlist').click()`);await new Promise(r=>setTimeout(r,100));
 if(!await win.webContents.executeJavaScript(`document.querySelector('.queue-row .song').textContent==='Saved song'`))throw Error('Canceled open changed queue');
 await win.webContents.executeJavaScript(`document.querySelector('.media-source').pause()`);
 console.log('Native playlist save/open, cancellation and file-backed playback passed');
 fs.rmSync(fixtureDir,{recursive:true,force:true});
 const layouts=[];
 for(const [width,height,columns] of [[1280,600,3],[1280,720,3],[1920,1080,3],[900,720,2],[600,700,1],[360,700,1]]){
  win.setSize(width,height);await new Promise(r=>setTimeout(r,400));
  const measure=await win.webContents.executeJavaScript(`(()=>{const rack=document.getElementById('tape-stereo-system'),eq=document.getElementById('tape-equalizer'),v=eq.querySelector('.master-volume').getBoundingClientRect(),b=eq.getBoundingClientRect();return {columns:Number(rack.dataset.columns),overflow:document.documentElement.scrollWidth>innerWidth,volumeInside:v.bottom<=b.bottom&&v.top>=b.top,rackBottom:rack.getBoundingClientRect().bottom,viewport:innerHeight,bodyHeight:document.body.scrollHeight}})()`);
  if(measure.columns!==columns||measure.overflow||!measure.volumeInside)throw Error('Layout '+width+': '+JSON.stringify(measure));
  if(columns===3&&measure.rackBottom>measure.viewport)throw Error('Desktop vertical overflow '+width);
  layouts.push({width,height,...measure});
  if(width===360)fs.writeFileSync(path.join(__dirname,'../dist/smoke-narrow.png'),(await win.webContents.capturePage()).toPNG());
 }
 for(const zoom of [1,1.25,1.5,2]){
  win.setSize(1366,768);win.webContents.setZoomFactor(zoom);await new Promise(r=>setTimeout(r,250));
  await win.webContents.executeJavaScript(`document.documentElement.dataset.maximized='true';window.dispatchEvent(new Event('resize'))`);await new Promise(r=>setTimeout(r,250));
  const fit=await win.webContents.executeJavaScript(`({width:innerWidth,fit:document.documentElement.dataset.fit,scroll:document.documentElement.scrollHeight>innerHeight,bottom:document.querySelector('.master-volume').getBoundingClientRect().bottom,h:innerHeight})`);
  if(fit.scroll||fit.bottom>fit.h)throw Error('Scaled maximized layout '+JSON.stringify(fit));
 }
 win.webContents.setZoomFactor(1);
 await win.webContents.executeJavaScript(`document.documentElement.dataset.maximized='false';window.dispatchEvent(new Event('resize'))`);
 win.maximize();await new Promise(r=>setTimeout(r,400));
 if(!await win.webContents.executeJavaScript(`document.documentElement.dataset.maximized==='true'`))throw Error('Native maximize state failed');
 await win.webContents.executeJavaScript(`document.querySelector('[data-window-action="maximize"]').click()`);await new Promise(r=>setTimeout(r,350));
 if(win.isMaximized())throw Error('Restore control failed');
 win.setSize(1280,800);await new Promise(r=>setTimeout(r,300));
 const before=await win.webContents.executeJavaScript('document.body.scrollHeight');
 for(const topic of ['vu','level','waveform','spectrum','equalizer','about']){
  const selector=topic==='about'?'.studio-about-link':'[data-sound-info="'+topic+'"]';
  await win.webContents.executeJavaScript(`document.querySelector('${selector}').click()`);
  await new Promise(r=>setTimeout(r,400));
  const child=BrowserWindow.getAllWindows().find(w=>w.getParentWindow()===win);
  if(!child)throw Error('Missing reading window '+topic);
  const checked=await child.webContents.executeJavaScript(`({hash:location.hash,shown:!document.getElementById('${topic==='about'?'signal-studio-about':'cassette-sound-guide'}').hidden})`);
  if(checked.hash!=='#'+topic||!checked.shown)throw Error('Wrong guide '+topic);
  if(topic==='vu')fs.writeFileSync(path.join(__dirname,'../dist/smoke-guide.png'),(await child.webContents.capturePage()).toPNG());
  if(await win.webContents.executeJavaScript('document.body.scrollHeight')!==before)throw Error('Player grew');
 }
 const child=BrowserWindow.getAllWindows().find(w=>w.getParentWindow()===win);child.close();await new Promise(r=>setTimeout(r,100));
 await win.webContents.executeJavaScript(`document.querySelector('[data-sound-info="vu"]').click()`);await new Promise(r=>setTimeout(r,300));
 if(BrowserWindow.getAllWindows().length!==2)throw Error('Guide reopen failed');
 BrowserWindow.getAllWindows().find(w=>w.getParentWindow()===win).close();
 console.log(JSON.stringify({layouts,guideChecks:6,playerHeightUnchanged:true}));
 const image=await win.webContents.capturePage();fs.writeFileSync(path.join(__dirname,'../dist/smoke-desktop.png'),image.toPNG());
 if(errors.length)throw Error(errors.join('\n'));
 console.log(JSON.stringify({passed:true,...result}));app.exit(0);
 }catch(e){console.error(e);app.exit(1)}
});
