#!/usr/bin/env bash
set -euo pipefail
cmd="${1:?csr | install | check}"; shift
case "$cmd" in
  csr)
    host="${1:?host name, for example fpms.farm.local}"
    openssl req -new -newkey rsa:2048 -nodes -keyout "$host.key.pem" -out "$host.csr" -subj "/CN=$host" -addext "subjectAltName=DNS:$host"
    chmod 600 "$host.key.pem"
    echo "send $host.csr to the issuer; keep $host.key.pem private (never in the repository)" ;;
  install)
    cert="${1:?issued certificate (pem)}"; key="${2:?private key (pem)}"; out="${3:?output .pfx}"
    openssl pkcs12 -export -in "$cert" -inkey "$key" -out "$out" -passout env:FPMS_CERT_PASSWORD
    echo "wrote $out" ;;
  check)
    pfx="${1:?.pfx file}"
    openssl pkcs12 -in "$pfx" -passin env:FPMS_CERT_PASSWORD -nokeys 2>/dev/null | openssl x509 -noout -subject -enddate ;;
  *) echo "unknown command: $cmd" >&2; exit 2 ;;
esac
