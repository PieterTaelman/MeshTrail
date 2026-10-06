# Multi-gateway platform over MQTT (2026-10-06)

## Context

Phases 0–2 were built for **one** gateway node reached by the server over TCP on the LAN. The goal changed: Meshtrail
becomes a **central platform** where people anywhere register their own nodes, and any registered node with WiFi can
act as a gateway. Three facts drive the design:

- A LoRa broadcast reaches only the nodes within a few hops (default 3, max 7) of the sending node. There is no
  "send to every node in the world"; you reach a node through a gateway that hears it.
- A server on the internet cannot open TCP connections to nodes behind home routers: gateways must connect **out**.
- With many gateways, the same packet arrives several times (every gateway that heard it).

Decisions taken with the product owner: one central server; gateways are ordinary registered nodes with their own
status; channel chat is per **team** (a private Meshtastic channel); node positions are public, SOS is visible to
everyone, direct messages and team chats are private.

## Options for connecting many gateways

| Option | Pros | Cons |
|---|---|---|
| **MQTT (firmware's MQTT module)** | In stock firmware, no extra software; node connects out (works behind NAT); TLS + login supported; the phone app can proxy MQTT for nodes without WiFi | Downlink details differ per firmware version; we must run a broker reachable from the internet |
| Own gateway agent (PC / Raspberry Pi / mobile app) | Full control over the protocol | Every gateway owner installs and runs extra software |
| TCP from the server | Already built | Impossible across the internet (NAT); one client per node |

**Decision:** MQTT. TCP stays for a local base station and development; a mobile-app gateway can be added later.

## Broker: embedded vs external

| Option | Pros | Cons |
|---|---|---|
| **Embedded MQTTnet (MIT) in the WebApi** | One process; per-gateway authentication in our own code (credentials table); we see every message without subscribing | Broker load lives in the API process |
| External (Mosquitto, EMQX) | Battle-tested at scale | Extra service to run on-prem; auth plugin or a sync job for credentials |

**Decision:** embedded MQTTnet 5.2 (`MQTTnet.Server`), wrapped in `MeshtasticMqttBroker` so it can be replaced.

## How the protocol is used

- Uplink topic `<root>/2/e/<ChannelName>/!<gatewayId>` with a protobuf `ServiceEnvelope {packet, channel_id,
  gateway_id}` (`meshtastic/mqtt.proto`, tag v2.8.1). The root is configurable per node (e.g. `msh/EU_868`), so the
  parser finds the `/2/<kind>/` part. Other kinds: `json`, `map` (MapReport), `stat` (online/offline).
- Gateways run with MQTT **encryption disabled** so packets arrive decoded and the server never needs channel keys
  (PSKs). Consequence: **TLS is mandatory** in production.
- Downlink: we publish a `ServiceEnvelope` to the same topic shape; a gateway with downlink enabled on that channel
  transmits it. Our packets use a **virtual Meshtrail node number** as `from` (default `!4d545231`), because firmware
  ignores packets that come back from MQTT with its own number. Replies and ACKs addressed to that number are uplinked.
- End-to-end (PKI) direct messages cannot be produced by a gateway for packets that come from MQTT; DMs are
  channel-encrypted on air until the server does the PKI crypto itself (later step).

## Status

Spike (phase A): broker, topic parser and `Meshtrail.MeshProbe --mqtt` built and unit/integration tested on loopback.
To confirm on hardware: decoded uplink, downlink text from the virtual node, ACK and reply, effect of `from` and hop
limit, behaviour with JSON downlink. Results go into the living doc once confirmed.

## Consequences

- Gateway owners configure their node once (server, port, TLS, username/password, root topic, encryption off,
  uplink/downlink on the channels they want to carry).
- The server needs a public endpoint with a TLS certificate for the broker port.
- Data model changes (next phase): gateways keyed by node number with owner and credentials, receptions per gateway,
  messages routed through the best gateway, server-side search per map area.
