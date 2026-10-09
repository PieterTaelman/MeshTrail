# Multi-gateway core (2026-10-06)

Point-in-time record of the choices made while building phase B. Not kept in sync with the code.

| Topic | Decision | Why |
|---|---|---|
| Gateway login check | The broker asks the API (`POST api/v1/mqtt/auth`, service key header), caches 1 min | The API owns the registry; brokers stay stateless and can run per region |
| Which gateway sent an uplink | Broker stamps the login as MQTT 5 user property `meshtrail-gateway` (removes a forged one) | MQTT does not tell subscribers who published |
| Proof of ownership | First uplink binds the login to its `gateway_id`; a login never moves | Simple; documented limit: a custom client could claim any id first |
| Downlink to one gateway | User property `meshtrail-target`; broker delivers only to that login | Otherwise every gateway on the channel would transmit it |
| Gateway online status | Broker publishes connected logins (on change + every 30 s) on `meshtrail/broker/gateways` | Immediate offline on disconnect, survives API restarts |
| Gateway key | Surrogate `Id`; `NodeNum` null while Pending, unique among active gateways | Credentials exist before we know the node |
| Routing | Online gateways that heard the node < 6 h; fresh (30 min) first, then hops, SNR, recency | A packet only travels a few hops around its gateway |
| Dedupe / throttle | In memory: sender+packet id 10 min; heard per node+gateway 1 min | Avoid database writes per duplicate; single API instance for now |
| Worldwide channel | Removed (DMs only); channel messages stored for team chat later | Physically impossible to broadcast to the world |
| Realtime | SignalR groups per 5° tile, `WatchArea` from the client | Avoid pushing every node change to every browser |
| Config | `Meshtastic:Gateway` renamed to `Meshtastic:Tcp`; `Mqtt` and `Simulator` sections | One transport per way in |
