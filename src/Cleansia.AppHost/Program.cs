var builder = DistributedApplication.CreateBuilder(args);

// Stable, not generated per run: the container is persistent, so a fresh password drifts from the
// baked-in one and fails auth with 28P01. → /architecture/local-orchestration#postgres-password
var postgresPassword = builder.AddParameter("postgres-password", secret: true);

var postgres = builder.AddPostgres("postgres", password: postgresPassword)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithEndpoint("tcp", e => { e.Port = 5432; e.TargetPort = 5432; e.IsProxied = false; });

var cleansiaDb = postgres.AddDatabase("ConnectionString", databaseName: "Cleansia");

// Ports pinned — random ports put the producer and the consumer on different Azurite instances and
// the queue function silently never fires. → /architecture/local-orchestration#azurite-ports
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(emulator => emulator
        .WithLifetime(ContainerLifetime.Persistent)
        .WithDataVolume("cleansia-azurite-data")
        .WithBlobPort(10000)
        .WithQueuePort(10001)
        .WithTablePort(10002));
var queues = storage.AddQueues("QueueStorageConnectionString");

// Declared, not created on demand: the blob read/list paths never create a container, so a fresh
// volume breaks the retention sweep and the PDF jobs. → /architecture/local-orchestration#blob-containers
string[] blobContainers =
[
    "generated-receipts",
    "generated-invoices",
    "user-files",
    "employee-documents",
    "order-photos",
    "dispute-evidence",
];
foreach (var containerName in blobContainers)
{
    storage.AddBlobContainer(containerName);
}

// The only startup actor allowed to touch the schema. Every API below WaitForCompletion(migrations),
// not WaitFor — waiting on the database alone lets background jobs race the migration and crash on
// missing tables. Executable, not AddProject: VS refuses to launch this as a project resource.
// → /architecture/local-orchestration#migrator
#if DEBUG
const string migratorConfiguration = "Debug";
#else
const string migratorConfiguration = "Release";
#endif
var migratorDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "Cleansia.MigrationService"));
var migrations = builder.AddExecutable(
        "migrations",
        "dotnet",
        migratorDir,
        Path.Combine(migratorDir, "bin", migratorConfiguration, "net10.0", "Cleansia.MigrationService.dll"))
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

// Separate from the web customer host because native clients cannot read its HttpOnly cookies: body
// -token JWT, no cookies, no CSRF, same audience. → /architecture/local-orchestration#two-customer-hosts
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
