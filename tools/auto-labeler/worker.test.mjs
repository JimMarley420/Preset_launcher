import test from "node:test";
import assert from "node:assert/strict";
import {classify,validSignature,handle} from "./worker.mjs";
const labels=["area: ui","area: updater","area: installer","area: ci","type: bug","type: feature","update: required","priority: high"];
const issue={title:"fix: repair updater",body:"",labels:[],pull_request:{}};
test("PR paths and conventional title",()=>assert.deepEqual(classify(issue,["PresetLauncherWinUI3/Services/UpdateService.cs"],labels),["area: updater","type: bug"]));
test("preserve manual categories",()=>assert.deepEqual(classify({...issue,labels:[{name:"area: ui"},{name:"type: feature"},{name:"update: required"}]},["Installer/build.ps1"],labels),[]));
test("unknown labels never created",()=>assert.deepEqual(classify(issue,["Installer/build.ps1"],[]),[]));
test("issue form classification",()=>assert.deepEqual(classify({title:"Update fails",body:"### Steps to reproduce\n\nOpen app",labels:[]},[],labels),["area: updater","type: bug"]));
test("priority and policy never selected",()=>assert.deepEqual(classify({title:"urgent data loss",body:"required update",labels:[]},[],labels),[]));
test("HMAC verifies exact bytes",async()=>{
 const body=new TextEncoder().encode("payload"), key=await crypto.subtle.importKey("raw",new TextEncoder().encode("secret"),{name:"HMAC",hash:"SHA-256"},false,["sign"]);
 const sig="sha256="+Buffer.from(await crypto.subtle.sign("HMAC",key,body)).toString("hex");
 assert.equal(await validSignature(body,"secret",sig),true);
 assert.equal(await validSignature(body,"wrong",sig),false);
 assert.equal(await validSignature(body,"secret",""),false);
});
test("reject unsigned webhook",async()=>{
 const r=await handle(new Request("https://example.org/github",{method:"POST",body:"{}"}),{GITHUB_TOKEN:"dummy",WEBHOOK_SECRET:"secret"});
 assert.equal(r.status,401);
});

test("signed maintainer request rejects redirects without forwarding credentials", async (ctx) => {
 const payload = JSON.stringify({repository:{full_name:"JimMarley420/Preset_launcher"},action:"created",sender:{login:"JimMarley420",type:"User"},comment:{body:"/label-this",user:{login:"JimMarley420"},author_association:"OWNER"},issue:{number:6}});
 const key = await crypto.subtle.importKey("raw",new TextEncoder().encode("secret"),{name:"HMAC",hash:"SHA-256"},false,["sign"]);
 const signature = "sha256="+Buffer.from(await crypto.subtle.sign("HMAC",key,new TextEncoder().encode(payload))).toString("hex");
 const mock = ctx.mock.method(globalThis,"fetch",async (url,options)=>{
   assert.equal(url,"https://api.github.com/repos/JimMarley420/Preset_launcher/issues/6");
   assert.equal(options.redirect,"manual");
   return new Response(null,{status:302,headers:{Location:"https://example.org/"}});
 });
 await assert.rejects(handle(new Request("https://example.org/github",{method:"POST",body:payload,headers:{"X-Hub-Signature-256":signature,"X-GitHub-Event":"issue_comment"}}),{GITHUB_TOKEN:"dummy",WEBHOOK_SECRET:"secret"}),/GitHub API failed: GET \/issues\/6 \(302\)/);
 assert.equal(mock.mock.callCount(),1);
});
