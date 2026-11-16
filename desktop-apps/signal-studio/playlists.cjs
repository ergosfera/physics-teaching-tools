'use strict';
const fs=require('node:fs/promises'),path=require('node:path');
const {pathToFileURL}=require('node:url');
const media=/\.(mp3|wav|m4a|ogg|flac|aac|aif|aiff|opus|mp4|m4v|mov|webm|mkv|avi)$/i;
function encodePlaylist(paths,file){
 if(!Array.isArray(paths)||!paths.length||paths.length>10000)throw Error('Choose tracks before saving a playlist.');
 const tracks=paths.map(p=>{if(typeof p!=='string'||!path.isAbsolute(p)||!media.test(p))throw Error('One or more tracks cannot be saved. Add files from your computer first.');return {path:(path.relative(path.dirname(file),p)||path.basename(p)).split(path.sep).join('/')}});
 return JSON.stringify({format:'signal-studio-playlist',version:1,tracks},null,2)+'\n';
}
async function readPlaylist(file){
 if((await fs.stat(file)).size>4*1024*1024)throw Error('This playlist is too large.');
 let value;try{value=JSON.parse(await fs.readFile(file,'utf8'))}catch{throw Error('This is not a valid Signal Studio playlist.');}
 if(value?.format!=='signal-studio-playlist'||value.version!==1||!Array.isArray(value.tracks)||value.tracks.length>10000)throw Error('Unsupported playlist format.');
 const tracks=[],missing=[];
 for(const item of value.tracks){
  if(typeof item?.path!=='string'||!item.path||!media.test(item.path)||item.path.includes('\0'))throw Error('The playlist contains an invalid track.');
  const p=path.resolve(path.dirname(file),item.path);
  try{if(!(await fs.stat(p)).isFile())throw Error();tracks.push({name:path.basename(p),path:p,url:pathToFileURL(p).href})}catch{missing.push(path.basename(p))}
 }
 return {tracks,missing};
}
module.exports={encodePlaylist,readPlaylist};
