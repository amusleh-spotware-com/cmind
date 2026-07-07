var builder = DistributedApplication.CreateBuilder(args);

var ownerEmail = builder.AddParameter("OwnerEmail");
var ownerPassword = builder.AddParameter("OwnerPassword", secret: true);
var dataProtectionCertB64 = builder.AddParameter("DataProtectionCertBase64", secret: true);
var dataProtectionCertPass = builder.AddParameter("DataProtectionCertPassword", secret: true);

var pgPassword = builder.AddParameter("PgPassword", secret: true);
var postgres = builder.AddPostgres("postgres", password: pgPassword)
    .WithDataVolume("app-pg-data")
    .WithPgAdmin();

var appDb = postgres.AddDatabase("appdb");

var web = builder.AddProject<Projects.Web>("web")
    .WithReference(appDb)
    .WaitFor(appDb)
    .WithEnvironment("App__OwnerEmail", ownerEmail)
    .WithEnvironment("App__OwnerPassword", ownerPassword)
    .WithEnvironment("App__DataProtectionCertBase64", dataProtectionCertB64)
    .WithEnvironment("App__DataProtectionCertPassword", dataProtectionCertPass)
    .WithExternalHttpEndpoints();

var mcp = builder.AddProject<Projects.Mcp>("mcp")
    .WithReference(appDb)
    .WaitFor(appDb)
    .WithEnvironment("App__DataProtectionCertBase64", dataProtectionCertB64)
    .WithEnvironment("App__DataProtectionCertPassword", dataProtectionCertPass)
    .WithExternalHttpEndpoints();

builder.Build().Run();
