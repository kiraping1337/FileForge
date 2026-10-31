# Architecture decisions

Short log of decisions and trade-offs, grouped by stage. Each entry: what was chosen and why.
Known limitations are listed at the end.

## Stage 1 — Foundation

**Projects.** `Domain`, `Application`, `Infrastructure`, `Api`, `Worker` and a unit test project.
Dependencies point inwards: `Domain` knows nothing; `Application` knows `Domain`; `Infrastructure`
implements `Application` interfaces; `Api` and `Worker` wire everything together. Projects were added
only when they had real code.

**Rich domain entities.** Entity properties have private setters; status changes only through methods
(`StartProcessing`, `Complete`, `Fail`, `Cancel`, `Expire`, `ReturnToQueue`). Each method checks the
current status, so an invalid transition throws `InvalidJobStateException` instead of corrupting data.

**Ids generated in code.** `Guid.CreateVersion7()` (time-ordered, keeps the primary key index compact).
Columns use `ValueGeneratedNever()`, so EF treats a new object with an id as a new row.

**Enums stored as text** with a CHECK constraint built from the enum names. Readable in SQL and safe
against reordering enum members.

**Times are UTC `DateTime`** (`timestamp with time zone`). Entities reject non-UTC values. The current
time comes from an injected `TimeProvider`, so tests can control it.

**Delete behaviour.** `ConversionFiles` → `Restrict`: a file row must not disappear while the physical
file may still exist. `JobAccessTokens` → `Cascade`: a token has no physical resource behind it.

**Local secrets.** No connection string in `appsettings.json`: user-secrets locally, environment
variables in Docker. PostgreSQL listens on `127.0.0.1:5433` to avoid clashing with a local install.

## Stage 2 — First working flow

**Conversion registry.** Each converter declares its routes (`png -> jpg`, ...). `ConversionRegistry`
collects them at startup and is the single source of truth for `GET /formats`, upload validation and
picking the converter. Two converters claiming the same route fail the startup.

**Format detection by content.** The extension and MIME type are never trusted. A signature check
(first bytes) rejects obvious fakes early; the real validation is done by ImageMagick itself, with the
decoder forced by a `png:` prefix instead of letting it guess.

**Storage keys.** Files are stored under 32 random hex characters. The user's file name is display
data only and is sanitized (directory part, control characters, length). Any key that does not match
the format is rejected before it can become a path.

**Upload is streamed and counted.** The size limit is enforced while copying, not trusted from headers.
On any failure after the file is saved, the file is deleted, so no orphan is left.

**Access tokens: opaque, not JWT.** 32 random bytes; the client gets them once, the database stores
only the SHA-256 hash. No salt or slow hash needed: the token is not guessable like a password.
Not JWT because every request reads the job from the database anyway, and an opaque token can be
revoked by deleting one row. No refresh token: the token lives exactly as long as the job's files.

**Unknown job = 403, not 404.** A missing job and a wrong token give the same answer, so the API does
not reveal which job ids exist.

**202 Accepted** for job creation: the conversion happens later, in the worker. The response with the
token is sent with `Cache-Control: no-store`.

**One error format.** All errors are RFC 9457 `ProblemDetails` with an extra `code` field
(`FILE_TOO_LARGE`, `ACCESS_DENIED`, ...). Expected errors are `AppException`; anything else becomes
a generic 500 without details.

**`IAppDbContext` instead of repositories.** The Application layer uses EF Core `DbSet`s through an
interface. Trade-off: Application depends on EF Core, but avoids a repository layer that would only
forward calls in a project of this size.

**Queue in PostgreSQL, no broker.** The worker reserves the oldest `Pending` job with
`SELECT ... FOR UPDATE SKIP LOCKED` and moves it to `Processing` in the same transaction. Several
workers never take the same job. RabbitMQ/Redis would add a service without a real need here.
The worker polls every 2 seconds when the queue is empty.

**Scope per job.** The worker lives as long as the process, but each job gets a new DI scope and so
a new `DbContext`, like one HTTP request in the API.

## Stage 3 — Security and recovery

**Cleanup service** (in the worker, at startup and every 5 minutes): deletes files of expired, failed
and cancelled jobs, marks expired jobs `Expired` (rows are kept for statistics), deletes expired tokens
and orphan files. The physical file is deleted first, the row second; a file that is in use keeps its
row and is retried next run. `Processing` jobs are never touched.

**Orphan files** (on disk, no row) are deleted only if older than 1 hour, because an upload is written
to disk a moment before its row is saved. Files whose names are not storage keys are never touched.

**Stuck jobs.** A job in `Processing` for more than 10 minutes means the worker crashed. It goes back
to `Pending`, or to `Failed` after 3 attempts. Conversion errors are not retried: a broken file will
not convert on the second try. On a normal shutdown the worker returns its current job immediately.

**Limits.**
- Upload size is checked by a resource filter *before* ASP.NET Core reads the form (by
  `Content-Length`), and the per-request body limit cuts off clients that do not announce the size.
- Rate limit: 10 uploads per minute per client IP (fixed window), `429` with `Retry-After`.
- Storage quota: new uploads get `503` when stored files exceed 1 GB. A soft limit: parallel uploads
  may overshoot it slightly.

**`DELETE` while processing returns 409.** Claiming "deleted" while ImageMagick is still writing the
file would be false. Image conversion takes under a second, so the client simply retries. Real
cancellation of a running conversion is planned for long video jobs.

**Converter sandbox.** ImageMagick runs in a one-off container per conversion
(`converters/imagemagick`):
- image: Alpine + ImageMagick, non-root user, the only entry point is `magick`;
- `policy.xml`: only PNG/JPEG/WebP/BMP, no delegates (external programs), width/height/area/memory
  limits that reject decompression bombs from the header;
- `docker run`: `--network none`, `--read-only`, `/tmp` as a small tmpfs, memory/CPU/pids limits,
  `--user 10001`, `--cap-drop ALL`, `no-new-privileges`;
- the container sees only the input file (read-only) and an empty output folder;
- on timeout the container is killed by name (killing the docker CLI alone would not stop it).
All flags are built in one method covered by unit tests, so none can be dropped silently.

**Storage path.** `%LOCALAPPDATA%\FileForge` instead of the temp folder: on Windows the temp path may
contain 8.3 short names that Docker cannot mount.

## Known limitations

- **Docker access from the worker.** Whoever can start containers is effectively root on the host.
  Fine while the worker runs on the developer machine; when the worker itself runs in Docker Compose,
  the Docker socket must not simply be mounted into it. To be decided in stage 6 (e.g. a minimal
  internal runner service).
- **Containers share the host kernel.** A kernel exploit could escape the sandbox. Stronger isolation
  (gVisor, microVMs) is out of scope.
- **No optimistic concurrency.** `DELETE` of a `Pending` job can race with the worker reserving it;
  the stuck-job recovery assumes no conversion runs longer than 10 minutes. A row version (PostgreSQL
  `xmin`) would close both gaps.
- **Converter infrastructure failures** (Docker not running, image missing) mark the job `Failed`
  instead of retrying it.
- **Client IP behind a proxy.** The rate limiter uses the connection IP; forwarded headers must be
  configured when a reverse proxy is added.
