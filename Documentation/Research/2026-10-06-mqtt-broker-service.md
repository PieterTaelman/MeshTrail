# MQTT broker as its own service (2026-10-06)

## Context

The [multi-gateway decision](2026-10-06-multi-gateway-mqtt.md) chose an MQTT broker embedded in the WebApi. The product
owner asked to see the broker as its own resource in the AppHost, and plans **regional brokers** (West-Europe,
East-Europe, …) for production later.

## Options

| Option | Pros | Cons |
|---|---|---|
| Broker inside the WebApi | One process | Cannot run one per region without running the whole API per region |
| **Separate broker service (`Meshtrail.MqttBroker`)** | One per region later; restarts and load independent of the API; visible in the Aspire dashboard | The API needs an MQTT connection to it |

## Decision

A separate service, `Code/Server/Meshtrail.MqttBroker` (MQTTnet), started by the AppHost on port 1883.
The Meshtrail API (and the probe) log in to it with a **service account** and receive everything; gateways log in
with their own credentials (in Development: any login).

The broker is a **strict router**, not a general MQTT broker:

- gateway uplinks go only to the service account — a gateway never receives another gateway's traffic (it would
  re-transmit it over the air, flooding meshes everywhere);
- only the service account (or the broker itself) can send to gateways;
- gateways may only publish Meshtastic topics and are rate-limited (default 20 messages/s).

## Consequences

- Production: one broker service per region, each with a public address + TLS; the API connects to every broker.
- The service password is a secret (`MqttBroker:ServicePassword`); Development uses a fixed dev-only value.
- Gateway credentials (per gateway) come with the gateway registry in the next phase.
