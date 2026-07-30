#!/bin/sh
set -e

apt-get update
update-ca-certificates

exec su app -s /bin/sh -c "dotnet GovUK.Dfe.FlexForms.Web.dll"