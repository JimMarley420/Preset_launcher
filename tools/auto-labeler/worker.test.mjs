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
