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
```

> The destination queue is never created by this tool: the transport is initialised with no receivers
> and no sending addresses, and MSMQ additionally runs with `CreateQueues = false`. Start the endpoint
> that owns the queue first — with ServiceControl that means running its setup so the `audit` (or
> `error`) queue exists.

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
