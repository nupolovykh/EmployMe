#!/bin/bash
set -euo pipefail  # Exit on error, undefined vars, and pipeline failures
IFS=$'\n\t'       # Stricter word splitting

# Based on the reference firewall from anthropics/claude-code's .devcontainer,
# extended with the domains this project's tooling needs (NuGet, the job
# sources listed in docs/SOURCES.md,
# and the remote MCP servers listed in PLAN.md's tooling map).

# 1. Extract Docker DNS info BEFORE any flushing
DOCKER_DNS_RULES=$(iptables-save -t nat | grep "127\.0\.0\.11" || true)

# Flush existing rules and delete existing ipsets
iptables -F
iptables -X
iptables -t nat -F
iptables -t nat -X
iptables -t mangle -F
iptables -t mangle -X
ipset destroy allowed-domains 2>/dev/null || true

# -F only clears rules, not chain policy. On first boot the kernel default
# policy is ACCEPT, so the rebuild steps below (GitHub API fetch, per-domain
# DNS resolution) run unrestricted as intended. But on a re-run against an
# already-locked-down container, policy is still DROP from the previous run
# -- and with no rules left after the flush, every new outbound connection,
# including the ones this script itself needs to make to rebuild the
# allowlist, gets dropped with no way to ever succeed. Reset to ACCEPT here
# so re-runs behave like first boot; re-tightened to DROP at the end once
# the allowlist is rebuilt.
iptables -P INPUT ACCEPT
iptables -P OUTPUT ACCEPT
iptables -P FORWARD ACCEPT

# 2. Selectively restore ONLY internal Docker DNS resolution
if [ -n "$DOCKER_DNS_RULES" ]; then
    echo "Restoring Docker DNS rules..."
    iptables -t nat -N DOCKER_OUTPUT 2>/dev/null || true
    iptables -t nat -N DOCKER_POSTROUTING 2>/dev/null || true
    echo "$DOCKER_DNS_RULES" | xargs -L 1 iptables -t nat
else
    echo "No Docker DNS rules to restore"
fi

# First allow DNS and localhost before any restrictions
# Allow outbound DNS
iptables -A OUTPUT -p udp --dport 53 -j ACCEPT
# Allow inbound DNS responses
iptables -A INPUT -p udp --sport 53 -j ACCEPT
# Allow outbound SSH
iptables -A OUTPUT -p tcp --dport 22 -j ACCEPT
# Allow inbound SSH responses
iptables -A INPUT -p tcp --sport 22 -m state --state ESTABLISHED -j ACCEPT
# Allow localhost
iptables -A INPUT -i lo -j ACCEPT
iptables -A OUTPUT -o lo -j ACCEPT

# Allow already-established connections immediately, before any of the
# network calls below (GitHub API fetch, per-domain DNS resolution). On a
# fresh container boot this is a no-op (chain policy defaults to ACCEPT
# until set below). But on a re-run against an already-locked-down
# container, the flush above removes this rule along with everything else
# while the DROP policy from the previous run stays in effect on the chain
# -- policies aren't touched by -F. Without this rule re-added immediately,
# every in-flight connection (including the one running this script) has
# its reply traffic silently dropped until the rule reappears ~90 lines and
# several round-trips later, which looks like the container losing network.
iptables -A INPUT -m state --state ESTABLISHED,RELATED -j ACCEPT
iptables -A OUTPUT -m state --state ESTABLISHED,RELATED -j ACCEPT

# Create ipset with CIDR support
ipset create allowed-domains hash:net

# Fetch GitHub meta information and aggregate + add their IP ranges
echo "Fetching GitHub IP ranges..."
gh_ranges=$(curl -s --retry 3 --retry-delay 2 --retry-connrefused --retry-all-errors https://api.github.com/meta)
if [ -z "$gh_ranges" ]; then
    echo "ERROR: Failed to fetch GitHub IP ranges"
    exit 1
fi

if ! echo "$gh_ranges" | jq -e '.web and .api and .git' >/dev/null; then
    echo "ERROR: GitHub API response missing required fields"
    exit 1
fi

echo "Processing GitHub IPs..."
while read -r cidr; do
    if [[ ! "$cidr" =~ ^[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}/[0-9]{1,2}$ ]]; then
        echo "ERROR: Invalid CIDR range from GitHub meta: $cidr"
        exit 1
    fi
    echo "Adding GitHub range $cidr"
    ipset add allowed-domains "$cidr" -exist
done < <(echo "$gh_ranges" | jq -r '(.web + .api + .git)[]' | aggregate -q)

# Resolve and add other allowed domains. A single domain failing to resolve
# (dead subdomain, transient DNS hiccup) must not abort the whole firewall —
# that would leave later steps (including the default-deny policy) never
# applied, i.e. fail OPEN instead of closed. Warn and skip instead.
for domain in \
    "registry.npmjs.org" \
    "api.nuget.org" \
    "www.nuget.org" \
    "builds.dotnet.microsoft.com" \
    "api.anthropic.com" \
    "sentry.io" \
    "statsig.com" \
    "marketplace.visualstudio.com" \
    "vscode.blob.core.windows.net" \
    "update.code.visualstudio.com" \
    "boards-api.greenhouse.io" \
    "www.greenhouse.io" \
    "www.greenhouse.com" \
    "developers.greenhouse.io" \
    "docs.greenhouse.io" \
    "api.lever.co" \
    "hire.lever.co" \
    "himalayas.app" \
    "jobicy.com" \
    "www.arbeitnow.com" \
    "railway.app" \
    "neon.com" \
    "neon.tech" \
    "ep-polished-poetry-b2qvi361-pooler.c-6.eu-central-1.aws.neon.tech" \
    "render.com" \
    "employme-4uql.onrender.com" \
    "employme-api.onrender.com" \
    "supabase.com" \
    "mcp.linear.app" \
    "mcp.slack.com" \
    "mcp.sentry.dev" \
    "mcp.railway.com"; do
    echo "Resolving $domain..."
    ips=$(dig +noall +answer A "$domain" | awk '$4 == "A" {print $5}')
    if [ -z "$ips" ]; then
        echo "WARNING: Failed to resolve $domain, skipping"
        continue
    fi

    while read -r ip; do
        if [[ ! "$ip" =~ ^[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}$ ]]; then
            echo "ERROR: Invalid IP from DNS for $domain: $ip"
            exit 1
        fi
        echo "Adding $ip for $domain"
        ipset add allowed-domains "$ip" -exist
    done < <(echo "$ips")
done

# Get host IP from default route
HOST_IP=$(ip route | grep default | cut -d" " -f3)
if [ -z "$HOST_IP" ]; then
    echo "ERROR: Failed to detect host IP"
    exit 1
fi

HOST_NETWORK=$(echo "$HOST_IP" | sed "s/\.[0-9]*$/.0\/24/")
echo "Host network detected as: $HOST_NETWORK"

# This also covers the sibling db/ollama containers on the same
# docker-compose bridge network, since they share this /24 with the gateway.
iptables -A INPUT -s "$HOST_NETWORK" -j ACCEPT
iptables -A OUTPUT -d "$HOST_NETWORK" -j ACCEPT

# Set default policies to DROP first
iptables -P INPUT DROP
iptables -P FORWARD DROP
iptables -P OUTPUT DROP

# (established-connection accept rules were added earlier, right after
# the flush, so in-flight connections survive a re-run -- see above)

# Then allow only specific outbound traffic to allowed domains
iptables -A OUTPUT -m set --match-set allowed-domains dst -j ACCEPT

# Explicitly REJECT all other outbound traffic for immediate feedback
iptables -A OUTPUT -j REJECT --reject-with icmp-admin-prohibited

echo "Firewall configuration complete"
echo "Verifying firewall rules..."
if curl --connect-timeout 5 https://example.com >/dev/null 2>&1; then
    echo "ERROR: Firewall verification failed - was able to reach https://example.com"
    exit 1
else
    echo "Firewall verification passed - unable to reach https://example.com as expected"
fi

# Verify GitHub API access
if ! curl --connect-timeout 5 --retry 3 --retry-delay 2 --retry-connrefused --retry-all-errors https://api.github.com/zen >/dev/null 2>&1; then
    echo "ERROR: Firewall verification failed - unable to reach https://api.github.com"
    exit 1
else
    echo "Firewall verification passed - able to reach https://api.github.com as expected"
fi
