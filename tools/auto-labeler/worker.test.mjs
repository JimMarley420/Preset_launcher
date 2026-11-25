import test from "node:test";
import assert from "node:assert/strict";
import {createHmac} from "node:crypto";
import {readFileSync} from "node:fs";
import {classify,validSignature,handle} from "./worker.mjs";
const labels=["area: ui","area: updater","area: installer","area: ci","type: bug","type: feature","update: required","priority: high"];
const issue={title:"fix: repair updater",body:"",labels:[],pull_request:{}};
test("PR paths and conventional title",()=>assert.deepEqual(classify(issue,["PresetLauncherWinUI3/Services/UpdateService.cs"],labels),["area: updater","type: bug"]));
test("preserve manual categories while adding a missing area",()=>assert.deepEqual(classify({...issue,labels:[{name:"area: ui"},{name:"type: feature"},{name:"update: required"}]},["Installer/build.ps1"],labels),["area: installer"]));
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

const repo = "JimMarley420/Preset_launcher";
const env = {GITHUB_TOKEN:"dummy", WEBHOOK_SECRET:"secret"};
const catalog = ["area: ci", "area: data", "area: documentation", "area: installer", "area: launcher",
  "area: localization", "area: platform", "area: tests", "area: ui", "area: updates", "type: bug",
  "type: documentation", "type: feature", "type: maintenance", "type: security", "priority: high", "update: required"];

test("PR #16 classifies all its changed files even with existing manually assigned labels", () => {
  // GitHub pulls/16/files at head 49502a44a5c06fc8aba62da4b2ab28fa5da80098.
  const files = readFileSync(new URL("./pr-16-files.txt", import.meta.url), "utf8").trim().split(/\r?\n/);
  assert.equal(files.length, 32);
  const pr = {...issue, title:"feat(platform): add multi-architecture support and optimize build efficiency"};
  assert.deepEqual(classify(pr, files, catalog), ["area: ci", "area: data", "area: documentation", "area: installer",
    "area: launcher", "area: localization", "area: platform", "area: tests", "area: updates", "type: feature", "type: security"]);
  pr.labels = ["type: feature", "area: launcher", "area: localization", "area: data", "area: installer",
    "area: updates", "update: recommended"].map(name => ({name}));
  assert.deepEqual(classify(pr, files, catalog), ["area: ci", "area: documentation", "area: platform", "area: tests", "type: security"]);
});

test("classifies architecture, runtime and platform paths", () => {
  for (const path of ["Installer/architectures.json", "Installer/assert-payload.ps1",
    "PresetLauncherWinUI3/Properties/PublishProfiles/win-x64.pubxml", "PresetLauncherWinUI3/app.manifest",
    "PresetLauncherWinUI3/Views/NativeWindow.cs", "tests/PresetLauncher.Tests/ArchitectureUpdateTests.cs"]) {
    assert.ok(classify(issue, [path], catalog).includes("area: platform"), path);
  }
  assert.deepEqual(classify(issue, ["Installer/banner.bmp"], catalog), ["area: installer", "type: bug"]);
});

test("identifies signing, protected state, and download verification as security-sensitive", () => {
  for (const path of [".circleci/sign_update.py", ".circleci/release.py", ".circleci/update-policy-public.pem",
    "PresetLauncherWinUI3/Services/UpdatePolicySignature.cs", "PresetLauncherWinUI3/Services/UpdatePolicyStore.cs",
    "PresetLauncherWinUI3/Services/UpdateCoordinator.cs", "PresetLauncherWinUI3/Services/UpdateService.cs"]) {
    const initial = classify(issue, [path], catalog);
    assert.ok(initial.includes("type: security"), path);
    assert.ok(initial.includes("area: updates"), path);
    assert.ok(initial.includes("type: bug"), path);
  }
  assert.deepEqual(classify(issue, [".circleci/test_sign_update.py", ".circleci/fixtures/signed-update.json"], catalog),
    ["area: ci", "area: tests", "type: bug"]);
  assert.deepEqual(classify({...issue, title:"docs: explain update signing"}, ["SECURITY.md", "docs/RELEASING.md"], catalog),
    ["type: documentation"]);
});

test("recognizes persistence beyond DataService and avoids duplicate update aliases", () => {
  assert.deepEqual(classify({...issue, labels:[{name:"AREA: UPDATER"}]}, ["PresetLauncherWinUI3/Services/AtomicJson.cs",
    "PresetLauncherWinUI3/Services/DataBackup.cs", "PresetLauncherWinUI3/Services/ExportPath.cs",
    "PresetLauncherWinUI3/Services/UpdatePolicyStore.cs"], catalog), ["area: data", "type: bug", "type: security"]);
});

const manual = {
  repository:{full_name:repo}, action:"created", sender:{login:"JimMarley420", type:"User"}, issue:{number:16},
  comment:{body:"/label-this", user:{login:"JimMarley420"}, author_association:"OWNER"}
};
const automatic = {
  repository:{full_name:repo}, action:"opened", number:16,
  pull_request:{number:16, base:{repo:{full_name:repo}}}, sender:{login:"contributor", type:"User"}
};

function deliver(event, payload) {
  const body = typeof payload === "string" ? payload : JSON.stringify(payload);
  const signature = "sha256=" + createHmac("sha256", env.WEBHOOK_SECRET).update(body).digest("hex");
  return handle(new Request("https://example.org/github", {method:"POST", body, headers:{
    "X-Hub-Signature-256":signature, "X-GitHub-Event":event, "X-GitHub-Delivery":"repeat-delivery"
  }}), env);
}

test("automatic and manual refresh use current files and labels on repeated deliveries", async ctx => {
  const current = {...issue, state:"open", labels:["area: ui", "type: feature", "update: required", "priority: high"].map(name => ({name}))};
  let files = [{filename:"Installer/architectures.json"}];
  const writes = [];
  const mock = ctx.mock.method(globalThis, "fetch", async (url, options) => {
    const path = url.slice(("https://api.github.com/repos/" + repo).length);
    switch (path) {
      case "/issues/16": return Response.json(current);
      case "/pulls/16/files?per_page=100&page=1": return Response.json(files);
      case "/labels?per_page=100&page=1": return Response.json(catalog.map(name => ({name})));
      case "/issues/16/labels": {
        assert.equal(options.method, "POST");
        const {labels} = JSON.parse(options.body);
        writes.push(labels);
        current.labels.push(...labels.map(name => ({name})));
        return Response.json(current.labels);
      }
      default: assert.fail(path);
    }
  });
  assert.deepEqual((await (await deliver("pull_request", automatic)).json()).added, ["area: installer", "area: platform"]);
  assert.deepEqual((await (await deliver("pull_request", automatic)).json()).added, []);
  assert.equal(mock.mock.callCount(), 6);
  files = [{filename:"PresetLauncherWinUI3/Services/UpdatePolicyStore.cs", previous_filename:"docs/old-store.md"}];
  assert.deepEqual((await (await deliver("pull_request", {...automatic, action:"synchronize"})).json()).added,
    ["area: data", "area: documentation", "area: updates", "type: security"]);
  assert.deepEqual((await (await deliver("pull_request", {...automatic, action:"reopened"})).json()).added, []);
  files = [{filename:".circleci/smoke-installer.ps1"}];
  assert.deepEqual((await (await deliver("issue_comment", manual)).json()).added, ["area: ci", "area: tests"]);
  assert.deepEqual((await (await deliver("issue_comment", manual)).json()).added, []);
  assert.equal(writes.length, 3);
  assert.deepEqual(current.labels.slice(0, 4).map(label => label.name), ["area: ui", "type: feature", "update: required", "priority: high"]);
});

test("rejects or ignores malformed and unauthorized deliveries before any GitHub API calls", async ctx => {
  const mock = ctx.mock.method(globalThis, "fetch", () => assert.fail("unexpected API call"));
  for (const payload of ["{", "null", "[]", "true"]) {
    assert.equal((await deliver("pull_request", payload)).status, 400);
  }
  for (const [event, payload] of [
    ["issue_comment", {...manual, comment:{...manual.comment, body:42}}],
    ["issue_comment", {...manual, sender:{login:"contributor", type:"User"}}],
    ["issue_comment", {...manual, sender:{...manual.sender, type:"Bot"}}],
    ["issue_comment", {...manual, comment:{...manual.comment, author_association:"NONE"}}],
    ["issue_comment", {...manual, comment:{...manual.comment, user:{login:"someone-else"}}}],
    ["pull_request", {...automatic, action:"labeled"}],
    ["pull_request", {...automatic, action:"edited"}],
    ["pull_request", {...automatic, number:"16"}],
    ["pull_request", {...automatic, number:17}],
    ["pull_request", {...automatic, pull_request:{number:16, base:{repo:{full_name:"other/repo"}}}}],
    ["pull_request", {...automatic, repository:{full_name:"other/repo"}}]
  ]) {
    assert.equal(await (await deliver(event, payload)).text(), "Ignored");
  }
  assert.equal(mock.mock.callCount(), 0);
});

test("refresh paginates both files and the label catalog before one additive write", async ctx => {
  const prefix = "https://api.github.com/repos/" + repo;
  const firstFiles = Array.from({length:100}, (_, index) => ({filename:`asset-${index}.png`}));
  const firstLabels = Array.from({length:100}, (_, index) => ({name:`status: sample-${index}`}));
  const reads = new Map([
    ["/issues/16", {...issue, state:"open", labels:[{name:"type: bug"}]}],
    ["/pulls/16/files?per_page=100&page=1", firstFiles],
    ["/pulls/16/files?per_page=100&page=2", [{filename:"Installer/architectures.json"}]],
    ["/labels?per_page=100&page=1", firstLabels],
    ["/labels?per_page=100&page=2", [{name:"area: platform"}]]
  ]);
  const mock = ctx.mock.method(globalThis, "fetch", async (url, options) => {
    const path = url.slice(prefix.length);
    if (options.method === "GET" && reads.has(path)) return Response.json(reads.get(path));
    assert.equal(path, "/issues/16/labels");
    assert.equal(options.method, "POST");
    assert.deepEqual(JSON.parse(options.body), {labels:["area: platform"]});
    return Response.json([{name:"area: platform"}]);
  });
  assert.deepEqual((await (await deliver("pull_request", automatic)).json()).added, ["area: platform"]);
  assert.equal(mock.mock.callCount(), 6);
});
