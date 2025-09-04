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
        or Learning transport.
```
