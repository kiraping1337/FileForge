# Sandbox image for FFmpeg. Same idea as the ImageMagick image: one short-lived container per
# conversion, started by the worker with no network, a read-only file system and resource limits.
# FFmpeg has no policy file; its restrictions come from the arguments the worker passes
# (forced input format, file-only protocol, thread limit).
FROM alpine:3.22

RUN apk add --no-cache ffmpeg \
    && adduser -D -H -u 10001 converter

USER 10001

ENTRYPOINT ["ffmpeg"]
