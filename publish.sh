#!/usr/bin/env bash

# Build
dotnet publish -c Release -r linux-x64 -o bin

# Deploy
scp bin/Argus.Collector.E621 jax@192.168.0.11:argus/collector
scp bin/Argus.Collector.FList jax@192.168.0.11:argus/collector
scp bin/Argus.Collector.Hypnohub jax@192.168.0.11:argus/collector
scp bin/Argus.Coordinator jax@192.168.0.11:argus/coordinator
scp bin/Argus.Worker jax@192.168.0.12:argus/worker
scp bin/Argus.Worker jax@192.168.0.13:argus/worker

