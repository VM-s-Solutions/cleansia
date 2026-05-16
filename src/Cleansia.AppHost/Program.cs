var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithEndpoint("tcp", e => { e.Port = 5432; e.TargetPort = 5432; e.IsProxied = false; });

var cleansiaDb = postgres.AddDatabase("ConnectionString", databaseName: "Cleansia");

var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var queues = storage.AddQueues("QueueStorageConnectionString");

var partnerApi = builder.AddProject<Projects.Cleansia_Web_Partner>("partner-api")
    .WithEndpoint("http", e => { e.Port = 5000; e.IsProxied = false; })
    .WithEndpoint("https", e => { e.Port = 8000; e.IsProxied = false; })
    .WithReference(cleansiaDb)
    .WithReference(queues)
    .WaitFor(cleansiaDb);

var adminApi = builder.AddProject<Projects.Cleansia_Web_Admin>("admin-api")
    .WithEndpoint("http", e => { e.Port = 5001; e.IsProxied = false; })
    .WithReference(cleansiaDb)
    .WithReference(queues)
    .WaitFor(cleansiaDb);

var partnerMobileApi = builder.AddProject<Projects.Cleansia_Web_Mobile_Partner>("partner-mobile-api")
    .WithEndpoint("http", e => { e.Port = 5002; e.IsProxied = false; })
    .WithReference(cleansiaDb)
    .WithReference(queues)
    .WaitFor(cleansiaDb);

var customerApi = builder.AddProject<Projects.Cleansia_Web_Customer>("customer-api")
    .WithEndpoint("http", e => { e.Port = 5003; e.IsProxied = false; })
    .WithReference(cleansiaDb)
    .WithReference(queues)
    .WaitFor(cleansiaDb);

// Dedicated host for the customer mobile app (Android, future iOS). Mirrors
// the partner-Mobile host shape: body-token JWT, no cookies, no CSRF — native
// clients can't read HttpOnly cookies that the Customer Web host (5003) uses
// after the booking-extras/HttpOnly migration. Issues tokens with
// JwtAudiences.Customer so the same user pool/audience as the web side.
var customerMobileApi = builder.AddProject<Projects.Cleansia_Web_Mobile_Customer>("customer-mobile-api")
    .WithEndpoint("http", e => { e.Port = 5004; e.IsProxied = false; })
    .WithReference(cleansiaDb)
    .WithReference(queues)
    .WaitFor(cleansiaDb);

var functions = builder.AddProject<Projects.Cleansia_Functions>("functions")
    .WithReference(cleansiaDb)
    .WithReference(queues)
    .WaitFor(cleansiaDb);

builder.Build().Run();
