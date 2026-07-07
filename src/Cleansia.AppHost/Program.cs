var builder = DistributedApplication.CreateBuilder(args);

// Stable password (from user-secrets / env: Parameters:postgres-password), NOT generated per run.
// The container is Persistent, so its password is baked in at first creation and never updated on
// later starts; a per-run generated password would drift from the existing container and fail auth
// (28P01). Pinning keeps the handed-out password identical to the baked-in one across restarts.
var postgresPassword = builder.AddParameter("postgres-password", secret: true);

var postgres = builder.AddPostgres("postgres", password: postgresPassword)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithEndpoint("tcp", e => { e.Port = 5432; e.TargetPort = 5432; e.IsProxied = false; });

var cleansiaDb = postgres.AddDatabase("ConnectionString", databaseName: "Cleansia");

// Azurite emulator with PINNED standard ports (10000 blob, 10001 queue,
// 10002 table). Pinning is load-bearing: appsettings.json /
// local.settings.json fall back to `UseDevelopmentStorage=true` which is
// hard-coded to those ports inside the Azure SDK. Without the pin, Aspire
// picks random ports and the producer (web hosts) + consumer (Functions
// queue triggers) end up on different Azurite instances — message goes in,
// nothing comes out, queue function never fires. This was the recurring
// "queue function not triggered" bug.
//
// Persistent + data volume so queue state survives Aspire restarts; the
// same container is reused across runs.
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(emulator => emulator
        .WithLifetime(ContainerLifetime.Persistent)
        .WithDataVolume("cleansia-azurite-data")
        .WithBlobPort(10000)
        .WithQueuePort(10001)
        .WithTablePort(10002));
var queues = storage.AddQueues("QueueStorageConnectionString");

// One-shot migrator: the ONLY startup actor allowed to touch the schema. Every app project
// below waits for its COMPLETION (exit 0), not merely for the Postgres container being
// healthy — WaitFor(cleansiaDb) alone let the hosts' background jobs (outbox drainer,
// fiscal sweep, Hangfire) race the in-process migration and crash on missing tables.
// A non-zero exit (failed migration) keeps every dependent stopped instead of letting it
// run against a half-migrated schema.
var migrations = builder.AddProject<Projects.Cleansia_MigrationService>("migrations")
    .WithReference(cleansiaDb)
    .WaitFor(cleansiaDb);

var partnerApi = builder.AddProject<Projects.Cleansia_Web_Partner>("partner-api")
    .WithEndpoint("http", e => { e.Port = 5000; e.IsProxied = false; })
    .WithEndpoint("https", e => { e.Port = 8000; e.IsProxied = false; })
    .WithReference(cleansiaDb)
    .WithReference(queues)
    .WaitForCompletion(migrations);

var adminApi = builder.AddProject<Projects.Cleansia_Web_Admin>("admin-api")
    .WithEndpoint("http", e => { e.Port = 5001; e.IsProxied = false; })
    .WithReference(cleansiaDb)
    .WithReference(queues)
    .WaitForCompletion(migrations);

var partnerMobileApi = builder.AddProject<Projects.Cleansia_Web_Mobile_Partner>("partner-mobile-api")
    .WithEndpoint("http", e => { e.Port = 5002; e.IsProxied = false; })
    .WithReference(cleansiaDb)
    .WithReference(queues)
    .WaitForCompletion(migrations);

var customerApi = builder.AddProject<Projects.Cleansia_Web_Customer>("customer-api")
    .WithEndpoint("http", e => { e.Port = 5003; e.IsProxied = false; })
    .WithReference(cleansiaDb)
    .WithReference(queues)
    .WaitForCompletion(migrations);

// Dedicated host for the customer mobile app (Android, future iOS). Mirrors
// the partner-Mobile host shape: body-token JWT, no cookies, no CSRF — native
// clients can't read HttpOnly cookies that the Customer Web host (5003) uses
// after the booking-extras/HttpOnly migration. Issues tokens with
// JwtAudiences.Customer so the same user pool/audience as the web side.
var customerMobileApi = builder.AddProject<Projects.Cleansia_Web_Mobile_Customer>("customer-mobile-api")
    .WithEndpoint("http", e => { e.Port = 5004; e.IsProxied = false; })
    .WithReference(cleansiaDb)
    .WithReference(queues)
    .WaitForCompletion(migrations);

var functions = builder.AddProject<Projects.Cleansia_Functions>("functions")
    .WithReference(cleansiaDb)
    .WithReference(queues)
    .WaitForCompletion(migrations);

builder.Build().Run();
