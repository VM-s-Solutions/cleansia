var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent);

var cleansiaDb = postgres.AddDatabase("ConnectionString", databaseName: "Cleansia");

var partnerApi = builder.AddProject<Projects.Cleansia_Web_Partner>("partner-api")
    .WithEndpoint("http", e => { e.Port = 5000; e.IsProxied = false; })
    .WithEndpoint("https", e => { e.Port = 8000; e.IsProxied = false; })
    .WithReference(cleansiaDb)
    .WaitFor(cleansiaDb);

var adminApi = builder.AddProject<Projects.Cleansia_Web_Admin>("admin-api")
    .WithEndpoint("http", e => { e.Port = 5001; e.IsProxied = false; })
    .WithReference(cleansiaDb)
    .WaitFor(cleansiaDb);

var mobileApi = builder.AddProject<Projects.Cleansia_Web_Mobile>("mobile-api")
    .WithEndpoint("http", e => { e.Port = 5002; e.IsProxied = false; })
    .WithReference(cleansiaDb)
    .WaitFor(cleansiaDb);

var customerApi = builder.AddProject<Projects.Cleansia_Web_Customer>("customer-api")
    .WithEndpoint("http", e => { e.Port = 5003; e.IsProxied = false; })
    .WithReference(cleansiaDb)
    .WaitFor(cleansiaDb);

builder.Build().Run();
