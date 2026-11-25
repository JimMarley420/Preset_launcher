const REPO = "JimMarley420/Preset_launcher";
const API = "https://api.github.com/repos/" + REPO;
const encoder = new TextEncoder();
const releaseSecurity = /^\.circleci\/(?:policy\.py|release\.py|sign_update\.py|update-policy-public\.pem)$/i;
const updateVerification = /^PresetLauncherWinUI3\/Services\/Update(?:Service|Coordinator|PolicySignature|PolicyStore)\.cs$/i;
const documentation = /(?:^docs\/|\.md$)/i;
const passwordService = /^PresetLauncherWinUI3\/Services\/PasswordService\.cs$/i;
const sdkChecksum = /^\.circleci\/dotnet-sdk\.sha512$/i;
const webhookImplementation = /^tools\/auto-labeler\/worker\.mjs$/i;
const dependencyManifest = /(?:\.(?:csproj|fsproj|vbproj)$|(?:^|\/)(?:global\.json|nuget\.config|packages\.(?:config|lock\.json)|directory\.(?:packages\.props|build\.(?:props|targets))|(?:package|package-lock|npm-shrinkwrap)\.json|yarn\.lock|pnpm-lock\.yaml|bun\.lockb?)$)/i;

const areas = [
  ["ui", /(?:\/views\/|\.xaml$|mainwindow)/i, /\b(interface|appearance|theme|dark mode|window|xaml|apparence|thème)\b/i],
  ["updates", p => updateVerification.test(p) || releaseSecurity.test(p), /\b(update|updater|mise à jour|upgrade)\b/i],
  ["installer", /^installer\//i, /\b(installer|installation|uninstall|désinstallation)\b/i],
  ["presets", /preseteditor|presetspage|\/preset\.cs$/i, /\b(presets?|préréglages?)\b/i],
  ["launcher", /launcherservice|executableitem/i, /\b(launch(?:ing)?|executables?|running processes|lancement)\b/i],
  ["processes", /^PresetLauncherWinUI3\/(?:Services\/LauncherService|Views\/ProcessSelector)\.cs$/i, /\b(?:(?:clos(?:e|ing)|detect(?:ing)?|terminat(?:e|ing)|kill(?:ing)?)\s+(?:processes|process|programs?)|process (?:detection|termination)|processus)\b/i],
  ["dependencies", p => sdkChecksum.test(p) || dependencyManifest.test(p), /\b(dependenc(?:y|ies)|nuget|sdk version|lockfile)\b/i],
  ["data", /^PresetLauncherWinUI3\/(?:Models\/(?:AppData|AppSettings)|Services\/(?:AtomicJson|DataBackup|DataService|ExportPath|SettingsService|UpdatePolicyStore))\.cs$/i, /\b(import|export|backup|json|sauvegarde|data loss)\b/i],
  ["settings", /settings(?:page|service)|appsettings/i, /\b(settings|preferences|paramètres)\b/i],
  ["localization", /localizationservice/i, /\b(translation|localization|language|traduction|langue)\b/i],
  ["history", /historypage|executionhistory/i, /\b(history|historique)\b/i],
  ["statistics", /statisticspage|usagestat/i, /\b(statistics|statistiques)\b/i],
  ["ci", /^(?:\.circleci|tools\/auto-labeler)\//i, /\b(circleci|pipeline|ci\/cd)\b/i],
  ["documentation", documentation, /\b(readme|documentation)\b/i],
  ["tests", /^(?:tests\/|\.circleci\/(?:test_[^/]+\.py$|smoke-[^/]+\.ps1$|fixtures\/))|\.test\.mjs$/i, /\b(unit tests?|regression tests?|coverage)\b/i],
  ["platform", /^(?:Installer\/(?:architectures\.json|assert-payload\.ps1|build\.ps1|PresetLauncher\.iss)|PresetLauncherWinUI3\/(?:PresetLauncherWinUI3\.csproj|app\.manifest|Properties\/PublishProfiles\/[^/]+\.pubxml|Services\/WindowsSystemDispatcherQueueHelper\.cs|Views\/NativeWindow\.cs)|tests\/PresetLauncher\.Tests\/ArchitectureUpdateTests\.cs)$/i, /\b(arm64|x64|x86|architecture|runtime compatibility)\b/i]
];
const aliases = {ui:["ux","ui/ux","interface"], updates:["updater","update"], installer:["installation"], presets:["preset"], launcher:["launching","launch"], data:["storage","import/export"], settings:["configuration"], localization:["i18n","translations"], statistics:["stats"], ci:["ci/cd","build"], documentation:["docs"], tests:["testing"]};
const types = {fix:["bug","fix"],feat:["feature","enhancement"],perf:["performance","enhancement"],docs:["documentation","docs"],ci:["ci","maintenance"],build:["build","maintenance"],test:["test","tests","maintenance"],refactor:["refactor"],chore:["maintenance","chore"],style:["style","maintenance"],revert:["revert","maintenance"]};

function labelCandidates(issue, paths) {
  const existing = new Set(issue.labels.map(l => l.name.toLowerCase()));
  const candidates = [];
  const add = (prefix, options) => {
    const labels = options.map(name => prefix + name);
    if (labels.length && !labels.some(name => existing.has(name))) candidates.push(labels);
  };
  const title = issue.title || "", body = issue.pull_request ? "" : issue.body || "";
  const kind = title.match(/^([a-z]+)(?:\([^)]*\))?!?: /)?.[1];
  const formArea = body.match(/^### (?:Affected|Related) area\s*\r?\n\s*([^\r\n]+)/im)?.[1] || "";
  const hasType = [...existing].some(name => name.startsWith("type:") && name !== "type: security");
  const documentationType = existing.has("type: documentation") || existing.has("type: docs") || (!hasType && kind === "docs");
  for (const [area, pathRule, textRule] of areas) {
    if (area === "documentation" && documentationType) continue;
    const matched = paths.some(p => typeof pathRule === "function" ? pathRule(p) : pathRule.test(p));
    if (matched || (!issue.pull_request && textRule.test(title + " " + formArea))) add("area: ", [area, ...(aliases[area] || [])]);
  }
  const securityPath = p => updateVerification.test(p) || releaseSecurity.test(p) || passwordService.test(p) ||
    sdkChecksum.test(p) || webhookImplementation.test(p);
  if (paths.some(securityPath)) add("type: ", ["security"]);
  if (!hasType) {
    let inferred = Object.hasOwn(types, kind) ? types[kind] : [];
    if (!inferred.length && !issue.pull_request) {
      if (/^### Steps to reproduce\s*$/im.test(body)) inferred = types.fix;
      else if (/^### Proposed change\s*$/im.test(body)) inferred = types.feat;
    }
    add("type: ", inferred);
  }
  return candidates;
}

function availableLabels(candidates, available) {
  const names = new Map(available.map(n => [n.toLowerCase(), n]));
  const selected = candidates.map(options => options.map(name => names.get(name)).find(Boolean));
  return [...new Set(selected.filter(Boolean))].sort();
}

export function classify(issue, paths, available) {
  return availableLabels(labelCandidates(issue, paths), available);
}
export async function validSignature(body, secret, signature) {
  if (!secret || !/^sha256=[0-9a-f]{64}$/.test(signature || "")) return false;
  const key = await crypto.subtle.importKey("raw", encoder.encode(secret), {name:"HMAC",hash:"SHA-256"}, false, ["verify"]);
  const bytes = Uint8Array.from(signature.slice(7).match(/../g), h => parseInt(h,16));
  return crypto.subtle.verify("HMAC", key, bytes, body);
}
async function github(env, path, method="GET", body) {
  const response = await fetch(API + path, {method, redirect:"manual", headers:{
    Authorization:"Bearer " + env.GITHUB_TOKEN, Accept:"application/vnd.github+json",
    "User-Agent":"PresetLauncher-labeler", "X-GitHub-Api-Version":"2022-11-28", "Content-Type":"application/json"
  }, ...(body ? {body:JSON.stringify(body)} : {})});
  if (!response.ok) throw new Error("GitHub API failed: " + method + " " + path + " (" + response.status + ")");
  return response.json();
}
async function pages(env,path,limit) {
  const all=[];
  for(let page=1;page<=limit;page++){
    const items=await github(env,path+"?per_page=100&page="+page);
    if(!Array.isArray(items)) throw new Error("Expected list");
    all.push(...items);
    if(items.length<100) return all;
  }
  throw new Error("Pagination limit reached");
}

function authorizedNumber(event, payload) {
  if (payload.repository?.full_name !== REPO) return;
  if (event === "pull_request") {
    if (!["opened", "reopened", "synchronize"].includes(payload.action)) return;
    if (payload.pull_request?.number !== payload.number || payload.pull_request?.base?.repo?.full_name !== REPO) return;
    return payload.number;
  }
  const {action, comment, sender, issue} = payload;
  if (action === "created" && typeof comment?.body === "string" && comment.body.trim() === "/label-this" &&
      sender?.login === "JimMarley420" && sender.type === "User" && comment.user?.login === sender.login &&
      ["OWNER", "MEMBER", "COLLABORATOR"].includes(comment.author_association)) return issue?.number;
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
  if(event!=="issue_comment" && event!=="pull_request") return new Response("Ignored");
  let p;
  try { p=JSON.parse(new TextDecoder().decode(body)); } catch { return new Response("Invalid JSON",{status:400}); }
  if (!p || typeof p !== "object" || Array.isArray(p)) return new Response("Invalid JSON object",{status:400});
  const number=authorizedNumber(event,p);
  if (!Number.isSafeInteger(number) || number < 1) return new Response("Ignored");
  const issue=await github(env,"/issues/"+number);
  if(issue.state!=="open") return new Response("Closed issue");
  if(event==="pull_request" && !issue.pull_request) return new Response("Ignored");
  // GitHub caps the file list at 3,000 entries. Allow a final empty page.
  const files=issue.pull_request ? await pages(env,"/pulls/"+number+"/files",31) : [];
  const paths=files.flatMap(f=>[f.filename,...(f.previous_filename?[f.previous_filename]:[])]);
  const candidates=labelCandidates(issue,paths);
  // Reserve one issue read, 31 file pages and one label write within 50 subrequests.
  const available=candidates.length ? (await pages(env,"/labels",17)).map(l=>l.name) : [];
  const selected=availableLabels(candidates,available);
  if(selected.length) await github(env,"/issues/"+number+"/labels","POST",{labels:selected});
  // Labels are additive and idempotent. No bot comment avoids replay spam.
  return Response.json({added:selected,priority:"manual",update:"manual"});
}
export default {async fetch(request,env) {
  try { return await handle(request,env); }
  catch(error) { console.error("Label request failed",error.message); return new Response("Label request failed",{status:502}); }
}};
