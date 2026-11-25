# On-demand issue and PR labels

Comment `/label-this` as JimMarley420 on an open issue or pull request. The Worker verifies GitHub's HMAC signature and adds existing area/type labels only. Existing categories are preserved. Priority, update policy, status and size remain manual. The response in GitHub webhook deliveries lists added labels. No CircleCI job or external AI is used.

## Deploy

From this directory, run `node --test worker.test.mjs` using Node 22 or newer, then `npx wrangler login` and `npx wrangler deploy`. Use `npx wrangler secret put GITHUB_TOKEN` and `npx wrangler secret put WEBHOOK_SECRET` to set secrets. Use a repository-scoped fine-grained PAT with Issues read/write, Pull requests read/write, Metadata read. Do not grant Contents write or reuse the release token.

In GitHub repository Settings > Webhooks, add the Worker URL ending in /github, content type application/json, the same webhook secret, and only Issue comments events. Verify SSL is enabled. Test the ping and a real maintainer comment; inspect Recent Deliveries for the response.

The Worker queries the live label catalog and never creates labels. Unknown categories are left unassigned. Label rules are conservative and not a semantic AI classifier. Repeated delivery is additive; no automated comment is posted. Rotate expired tokens in Cloudflare, not in the repository.
