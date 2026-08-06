FROM mcr.microsoft.com/devcontainers/base:trixie

# iptables/ipset/dig are needed by init-firewall.sh, not by the app itself
RUN apt-get update && apt-get install -y --no-install-recommends \
        iptables \
        ipset \
        dnsutils \
        jq \
        aggregate \
    && rm -rf /var/lib/apt/lists/*
