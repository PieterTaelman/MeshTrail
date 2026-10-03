# Mesh gateway decisions (2026-10-03)

## Context

Meshtrail talks to a LoRa mesh of Meshtastic devices (stock firmware ≥ 2.5, not only our own devices) through **one
gateway node** on the local WiFi: an M5Stack Unit C6L (ESP32-C6 + SX1262), node `!f115aaec`, region EU_868.
We had to choose how the server talks to that node, how we get the Meshtastic protocol into C#, and where the
radio code runs.

## 1. Transport: TCP stream API (now) vs MQTT (later)

| Option | Pros | Cons |
|---|---|---|
| **TCP stream API (port 4403)** | Direct, no broker; full control (send with `want_ack`, admin messages, NodeDB dump on connect); works fully offline | Only **one** TCP client per node (the phone app kicks us off); one gateway only |
| MQTT (node → broker) | Many gateways at once; firmware handles reconnects | Needs a broker; uplink/downlink per channel must be configured; weaker control over ACKs and admin; encrypted/JSON variants to handle |
| Serial / BLE | No network needed | Server must sit next to the node; BLE is fragile on servers |

**Decision:** TCP stream API. Frame = `0x94 0xC3` + 2-byte big-endian length (max 512) + protobuf. We send
`want_config_id` on connect, a heartbeat every 5 minutes, and reconnect with exponential backoff. MQTT is the
expected next step when we need more than one gateway; the `IMeshRadio` abstraction is the seam for it.

## 2. Protocol: vendored `.proto` files vs the `Meshtastic` NuGet package

| Option | Pros | Cons |
|---|---|---|
| **Vendor `meshtastic/protobufs` + `Google.Protobuf` + `Grpc.Tools`** | Exactly the protocol and nothing else; we choose the tag; small dependency (`Google.Protobuf` only at runtime) | We must bump the tag ourselves |
| `Meshtastic` NuGet (the .NET CLI library) | Some helpers included | It is the CLI's library (serial/CLI dependencies), last release 2.0.8, slower to follow firmware; same licence |

**Decision:** vendor the `.proto` files from tag **`v2.8.1`** (newest at the time; contains `SharedContact` and
`AdminMessage.add_contact`). Only the files the gateway needs (the import closure of `mesh`, `admin`, `telemetry`,
`portnums`) are copied to `Code/Libraries/Meshtrail.Mesh/Protos/`, plus `nanopb.proto` because some files import it.
Messages only (`GrpcServices="None"`). Protobuf is backward compatible, so firmware ≥ 2.5 keeps working; unknown
newer fields are ignored.

**Licence note:** `meshtastic/protobufs` is **GPL-3.0** (so is the NuGet package, so no option avoids it). The
generated C# is a derivative work. For an on-prem server that is not distributed this is normally fine; if Meshtrail
is ever distributed to third parties, get a licence review first. The upstream licence is kept next to the files
(`Protos/LICENSE-meshtastic-protobufs`).

Our own SOS payload (`MeshtrailBeacon`, portnum `PRIVATE_APP` 256) is a separate `.proto` in `Protos/meshtrail/`.

## 3. Hosting: inside the WebApi process

The Mediator source generator may only run in `Meshtrail.WebApi`, and the gateway must turn radio packets into
Mediator commands. Options were a separate worker service (needs its own mediator, its own deployment, and a way to
reach SignalR) or a hosted service inside the WebApi.

**Decision:** the gateway runs as a `BackgroundService` hosted in the WebApi process.

- `Meshtrail.Mesh` (protocol, framing, `IMeshRadio`, simulator, beacon detection) references **nothing** from
  `Meshtrail.Core`, so tools and tests can use it alone.
- `Meshtrail.Core.Infrastructure/Mesh` holds the adapters (worker, outbound dispatcher, event → command mapping).
- A radio failure must never crash the API: the worker catches everything, and the gateway health check reports
  **Degraded** (not Unhealthy), so `/health/ready` stays 200 while the node is unreachable.

## Consequences

- One gateway at a time; the phone app cannot be connected to the gateway node over TCP at the same time.
- ESP32 nodes switch Bluetooth off when WiFi is on, so the gateway node is configured once (BT or USB) and then
  only reached over WiFi.
- `Meshtastic:Gateway:Mode = Simulated` gives a fake mesh, so development and tests need no hardware.
- Upgrading the protocol = copy the newer tag's files, rebuild, update this tag in the living doc.
