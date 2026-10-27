# Architecture decisions

Short log of decisions and trade-offs. Newest at the bottom.

## Stage 1 — Foundation

**Projects.** `Domain`, `Infrastructure`, `Api` and a unit test project. The `Application` and `Worker`
projects from the spec are added in stage 2, when they have real code. Empty projects add references
and noise without any benefit.

**Rich domain entities.** Entity properties have private setters; status changes only through methods
(`StartProcessing`, `Complete`, `Fail`, ...). Each method checks the current status, so invalid
transitions throw `InvalidJobStateException` instead of silently corrupting data.

**Ids generated in code.** `Guid.CreateVersion7()` (time-ordered) in the entity factory methods.
Columns are configured with `ValueGeneratedNever()`, so EF treats a new object with an id as a new row.

**Enums stored as text.** `Status` and `Role` are stored as strings with a CHECK constraint built from
the enum names. Slightly larger than integers, but readable in raw SQL and pgAdmin, and safe against
enum reordering.

**Times are UTC `DateTime`.** Npgsql maps them to `timestamp with time zone`. Entities reject
non-UTC values early with a clear error.

**Delete behaviour.**
- `ConversionFiles` → `Restrict`. A file row must not disappear while the physical file may still exist.
  Cleanup deletes the physical file first, then the row.
- `JobAccessTokens` → `Cascade`. A token has no physical resource behind it.
- Expired jobs are kept as rows with status `Expired` (useful for statistics); their files and tokens are removed.

**Access tokens.** Only a SHA-256 hash is stored. A salt or a slow hash (bcrypt) is not needed: the
token is 32 random bytes, so it cannot be guessed or brute-forced like a human password.

**Retry tracking.** `AttemptCount` is added to `ConversionJobs` (not in the original spec) so the queue
can stop retrying a job that keeps crashing workers.

**Local secrets.** No connection string in `appsettings.json`. Locally it is stored with
`dotnet user-secrets`, in Docker it is passed as an environment variable. PostgreSQL listens on
`127.0.0.1:5433` to avoid clashing with a locally installed PostgreSQL.

### Open questions
- Cancelling a job that is already `Processing` must stop the converter before reporting success.
  The domain currently allows `Processing -> Cancelled`; stage 3 decides how the worker learns about it.
