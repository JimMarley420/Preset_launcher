const REPO = "JimMarley420/Preset_launcher";
const API = "https://api.github.com/repos/" + REPO;
const encoder = new TextEncoder();

const areas = [
  ["ui", /(?:\/views\/|\.xaml$|mainwindow)/i, /\b(interface|appearance|theme|dark mode|window|xaml|apparence|thème)\b/i],
  ["updater", /update(?:service|coordinator)|\/installer\//i, /\b(update|updater|mise à jour|upgrade)\b/i],
  ["installer", /^installer\//i, /\b(installer|installation|uninstall|désinstallation)\b/i],
  ["presets", /preseteditor|presetspage|\/preset\.cs$/i, /\b(presets?|préréglages?)\b/i],
  ["launcher", /launcherservice|executableitem/i, /\b(launch|processes?|executables?|lancement|processus)\b/i],
  ["data", /dataservice|appdata/i, /\b(import|export|backup|json|sauvegarde|data loss)\b/i],
  ["settings", /settings(?:page|service)|appsettings/i, /\b(settings|preferences|paramètres)\b/i],
  ["localization", /localizationservice/i, /\b(translation|localization|language|traduction|langue)\b/i],
  ["history", /historypage|executionhistory/i, /\b(history|historique)\b/i],
  ["statistics", /statisticspage|usagestat/i, /\b(statistics|statistiques)\b/i],
  ["ci", /^\.circleci\//i, /\b(circleci|pipeline|ci\/cd)\b/i],
  ["documentation", /(?:^docs\/|\.md$)/i, /\b(readme|documentation)\b/i],
  ["tests", /^tests\//i, /\b(unit tests?|regression tests?|coverage)\b/i],
  ["security", /passwordservice/i, /\b(authentication|password|mot de passe)\b/i]
];
const aliases = {ui:["ui","ux","ui/ux","interface"], updater:["updater","updates","update"], installer:["installer","installation"], presets:["presets","preset"], launcher:["launcher","launching","launch","processes"], data:["data","storage","import/export"], settings:["settings","configuration"], localization:["localization","i18n","translations"], history:["history"], statistics:["statistics","stats"], ci:["ci","ci/cd","build"], documentation:["documentation","docs"], tests:["tests","testing"], security:["security"]};
export function classify(issue, paths, available) {
  const names = new Map(available.map(n => [n.toLowerCase(), n]));
  const existing = new Set(issue.labels.map(l => l.name.toLowerCase()));
  const result = new Set();
  const has = prefix => [...existing].some(n => n.startsWith(prefix));
  const pick = (prefix, options) => options.map(n => names.get(prefix + n)).find(Boolean);
  const add = n => { if (n && !existing.has(n.toLowerCase())) result.add(n); };
  const title = issue.title || "", body = issue.body || "";
  const formArea = body.match(/^### (?:Affected|Related) area\s*\r?\n\s*([^\r\n]+)/im)?.[1] || "";
  if (!has("area:")) {
    for (const [area, pathRule, textRule] of areas) {
      if (paths.some(p => pathRule.test(p)) || (!issue.pull_request && textRule.test(title + " " + formArea))) add(pick("area: ", aliases[area]));
    }
  }
  if (!has("type:")) {
    const kind = title.match(/^([a-z]+)(?:\([^)]*\))?!?: /)?.[1];
    const types = {fix:["bug","fix"],feat:["feature","enhancement"],perf:["performance","enhancement"],docs:["documentation","docs"],ci:["ci","maintenance"],build:["build","maintenance"],test:["test","tests"],refactor:["refactor"],chore:["maintenance","chore"],style:["style"],revert:["revert"]};
    const inferred = types[kind] || (!issue.pull_request && /^### Steps to reproduce\s*$/im.test(body) ? ["bug","fix"] : !issue.pull_request && /^### Proposed change\s*$/im.test(body) ? ["feature","enhancement"] : []);
    add(pick("type: ", inferred));
  }
  return [...result].sort();
}
export async function validSignature(body, secret, signature) {
  if (!secret || !/^sha256=[0-9a-f]{64}$/.test(signature || "")) return false;
  const key = await crypto.subtle.importKey("raw", encoder.encode(secret), {name:"HMAC",hash:"SHA-256"}, false, ["verify"]);
  const bytes = Uint8Array.from(signature.slice(7).match(/../g), h => parseInt(h,16));
  return crypto.subtle.verify("HMAC", key, bytes, body);
}
async function github(env, path, method="GET", body) {
  const response = await fetch(API + path, {method, redirect:"error", headers:{
    Authorization:"Bearer " + env.GITHUB_TOKEN, Accept:"application/vnd.github+json",
    "User-Agent":"PresetLauncher-labeler", "X-GitHub-Api-Version":"2022-11-28", "Content-Type":"application/json"
  }, ...(body ? {body:JSON.stringify(body)} : {})});
  if (!response.ok) throw new Error("GitHub API failed: " + response.status);
  return response.json();
}
async function pages(env,path,limit=30) {
  const all=[];
  for(let page=1;page<=limit;page++){
    const items=await github(env,path+"?per_page=100&page="+page);
    if(!Array.isArray(items)) throw new Error("Expected list");
    all.push(...items);
    if(items.length<100) return all;
  }
  throw new Error("Pagination limit reached");
}
export async function handle(request,env) {
  const url=new URL(request.url);
  if(url.pathname!=="/github") return new Response("Not found",{status:404});
  if(request.method!=="POST") return new Response("Method not allowed",{status:405});
  if(!env.GITHUB_TOKEN || !env.WEBHOOK_SECRET) return new Response("Not configured",{status:503});
  const body=await request.arrayBuffer();
  if(body.byteLength>1048576) return new Response("Too large",{status:413});
  if(!await validSignature(body,env.WEBHOOK_SECRET,request.headers.get("X-Hub-Signature-256"))) return new Response("Unauthorized",{status:401});
  const event=request.headers.get("X-GitHub-Event");
  if(event==="ping") return new Response("pong");
  if(event!=="issue_comment") return new Response("Ignored");
  let p;
  try { p=JSON.parse(new TextDecoder().decode(body)); } catch { return new Response("Invalid JSON",{status:400}); }
  if(p.repository?.full_name!==REPO || p.action!=="created" || p.comment?.body?.trim()!=="/label-this" || p.sender?.login!=="JimMarley420" || p.comment?.user?.login!==p.sender.login || p.sender?.type!=="User" || !["OWNER","MEMBER","COLLABORATOR"].includes(p.comment.author_association) || !Number.isSafeInteger(p.issue?.number) || p.issue.number<1) return new Response("Ignored");
  const number=p.issue.number;
  const issue=await github(env,"/issues/"+number);
  if(issue.state!=="open") return new Response("Closed issue");
  const available=(await pages(env,"/labels")).map(l=>l.name);
  const files=issue.pull_request ? await pages(env,"/pulls/"+number+"/files",31) : [];
  const paths=files.flatMap(f=>[f.filename,...(f.previous_filename?[f.previous_filename]:[])]);
  const selected=classify(issue,paths,available);
  if(selected.length) await github(env,"/issues/"+number+"/labels","POST",{labels:selected});
  // Labels are additive and idempotent. No bot comment avoids replay spam.
  return Response.json({added:selected,priority:"manual",update:"manual"});
}
export default {async fetch(request,env) {
  try { return await handle(request,env); }
  catch(error) { console.error("Label request failed",error.message); return new Response("Label request failed",{status:502}); }
}};
