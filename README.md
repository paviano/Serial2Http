# Serial2Http

HTTP wrapper for serial ports. Receives a POST, writes to the serial port, returns the `\r\n`-terminated response as JSON.

## Run

```bash
dotnet run --project Serial2Http
```

Default: `http://0.0.0.0:1234`

## POST /send

```json
{ "port": "COM3", "baudRate": 9600, "data": "PING\r\n", "timeoutMs": 2000 }
```

```json
{ "success": true, "data": "PONG", "error": null }
{ "success": false, "data": null, "error": "Port 'COM9' not found. Available ports: COM1, COM3" }
```

## Local testing

Install **com0com** to create a virtual port pair (e.g. `COM7` ↔ `COM8`), then run the fake device:

```bash
dotnet run --project SerialFakeDevice -- COM8 9600
```

Supported commands: `STATUS`, `VERSION`, `PING`, `RESET`.
