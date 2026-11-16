(()=>{
 const api=window.signalStudioDesktop,bar=document.querySelector('.desktop-titlebar');
 if(!api||!bar)return;
 function state(value){document.documentElement.dataset.maximized=String(value.maximized||value.fullscreen);const button=bar.querySelector('[data-window-action="maximize"]');button.setAttribute('aria-label',value.maximized?'Restore window':'Maximize window');button.textContent=value.maximized?'❐':'□';window.dispatchEvent(new Event('resize'))}
 bar.querySelectorAll('[data-window-action]').forEach(button=>button.addEventListener('click',()=>api.windowAction(button.dataset.windowAction)));
 bar.addEventListener('dblclick',event=>{if(!event.target.closest('button'))api.windowAction('maximize')});
 api.onWindowState(state);api.windowState().then(state);
})();
