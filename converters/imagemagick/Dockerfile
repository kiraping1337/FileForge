# Sandbox image for ImageMagick. One short-lived container per conversion:
# the worker starts it with no network, a read-only file system and resource limits.
FROM alpine:3.22

# imagemagick itself handles PNG and BMP; JPEG and WebP are separate packages on Alpine.
RUN apk add --no-cache imagemagick imagemagick-jpeg imagemagick-webp \
    && adduser -D -H -u 10001 converter

# Replaces the default policy: only our formats, no external programs, hard resource limits.
COPY policy.xml /etc/ImageMagick-7/policy.xml

ENV MAGICK_TEMPORARY_PATH=/tmp

# Never root. The worker passes --user too, so this holds even if the image changes.
USER 10001

# The container can only run magick: the worker passes arguments, not a command.
ENTRYPOINT ["magick"]
