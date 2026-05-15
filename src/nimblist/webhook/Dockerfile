# Build webhook from source so the binary is statically linked against musl,
# not glibc — the official release binaries are glibc-linked and won't run on Alpine.
FROM golang:alpine AS builder
RUN go install github.com/adnanh/webhook@latest

FROM alpine:3.21
# docker-cli gives us `docker` + the compose v2 plugin
RUN apk add --no-cache docker-cli docker-cli-compose
COPY --from=builder /go/bin/webhook /usr/local/bin/webhook

EXPOSE 9000
ENTRYPOINT ["webhook"]
