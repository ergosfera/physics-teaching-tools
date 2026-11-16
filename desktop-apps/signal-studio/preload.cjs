const {contextBridge,ipcRenderer,webUtils}=require('electron');
contextBridge.exposeInMainWorld('signalStudioDesktop',{
 openGuide:(topic,anchor)=>ipcRenderer.invoke('signal-studio:guide',topic,anchor),
 windowAction:action=>ipcRenderer.invoke('signal-studio:window',action),
 windowState:()=>ipcRenderer.invoke('signal-studio:window-state'),
 onWindowState:callback=>{const listener=(_event,state)=>callback(state);ipcRenderer.on('signal-studio:window-state',listener);return ()=>ipcRenderer.removeListener('signal-studio:window-state',listener)},
 filePath:file=>webUtils.getPathForFile(file),
 savePlaylist:paths=>ipcRenderer.invoke('signal-studio:playlist-save',paths),
 openPlaylist:()=>ipcRenderer.invoke('signal-studio:playlist-open')
});
