# Security policy

This is a teaching sample for a fictional retailer (Northstar Outfitters). It is **not** production-ready; see "Known limits" in the README.

## Secrets

- The repository contains no secrets, tenant IDs, subscription IDs or real customer data. `appsettings.example.json` holds placeholders only.
- Keep real values in [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) or environment variables, never in committed files. `.gitignore` excludes `appsettings.Local.json`, `appsettings.Production.json`, `secrets.json` and `.env`.
- If you accidentally commit a secret, rotate it first, then remove it from history.

## Reporting a vulnerability

Please do not open a public issue for a security problem. Use GitHub's private
"Report a vulnerability" option on the repository's Security tab. If it is not enabled, open an issue asking for a private contact without including details.

## Design choices worth knowing

- The host fails closed: it refuses to start unless `Host:AllowAnonymous` is explicitly `true`. The sample ships **no** token validation.
- Actions check order ownership and return a limited projection. "Not yours" and "does not exist" look identical to the customer.
- Logs record route, duration and conversation id, not message text.
