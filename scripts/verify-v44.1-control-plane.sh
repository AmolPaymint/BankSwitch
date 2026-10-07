#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
required=(
  "src/BankSwitch.Application/EnterpriseConfigurationControlPlane.cs"
  "src/BankSwitch.Infrastructure/SqlEnterpriseConfigurationRepository.cs"
  "src/BankSwitch.Infrastructure/InMemoryEnterpriseConfigurationRepository.cs"
  "src/BankSwitch.Infrastructure/ConfigurationRuntimeProbe.cs"
  "src/BankSwitch.Admin/Endpoints/EnterpriseConfigurationEndpoints.cs"
  "src/BankSwitch.Admin/Pages/Configuration/ControlPlane.cshtml"
  "db/038_enterprise_configuration_control_plane.sql"
  "V44_1_ENTERPRISE_CONFIGURATION_CONTROL_PLANE.md"
)
for f in "${required[@]}"; do test -f "$root/$f" || { echo "MISSING: $f"; exit 1; }; echo "PASS: $f"; done
grep -q "MapEnterpriseConfigurationEndpoints" "$root/src/BankSwitch.Admin/Program.cs"
grep -q "SqlEnterpriseConfigurationRepository" "$root/src/BankSwitch.Admin/Program.cs"
grep -q "Production requires Repository:Provider=SqlServer" "$root/src/BankSwitch.Admin/Program.cs"
grep -q "ConfigurationChangeRequests" "$root/db/038_enterprise_configuration_control_plane.sql"
grep -q "SecretReferences" "$root/db/038_enterprise_configuration_control_plane.sql"
echo "PASS: v44.1 static control-plane verification"
