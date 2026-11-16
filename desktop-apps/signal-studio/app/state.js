(()=>{
  const key='signal-studio.settings.v1';
  let saved=null;
  try { saved=JSON.parse(localStorage.getItem(key)||'null'); } catch {}
  function valid(state){
    const p=state?.privateContent;
    if(!p||typeof p!=='object')return null;
    return {privateContent:{eqValues:Array.isArray(p.eqValues)&&p.eqValues.length===10?p.eqValues.map(n=>Number.isFinite(n)?Math.max(-12,Math.min(12,n)):0):Array(10).fill(0),eqEnabled:typeof p.eqEnabled==='boolean'?p.eqEnabled:true,masterVolume:Number.isFinite(p.masterVolume)?Math.max(0,Math.min(1,p.masterVolume)):1}};
  }
  window.signalStudioState={widgetState:valid(saved),async setWidgetState(state){const normalized=valid(state);this.widgetState=normalized;try{localStorage.setItem(key,JSON.stringify(normalized));}catch{}}};
})();
