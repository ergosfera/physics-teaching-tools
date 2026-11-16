(()=>{
const titles={vu:'Stereo VU · sustained level',level:'Stereo level · digital peaks',waveform:'Waveform · amplitude through time',spectrum:'Spectrum · sound by frequency',equalizer:'Equalizer · shaping the spectrum',about:'About Signal Studio'};
function render(){
 const topic=location.hash.slice(1),valid=Object.hasOwn(titles,topic),isAbout=topic==='about';
 document.title=valid?titles[topic]+' — Signal Studio':'Signal Studio';
 document.getElementById('signal-studio-about').hidden=!isAbout;
 document.getElementById('cassette-sound-guide').hidden=!valid||isAbout;
 document.querySelectorAll('[data-lesson]').forEach(a=>a.hidden=a.dataset.lesson!==topic);
 document.getElementById('sound-guide-title').textContent=titles[topic]||'Guide';
 window.scrollTo(0,0);
}
document.querySelectorAll('.guide-close,.studio-about-close').forEach(b=>b.addEventListener('click',()=>window.close()));
document.addEventListener('keydown',e=>{if(e.key==='Escape')window.close()});
window.addEventListener('hashchange',render);render();
})();
