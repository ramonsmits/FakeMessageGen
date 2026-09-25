# FakeMessageGen

[![CI](https://github.com/ramonsmits/FakeMessageGen/actions/workflows/ci.yml/badge.svg)](https://github.com/ramonsmits/FakeMessageGen/actions/workflows/ci.yml) [![NuGet](https://img.shields.io/nuget/v/NBraceIT.FakeMessageGen)](https://www.nuget.org/packages/NBraceIT.FakeMessageGen)

## What

This is a tool to generates fake NServiceBus messages without requiring processing first. Its purpose is to generate a large set of message for ingestion by Particular Software its ServiceControl audit and error ingestion software.

## Install

This tool requires the [.net sdk](https://dotnet.microsoft.com/en-us/download/dotnet).

Installation:
```con
dotnet tool install -g NBraceIT.FakeMessageGen
```

Update:
```
dotnet tool update -g NBraceIT.FakeMessageGen
```

## Versioning

Versions follow `YYYY.RELEASECOUNT.PATCH`:

- `YYYY` — the year of the release
- `RELEASECOUNT` — the release number within that year, starting at 1
- `PATCH` — hotfix number for that release, starting at 0

For example, `2026.1.0` is the first release of 2026 and `2026.1.1` a hotfix for it. Versions are derived from git tags using [MinVer](https://github.com/adamralph/minver). Releases before 2026.1.0 used semantic versioning (`1.0.0` to `2.0.0`).

## Release

Releases are made by pushing a version tag. The [release workflow](.github/workflows/release.yml) packs the tool with that version, pushes it to nuget.org and creates a GitHub release using the matching `CHANGELOG.md` section, so add that section first.

Publishing uses [nuget.org Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) (OpenID Connect, no stored API key). One-time setup:

1. On nuget.org open your profile menu, **Trusted Publishing**, and add a policy with repository owner `ramonsmits`, repository `FakeMessageGen`, workflow file `release.yml` (no environment).
2. Add the repository secret `NUGET_USER` containing the nuget.org profile name that owns the package (not the e-mail address).

```
git tag 2026.1.0
git push origin 2026.1.0
```

Every push and pull request is built on Linux and Windows by the [CI workflow](.github/workflows/ci.yml).

## Help

The command line help output:

```
FakeMessagGen.exe destination isError [maxQueueLength] [rateLimit] [maxConcurrency] [batchSize] [connectionString]

    destination:
        The queue name to send messages to. It must already exist;
        this tool never creates queues. Start the consuming endpoint
        (for example ServiceControl) first.

    isError:
        true    — generate fake error messages.
        false   — generate fake audit messages.

    maxQueueLength (default 10000):
        Pauses sending when the destination reaches this depth.

    rateLimit (default 5000):
        Max messages per second (taking batchSize into account).

    maxConcurrency (default 100):
        Number of concurrent batches in flight.

    batchSize (default 16):
        Number of messages per batch send.

    connectionString:
        Connection to use. If not specified, the tool probes environment and config.
        Recognized formats:
            Azure Service Bus — starts with "Endpoint="
            RabbitMQ          — starts with "host="
            Learning          — path-like (/foo or C:\foo)
            MSMQ              — "msmq" (Windows only, destination can be queue@machine)

Tip: If you omit connectionString, it will try to resolve it from env or config.

Output is a full-screen UI when attached to a terminal that supports ANSI escape sequences,
and plain lines (one queue length and rate line every 2s)
when stdout is redirected or the console has no ANSI support (legacy Windows console).
Set FAKEMESSAGEGEN_PLAIN=1 to force plain lines.
```

> The destination queue is never created by this tool: the transport is initialised with no receivers
> and no sending addresses, and MSMQ additionally runs with `CreateQueues = false`. Start the endpoint
> that owns the queue first — with ServiceControl that means running its setup so the `audit` (or
> `error`) queue exists.

## Output

The tool has two output modes and picks one at startup:

| Mode | When | What you get |
|------|------|--------------|
| Interactive | stdout is a terminal that renders ANSI escape sequences | Full-screen UI with a main, queue length and log frame, updated live |
| Plain | stdout is redirected (file, pipe, scheduled task, service, CI, `Start-Process -RedirectStandardOutput`), the console has no ANSI support, or `FAKEMESSAGEGEN_PLAIN=1` is set | Startup settings, then one line every 2 seconds with the queue length and send rates, plus NServiceBus log output at Info level and above. No escape sequences |

Plain output looks like this:

```
         Using: LearningTransport
     RateLimit: 200.00/s
     BatchSize: 16
   Destination: audit
       IsError: False
MaxQueueLength: 100,000
MaxConcurrency: 10
Press CTRL+C to exit...
2026-09-25 12:11:15 [queued:        400 Rates/sec [now:   176.0/s] [10s:    22.4/s] [1min:     3.7/s] [10min:     0.4/s] [1hr:     0.1/s] [lifetime:   190.3/s]
2026-09-25 12:11:17 [queued:        784 Rates/sec [now:   176.0/s] [10s:    60.8/s] [1min:    10.1/s] [10min:     1.0/s] [1hr:     0.2/s] [lifetime:   191.0/s]
Paused until under 100000
```

ANSI support on Windows is detected by asking the console to enable virtual terminal processing
(`SetConsoleMode` with `ENABLE_VIRTUAL_TERMINAL_PROCESSING`). Windows Terminal has it on already; the
classic console host used by `cmd.exe` on Windows Server supports it since Server 2016 but leaves it off
until the application turns it on, which the tool now does. If enabling fails (legacy console) the tool
falls back to plain output instead of printing escape sequences as garbage. On Linux and macOS a
terminal is assumed to support ANSI unless `TERM` is empty or `dumb`.

## Transports

| Transport | Connection string format | Environment variable |
|-----------|--------------------------|----------------------|
| Azure Service Bus | starts with `Endpoint=` | `CONNECTIONSTRING_AZURESERVICEBUS` |
| RabbitMQ | starts with `host=` | `CONNECTIONSTRING_RABBITMQ` |
| Learning | path-like (`/foo` or `C:\foo`) | `CONNECTIONSTRING_LEARNING` |
| MSMQ | `msmq` | `CONNECTIONSTRING_MSMQ` |

When no connection string is passed, all environment variables containing `CONNECTIONSTRING` are probed and a selection menu is shown.

### MSMQ

MSMQ is only available on Windows. The destination can be a local queue name (`audit`) or a remote queue (`audit@machine`). Messages are sent to transactional queues in one native MSMQ transaction per batch, and the queue length is read via the MSMQ management API.

The MSMQ transport is compiled in from the [NServiceBus.Transport.Msmq.Sources](https://docs.particular.net/nuget/NServiceBus.Transport.Msmq.Sources) source package (and its `Particular.Msmq` dependency). These packages only target `net10.0-windows`, and a dotnet tool cannot target a Windows specific framework, so the project downloads the packages and compiles their sources into the `net10.0` build. They are only published on the Particular Software feed, which is configured in `nuget.config`.
