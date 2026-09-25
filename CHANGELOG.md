# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Legend:

- 🚨 = Breaking change
- ✨ = Feature/Improvement
- 💥 = Critical bug
- 🐛 = Bug
- 📦 = Dependency update


## [Unreleased]

- 💥 Fixed the crash `IOException: The handle is invalid` on Windows when stdout is redirected (scheduled task, service, CI, `Start-Process -RedirectStandardOutput`). The tool now prints plain lines instead of the full-screen UI: one queue length and rate line every 2 seconds, without escape sequences
- 🐛 Fixed garbled output in `cmd.exe` on Windows Server: the tool now enables ANSI escape sequence processing on the Windows console at startup, and falls back to plain output when the console does not support it (legacy console)
- ✨ Plain output can be forced with `FAKEMESSAGEGEN_PLAIN=1`, and is used automatically when `TERM` is `dumb`
- ✨ The transport selection menu accepts piped input (reads a line when stdin is redirected instead of a key press)

## [2026.1.1] - 2026-09-25

- 💥 Fixed the MSMQ queue length check throwing `InvalidCastException` on every call. The backpressure check never completed, so no messages were sent at all and MSMQ was unusable in 2026.1.0
- ✨ The command line help and README now state that the destination queue must already exist, since the tool never creates queues
- ✨ The NuGet package now embeds an SPDX Software Bill of Materials (`_manifest/spdx_2.2/manifest.spdx.json`) listing all bundled dependencies, for supply-chain auditing
- 📦 Upgraded MinVer from 7.0.0 to 8.0.0
- 📦 Upgraded NuGet.Protocol from 7.3.1 to 7.9.0
- 📦 Upgraded NuGet.Versioning from 7.3.1 to 7.9.0

## [2026.1.0] - 2026-09-25

- ✨ Added GitHub Actions: CI build on every push and pull request (Linux and Windows), release workflow that publishes to nuget.org via Trusted Publishing (no stored API key) and creates the GitHub release when a version tag is pushed
- ✨ Version numbers now follow `YYYY.RELEASECOUNT.PATCH` (year, release number within the year, hotfix) instead of semantic versioning
- ✨ Added MSMQ transport support (Windows only) by compiling in the `NServiceBus.Transport.Msmq.Sources` source package. Select it with connection string `msmq` or the `CONNECTIONSTRING_MSMQ` environment variable
- ✨ Added `nuget.config` with the Particular Software feed as the MSMQ source packages are not published on nuget.org
- 📦 Upgraded NServiceBus from 9.2.8 to 10.2.9 (required by the MSMQ source package)
- 📦 Upgraded NServiceBus.RabbitMQ from 10.1.7 to 11.2.1
- 📦 Upgraded NServiceBus.Transport.AzureServiceBus from 5.1.2 to 6.5.0
- 📦 Upgraded Azure.Messaging.ServiceBus from 7.20.1 to 7.21.0
- 📦 Upgraded NuGet.Protocol from 7.3.0 to 7.3.1 (resolves low severity advisory GHSA-g4vj-cjjj-v7hg)

## [2.0.0] - 2026-01-22

- 🚨 Targeting .NET 10
- ✨ Added launch profiles for error and audit scenarios
- ✨ Environment variables are now ordered by key
- ✨ Enabled dependabot for automated dependency updates
- 🐛 Fix rate limit issue (using float for math and correct semaphore initialization)
- 🐛 Removed newline from version text when a new version is detected

## [1.3.1] - 2025-09-04

- 🐛 Endpoint name does not except empty string for RabbitMQ transport

## [1.3.0] - 2025-06-16

- 🐛 List started at 11, accidentally added 11 instead of 1
- ✨ Migrated from NServiceBus.Raw to NServiceBus 9.x transport seam
- ✨ Reporting version information at exit

## [1.2.0] - 2025-02-14

- ✨ Connection string options start at 1 instead of 0 making it more natural for keyboard layout
- ✨ Connection string selection only needs a key press when there are less than 10 items

## [1.1.0] - 2025-02-11

- ✨ Added ability to select auto-detected envvar connection string

## [1.0.1] - 2025-02-11

- 🐛 Fixed shutdown cancellation issue
- 🐛 Fixed terminal corruption at shutdown

## [1.0.0] - 2025-02-11

- 🥳 First sort of release roughly 4 years after the [first commit](https://github.com/ramonsmits/FakeMessageGen/commit/8c1bd0d689106962ebaefcb77b6ebbde7fea9eb5)
