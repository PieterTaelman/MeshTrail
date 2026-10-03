# Prompt for Claude Code — Meshtrail mesh platform (phases 0–3)

You are working in the Meshtrail repo. Read `AGENTS.md` first and follow it strictly (no SysLib, no MediatR,
Mediator source generator only in `Meshtrail.WebApi`, SSDT schema, 0 warnings). Before each layer, read the matching
skill from the routing table in `AGENTS.md`. Copy the shape of the `Samples` module; do not invent new patterns.

**Work in plan mode first:** read the codebase, then show me a plan (projects, tables, use cases, endpoints,
Angular features, test list). Wait for my approval before writing code. Then implement **phase by phase**. After each
phase: `dotnet build Meshtrail.slnx` (0 warnings), unit + integration tests green, `npm run lint && npm test` green,
then a Conventional Commit. Stop and report after every phase so I can test with real hardware.

## Goal

Meshtrail is an offline navigation and emergency platform. The server connects to **one Meshtastic gateway node on my
WiFi** and uses it to talk to the LoRa mesh. Through the platform we can:

1. discover all Meshtastic nodes the gateway hears,
2. register (claim + verify) nodes to users,
3. send and receive messages (channel broadcast and direct messages),
4. detect SOS signals ("Beacons", the product name for SOS) from any node and show them on a central topo map.

The central map is a **common operating picture**: mesh nodes are the first layer. Later phases add consented
person tracking and external feeds (CAP / CoT / GeoJSON). Do **not** build those now, but make sure nothing in the
design blocks them (generic map-layer API, `Source` fields, etc.).

It must interoperate with **every** Meshtastic device (stock firmware ≥ 2.5), not only Meshtrail devices.

## Hardware facts

- Gateway node: M5Stack Unit C6L (ESP32-C6 + SX1262), stock Meshtastic firmware, WiFi enabled.
  Node id `!f115aaec`, node num `4044729068`, long name "Node 2109", short name "2109". Region EU_868.
- The node's IP address goes in configuration (`Meshtastic:Gateway:Host`, port `4403`) — user secrets / appsettings
  Development, never committed.
- I have a second C6L that acts as a "hiker" node for testing.

## Architecture decisions (already taken)

- **Transport = Meshtastic TCP stream API**, port 4403. Frame: `0x94 0xC3` + 2-byte big-endian length (max 512) +
  protobuf. Send `ToRadio{want_config_id}` on connect to receive MyNodeInfo, NodeInfo (NodeDB), config and
  `config_complete_id`. Then stream `FromRadio.packet`. Send `ToRadio.heartbeat` every ~5 minutes or the node drops the
  connection. Only ONE TCP client can be connected at a time (the phone app will kick us off) — handle disconnects
  with exponential backoff reconnect and expose gateway status (Connecting / Online / Offline + last error).
- **Protocol:** vendor the official `meshtastic/protobufs` `.proto` files (pin a tag, note it in docs) and generate C#
  with `Google.Protobuf` + `Grpc.Tools` (nuget.org). If you prefer the official `Meshtastic` NuGet package instead,
  justify it in the plan (dependency size, license, maintenance).
- **Abstraction:** `IMeshRadio` (connect, events for received packets/node info, `SendAsync`) with two implementations:
  `TcpMeshRadio` and `SimulatedMeshRadio`. `Meshtastic:Gateway:Mode = Tcp | Simulated`. The simulator produces a few
  fake nodes around Belgium, random positions/telemetry, text messages and (via a dev endpoint or Aspire dashboard
  command) a fake SOS — so the whole app runs and the tests run without hardware. Default Development mode: Simulated.
- **Hosting:** the Mediator source generator may only live in `Meshtrail.WebApi`, so the gateway runs as a
  `BackgroundService` **hosted in WebApi**, with all radio/protocol code in a new library
  `Code/Libraries/Meshtrail.Mesh` (framing, protobufs, `IMeshRadio`, simulator, packet → message translation).
  It must be fully isolated: a radio failure may never crash the API or fail `/health/ready` (report gateway state as a
  separate, non-blocking health check). The worker turns incoming packets into Mediator commands
  (`RecordNodeHeard`, `RecordPosition`, `ReceiveTextMessage`, `RecordAck`, …) inside a fresh DI scope per packet.
- **Outbound:** sending is a command that stores a `MeshMessage` with status `Queued`. An outbound dispatcher
  (`Channel<T>` signalled by the handler, plus a DB poll on startup for leftovers) sends it with `want_ack = true`,
  stores the packet id → `Sent`. A `ROUTING_APP` packet whose `request_id` matches → `Acked` (or `Failed` with the
  routing error). No ack after a timeout → `Failed`. Respect the EU868 duty cycle: a simple rate limit
  (configurable, default max 1 outbound packet per 10 s) — never flood the mesh.
- **Realtime:** reuse the existing SignalR hub `/hubs/notifications` (server → client only) with new event names
  (`NodeUpdated`, `MessageReceived`, `MessageStatusChanged`, `BeaconRaised`, `BeaconUpdated`, `GatewayStatusChanged`).
- **Map:** MapLibre GL JS (`maplibre-gl`) in Angular. Topo style URL is configuration (default: an OpenTopoMap raster
  style for development; offline PMTiles come in a later phase). Wrap MapLibre in one component/service so it can be
  swapped.

## Domain (bounded context "Mesh"; same DDD rules as Samples)

| Entity | Notes |
|---|---|
| `MeshGateway` | node num, status, last connected, firmware version |
| `MeshNode` | NodeNum (uint, key), NodeId (`!xxxxxxxx`), LongName, ShortName, HwModel, PublicKey (32 bytes), Role, LastHeard, SNR, RSSI, HopsAway, BatteryLevel, Voltage, LastPosition (lat/lon/alt/time/precision). Created on first sight ("discovered"). |
| `NodePosition` | position history per node (for trails later); retention configurable |
| `NodeRegistration` | node ↔ user. States: `Claimed → Verified`, or `Revoked`. Verification code: 6 digits, sent by DM over the mesh, expires after 15 min, max 5 attempts — rules in the entity |
| `MeshMessage` | channel index or destination node, from node, text (≤ 200 UTF-8 bytes — constant in the domain, validator reuses it), direction, packet id, hop info, status `Queued → Sent → Acked / Failed`, timestamps |
| `Beacon` | source node, position (+ accuracy/age), trigger (`InteropText`, `InteropBell`, `MeshtrailStructured`), message text, status `Active → Acknowledged → Resolved` or `Cancelled`; acknowledged-by/assigned-to user; timeline of events (`BeaconEvent`). Same node sending SOS again while active = update the existing beacon, not a new one |

Keep the Mesh context free of map/intel concerns: the map layer API reads from it.

## Beacon (SOS) detection — in `Meshtrail.Mesh`, unit tested

1. **Interop text:** `TEXT_MESSAGE_APP` whose trimmed text starts with `SOS` (case-insensitive, word boundary) →
   `InteropText`.
2. **Interop bell:** text contains the bell character `U+0007` (used by Meshtastic canned messages / external
   notification) → `InteropBell`.
3. **Meshtrail structured:** our own `MeshtrailBeacon` protobuf (lat, lon, accuracy, kind, free text, cancel flag)
   on portnum `PRIVATE_APP` (256). Cancel flag → `Cancelled`.

Position = position in the packet if present, else the node's last known position (flag it as "last known" + age).
While a beacon is active, ask the node for a fresh position (position request) at a modest interval.

## Use cases / endpoints (`api/v1/...`)

- `GET gateway` — status. `POST gateway/reconnect`.
- `GET nodes` (paged, search by name/id, filter registered/online), `GET nodes/{nodeNum}`,
  `POST nodes/{nodeNum}/position-request`, `POST nodes/{nodeNum}/traceroute` (result arrives via SignalR + stored).
- `POST registrations/from-contact-url` — body: a `https://meshtastic.org/v/#…` URL (that is what the Meshtastic
  app's "share contact" QR code contains). Parse it: base64url → `SharedContact` protobuf (`node_num`, `user` with
  names + public key). Creates/updates the node and a `Claimed` registration for the current user, then sends the
  verification code by DM. `POST registrations/{id}/verify` (code), `DELETE registrations/{id}`.
- `GET messages?channel=…|node=…`, `POST messages` (channel or DM). DMs to registered nodes use the stored public
  key (firmware does PKC encryption when the destination's key is known).
- `GET beacons?status=…`, `GET beacons/{id}`, `POST beacons/{id}/acknowledge`, `/assign`, `/resolve`.
- `GET map/features?bbox=…&layers=nodes,beacons` — GeoJSON FeatureCollection, layer-generic so tracking/intel can be
  added as new layers later.
- Dev only: `POST dev/simulator/sos` (Simulated mode only).

## Angular (`Client-Web/src/app/features/…`, Optimus UI, Signals, see `Client-Web/AGENTS.md`)

One main screen, the **operations map**:
- top bar: gateway status chip, active-beacon counter (red, pulsing when > 0);
- left panel: layer toggles (Nodes, Beacons), node list with search, online dot (heard < 15 min), registered badge;
- center: topo map — nodes as markers (colour by last heard), beacons as large red markers with pulse, click → detail;
- right panel (detail): node info (SNR, hops, battery, last heard, position age) with buttons Message / Request
  position / Traceroute / Register; or beacon detail with timeline and Acknowledge / Assign / Resolve;
- bottom drawer: chat — channel tab + one tab per DM, message status icons (queued/sent/acked/failed), byte counter
  (200 max);
- beacon raised → full-width alert banner + sound (user can mute), map flies to it.
- registration dialog: paste the contact URL (QR scanning comes with the mobile app later), then enter the code.
All live updates via SignalR; no polling.

## Database

New tables via the `meshtrail-database-table` skill (add each to the `.sqlproj` AND `Scripts_Core.txt`):
`MeshGateway`, `MeshNode`, `NodePosition`, `NodeRegistration`, `MeshMessage`, `Beacon`, `BeaconEvent`.
NodeNum is stored as `bigint` (uint32 does not fit in `int`). Indexes for: node last heard, messages by
channel/node + time, beacons by status.

## Phases

0. **Spike:** `Meshtrail.Mesh` with framing + protobufs + `TcpMeshRadio` + `SimulatedMeshRadio`; a small console tool
   `tools/Meshtrail.MeshProbe` that connects to the node and prints MyNodeInfo, the NodeDB and incoming packets.
   Unit tests for the frame reader (split frames, garbage before the magic bytes, oversized length) and the
   contact-URL parser (use node `!f115aaec` / `4044729068` as the test case).
1. **Gateway + discovery:** hosted worker, Mesh domain/tables for gateway/nodes/positions, nodes endpoints, SignalR,
   Angular map + node list + node detail.
2. **Registration + messaging:** registrations, messages, outbound dispatcher with ACK tracking + rate limit, chat
   drawer, registration dialog.
3. **Beacon:** detector, Beacon aggregate + workflow, alert banner, beacon layer + detail.

## Tests (minimum)

- Unit: frame reader, contact-URL parser, beacon detector (all three triggers + non-matches like "SOSA", "the SOS"),
  `NodeRegistration` rules (expiry, attempts, wrong code), `Beacon` state machine, `MeshMessage` status transitions,
  outbound rate limiter (use `FakeTimeProvider`), packet → command translation.
- Integration: every endpoint above, with the radio replaced by a fake `IMeshRadio` that records sent packets and lets
  the test inject incoming packets (e.g. inject an SOS text → `GET beacons` returns an active beacon; send a message →
  inject routing ack → status `Acked`).

## Documentation

- Living doc `Documentation/Mesh/README.md`: architecture diagram (radio → worker → Mediator → DB → SignalR → map),
  domain model, state machines, beacon detection rules, configuration keys, how to connect a real node
  (enable WiFi on the node; note that WiFi disables Bluetooth on ESP32 and the phone app cannot be connected over
  TCP at the same time), and how to use the simulator. Link it from `Documentation/README.md`.
- Decision record `Documentation/Research/<date>-mesh-gateway-decisions.md`: TCP vs MQTT (MQTT = later, for multiple
  gateways), protobuf generation choice + pinned tag, hosting inside WebApi and why.
- Update `README.md` with the new configuration keys.

## Constraints

- Never log or store channel PSKs. Public keys are fine to store.
- Do not commit the node's IP or any secret.
- Message text and node names come from the radio = untrusted input: length-limit and never render as HTML.
- Keep comments short and junior-friendly, as `AGENTS.md` says.
