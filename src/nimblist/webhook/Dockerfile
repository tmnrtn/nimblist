FROM alpine:3.21

# docker-cli gives us `docker` + the compose v2 plugin
RUN apk add --no-cache docker-cli docker-cli-compose

# Download the webhook binary for the target architecture.
# TARGETARCH is set automatically by `docker build` / `docker buildx build`.
ARG TARGETARCH=amd64
ARG WEBHOOK_VERSION=2.8.1
RUN wget -qO /tmp/webhook.tar.gz \
      "https://github.com/adnanh/webhook/releases/download/${WEBHOOK_VERSION}/webhook-linux-${TARGETARCH}.tar.gz" && \
    tar -xzf /tmp/webhook.tar.gz -C /usr/local/bin && \
    mv /usr/local/bin/webhook-linux-${TARGETARCH} /usr/local/bin/webhook && \
    rm /tmp/webhook.tar.gz

EXPOSE 9000
ENTRYPOINT ["webhook"]
