# Teams and private chat (2026-10-09)

Point-in-time record of the choices made while building phase C. Not kept in sync with the code.

| Topic | Decision | Why |
|---|---|---|
| Group chat | Teams, each on its own Meshtastic channel name | No worldwide channel; a team's own channel keeps its chat out of public channels |
| Team ↔ channel | Matched by channel name from the gateway's MQTT topic; names unique, public presets refused | Meshtrail never knows channel keys; the name is all the server sees |
| Joining | 8-character join code, renewable by the owner | Identity provider not chosen yet; simple to share by voice or message |
| Sending team chat | Same packet through every online gateway that uplinked the channel in the last 24 h | Reach the whole team wherever its gateways are; overlapping copies are dropped by the nodes |
| TCP gateways | Not used for team chat | Over TCP we only know channel numbers, not names |
| Conversation privacy | Visible to the node's verified owner and everyone who wrote to it | People who never talked to a node have no business reading its messages |
| Realtime | Messages pushed to exactly those users (SignalR `Clients.Users`) | Same rule as the queries; nothing private goes to everyone |
| Others' conversations | Empty page (not 404) | Opening a new conversation is the normal case and must not look like an error |
