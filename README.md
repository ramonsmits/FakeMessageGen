# FakeMessageGen

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

## Help

The command line help output:

```
FakeMessageGen.exe destination isError (maxQueueLength) (rateLimit) (maxConcurrency) (batchSize) (connectionstring)
  
    destination: 
    
        Queue to send messages to.
    
    isError:
    
        true will generate fake error.
        false will generate fake audit.
    
    maxQueueLength: default {MaxQueueLength}
    
        Will pause seeding message when the queue length exceeds this limit.
    
    rateLimit: default {RateLimit}
    
        Will not generate more messages per second than this limit taking
        batch size into account.
                    
    maxConcurrency: default {MaxConcurrency}
    
        How many concurrency (batch) sends to allow
    
    batchSize: default {BatchSize}
     
        The batch size to use for each batch send operation
    
    connectionstring:
    
        The connection string to use for the destination.
    
        Will probe the format to check if it can assume RabbitMQ, Azure Service Bus,
        Learning or MSMQ transport.
```

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
