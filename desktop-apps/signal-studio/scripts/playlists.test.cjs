const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs/promises'),os=require('node:os'),path=require('node:path');
const {encodePlaylist,readPlaylist}=require('../playlists.cjs');
test('playlist round trip preserves order, Unicode and relative paths, reporting missing files',async()=>{
 const dir=await fs.mkdtemp(path.join(os.tmpdir(),'signal-playlist-'));
 try{const a=path.join(dir,'Canción #1.wav'),b=path.join(dir,'Second.mp3'),missing=path.join(dir,'Gone.wav'),file=path.join(dir,'Mix.signalplaylist');await fs.writeFile(a,'fixture');await fs.writeFile(b,'fixture');await fs.writeFile(file,encodePlaylist([b,a,missing,a],file));const value=await readPlaylist(file);assert.deepEqual(value.tracks.map(t=>t.name),['Second.mp3','Canción #1.wav','Canción #1.wav']);assert.equal(value.missing.length,1);assert(value.tracks[1].url.includes('%23'));assert(!JSON.parse(await fs.readFile(file)).tracks[0].path.startsWith('/'));}finally{await fs.rm(dir,{recursive:true,force:true})}
});
test('rejects empty, malformed and nonmedia playlist references',async()=>{
 assert.throws(()=>encodePlaylist([], '/tmp/mix.signalplaylist'));assert.throws(()=>encodePlaylist(['/tmp/passwords.txt'],'/tmp/mix.signalplaylist'));
 const dir=await fs.mkdtemp(path.join(os.tmpdir(),'signal-invalid-'));try{const file=path.join(dir,'mix.signalplaylist');await fs.writeFile(file,'not JSON');await assert.rejects(()=>readPlaylist(file));await fs.writeFile(file,JSON.stringify({format:'signal-studio-playlist',version:1,tracks:[{path:'file.txt'}]}));await assert.rejects(()=>readPlaylist(file));}finally{await fs.rm(dir,{recursive:true,force:true})}
});
