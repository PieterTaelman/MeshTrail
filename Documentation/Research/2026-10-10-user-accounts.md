# User accounts (2026-10-10)

Point-in-time record of the choices made while adding accounts and the profile page. Not kept in sync with the code.

| Topic | Decision | Why |
|---|---|---|
| Identity | Own accounts: email + password, confirmed by mail | No identity provider chosen yet; the user asked for register → confirm email → profile |
| Passwords | ASP.NET Core Identity's `PasswordHasher` (package `Microsoft.Extensions.Identity.Core`) without the rest of Identity | Proven PBKDF2 with a versioned format; full Identity would bring its own tables and UI |
| Tokens | Our own HS256 JWT (12 h) in localStorage, sent as Bearer; SignalR gets it as `access_token` | Works with the existing JWT bearer setup; can move to an external provider later by changing the token validation |
| Mail | SMTP (MailPit locally through Aspire); log the mail when no server is configured | Flows can be tried without Docker; production picks a real SMTP server |
| Enumeration | Register / resend / forgot always answer 204 | Nobody can find out who has an account |
| Public map | Map, node list, node detail and gateway list/summary are anonymous; the rest needs an account | The user chose a public read-only map |
| Gateway identity | Adding a gateway means choosing its node; the login only works for that node | "No IP address?" felt strange; an IP cannot identify a gateway that connects to a central server, the node id can |
| Duplicates | A node can be an active gateway once; remove first, then add again | Asked by the user; avoids two owners for one radio |
| First node | The gateway's first uplink registers its node to the owner | A code DM needs a gateway, so the first node could never be registered with a code |
| Tests | `Development` mode accepts real tokens and the `X-Dev-User` header | Integration tests act as several people without signing in each time |
