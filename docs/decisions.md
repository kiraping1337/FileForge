# Architecture decisions

The main design choices, the alternatives that were rejected, and the cost of each choice.
Known limitations are listed at the end.

## Structure

**Layers.** `Domain` → `Application` → `Infrastructure`; `Api`, `Worker` and `SandboxRunner` are entry
points that wire everything together. Dependencies point inwards, so business rules do not depend on
EF Core, Docker or HTTP.

**Rich domain entities.** Status changes only through methods (`StartProcessing`, `Complete`,
`Cancel`, ...), and each method checks the current status. An invalid transition throws instead of
silently corrupting data.

**`IAppDbContext` instead of repositories.** The Application layer uses EF Core `DbSet`s through an
interface. Cost: Application depends on EF Core. Gain: no repository layer that would only forward
calls in a project of this size.

## Jobs and queue

**Queue in PostgreSQL, no message broker.** The worker reserves the oldest `Pending` job with
`SELECT ... FOR UPDATE SKIP LOCKED` and marks it `Processing` in the same transaction, so several
workers never take the same job. RabbitMQ or Redis would add a service without a real need here.
Cost: the worker polls the table when the queue is empty.

**Crash recovery.** A job that stays in `Processing` longer than `StuckJobTimeout` was abandoned by a
crashed worker: it goes back to the queue, or fails after `MaxAttempts`. Conversion errors are not
retried, because a broken file will not convert on the second try. The worker refuses to start if
`StuckJobTimeout` is not well above the longest converter timeout; otherwise a slow conversion could
be given to a second worker.

**Real cancellation.** `DELETE` on a running job only records a cancellation request and answers
`202 Accepted`. The worker checks for it during the conversion, kills the container and deletes the
files. The API never claims a job is deleted while a converter is still writing its files.

**Cleanup.** Expired, failed and cancelled jobs lose their files; expired rows are kept for
statistics. The file is deleted first and the row second, so a failure never leaves a file without a
row. Files with no row are deleted only after an hour, because an upload is written to disk a moment
before its row is saved.

## Security

**Access tokens: opaque, not JWT.** 32 random bytes, sent to the client once; the database stores only
the SHA-256 hash. A JWT brings nothing here: every request reads the job from the database anyway,
and an opaque token is revoked by deleting one row. No accounts: the token lives as long as the job.

**Unknown job = 403, not 404.** A missing job and a wrong token get the same answer, so the API does
not reveal which job ids exist.

**Format by content, never by extension.** A signature check rejects fakes early; the converter is then
forced to use the detected decoder instead of guessing. The user's file name is display data only:
files are stored under random keys.

**Limits.** Upload size is checked before the form is read and again while streaming; uploads are
rate-limited per client IP; new uploads are refused when total storage exceeds a quota; FFmpeg output
is capped, and a result truncated by the cap fails the job.

**One sandbox for every conversion.** Each conversion runs in a new container: no network, read-only
file system, non-root user, no capabilities, memory/CPU/process/time limits. The container sees only
its input file and an empty output folder. All `docker run` flags are built in one method covered by
unit tests, so none can be dropped silently.

**Tool-specific hardening.** ImageMagick gets a `policy.xml` that allows only our four formats, no
external programs and limited image dimensions, which rejects decompression bombs. FFmpeg has no
policy file, so its restrictions are arguments: a forced input format and `-protocol_whitelist file`
block crafted files that make it read other files or URLs.

**The Docker socket lives in one small service.** Access to the socket is effectively root on the host.
The worker has a database connection and handles every job, so it does not get it. The sandbox runner
does: one endpoint, no database, no public port, reachable only from the worker. The worker sends only
the route and storage keys, never paths or tool arguments, so even a compromised worker cannot start
an arbitrary container.

## API and web interface

**One error format.** Errors are RFC 9457 `ProblemDetails` with a stable `code` field. The UI maps the
code to its own message and never shows server text, so wording can change on either side.

**Tokens stay in browser memory.** Never in the URL (history, logs) or `localStorage` (readable by any
script on the page). Cost: reloading the page loses the job list. Because the token goes in a header,
results are downloaded as a blob instead of a plain link.

**Same origin, no CORS.** nginx (and the Angular dev server in development) serves the app and proxies
`/api`, so the browser talks to one site and the API needs no CORS configuration.

## Tests and deployment

**Integration tests against a real PostgreSQL** (Testcontainers), because the queue relies on
`FOR UPDATE SKIP LOCKED`, which an in-memory provider cannot check. Converters and time are replaced
with test doubles, so the tests need no converter images and no waiting.

**Migrations run once, before everything else.** A one-shot `migrate` service applies them and exits;
the API and the worker start only after it succeeded. Two processes never migrate at the same time.

**Three networks.** `frontend` (nginx, API), `backend` (API, worker, PostgreSQL) and `sandbox` (worker,
runner). The last two have no route to the internet. Only nginx publishes a port, on `127.0.0.1` by
default.

## Known limitations

- **The sandbox runner holds the Docker socket.** Whoever takes it over controls the host. Rootless
  Docker or a socket proxy that allows only the needed API calls would reduce this.
- **Containers share the host kernel.** A kernel exploit could escape the sandbox. Stronger isolation
  (gVisor, microVMs) is out of scope.
- **No optimistic concurrency.** `DELETE` of a `Pending` job can race with the worker reserving it, and
  a cancellation can arrive just after the job completed. A row version (PostgreSQL `xmin`) would
  close these gaps.
- **Cancellation is polled**, so stopping takes a few seconds; LISTEN/NOTIFY could make it instant.
- **Infrastructure failures** (Docker down, image missing) fail the job instead of retrying it.
- **Forwarded headers are trusted from any proxy.** Safe here, where only nginx reaches the API. If the
  API were exposed directly, a client could fake its IP and bypass the rate limit.
- **No HTTPS and no Content Security Policy.** Meant for `localhost` or a local network; a public
  deployment needs TLS in front of nginx and a CSP header.
- **Fonts come from Google Fonts**, so the UI makes third-party requests. Self-hosting them would fix it.
