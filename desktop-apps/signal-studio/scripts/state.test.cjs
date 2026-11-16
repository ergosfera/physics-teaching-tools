const {test}=require('node:test');
const assert=require('node:assert/strict');
const vm=require('node:vm');
const fs=require('node:fs');
const path=require('node:path');
const stateCode=fs.readFileSync(path.join(__dirname,'../app/state.js'),'utf8');
function boot(stored){const store=new Map([['signal-studio.settings.v1',stored]]);const context={window:{},localStorage:{getItem:k=>store.get(k),setItem:(k,v)=>store.set(k,v)}};vm.runInNewContext(stateCode,context);return {api:context.window.signalStudioState,store};}
test('volume and EQ survive a fresh desktop session',async()=>{const {api,store}=boot(null);await api.setWidgetState({privateContent:{eqValues:[6,0,0,0,0,0,0,0,0,-4],eqEnabled:false,masterVolume:0.37}});const next=boot(store.get('signal-studio.settings.v1')).api.widgetState.privateContent;assert.equal(next.masterVolume,.37);assert.equal(next.eqEnabled,false);assert.equal(next.eqValues[0],6);assert.equal(next.eqValues[9],-4);});
test('malformed storage uses defaults and out-of-range values are clamped',async()=>{assert.equal(boot('{bad').api.widgetState,null);const {api}=boot(null);await api.setWidgetState({privateContent:{masterVolume:8,eqValues:Array(10).fill(50)}});assert.equal(api.widgetState.privateContent.masterVolume,1);assert.equal(api.widgetState.privateContent.eqValues[0],12);});
test('all renderer scripts parse and are offline',()=>{new vm.Script(fs.readFileSync(path.join(__dirname,'../app/player.js'),'utf8'));const html=fs.readFileSync(path.join(__dirname,'../app/index.html'),'utf8');assert(!/<(?:script|link)[^>]*(?:src|href)="https?:/.test(html));assert(html.includes("connect-src 'none'"));const guide=fs.readFileSync(path.join(__dirname,'../app/guide.html'),'utf8');assert(guide.includes('Mauricio Mendivelso-Villaquirán'));assert(guide.includes('Valentyn Vieshchunov'));});
