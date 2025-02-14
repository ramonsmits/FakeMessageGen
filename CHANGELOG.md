# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [?] - ?

- ✨ Migrated from NServiceBus.Raw to NServiceBus 9.x transport seam
- 🐛 List started at 11, accidentally added 11 instead of 1

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
