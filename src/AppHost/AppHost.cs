var builder = DistributedApplication.CreateBuilder(args);

var ownerEmail = builder.AddParameter("OwnerEmail");
var ownerPassword = builder.AddParameter("OwnerPassword", secret: true);
var dataProtectionCertB64 = builder.AddParameter("DataProtectionCertBase64", secret: true);
var dataProtectionCertPass = builder.AddParameter("DataProtectionCertPassword", secret: true);

var pgPassword = builder.AddParameter("PgPassword", secret: true);
var postgres = builder.AddPostgres("postgres", password: pgPassword)
    .WithDataVolume("ctw-pg-data")
    .WithPgAdmin();

var ctwDb = postgres.AddDatabase("ctwdb");

var web = builder.AddProject<Projects.Web>("web")
    .WithReference(ctwDb)
    .WaitFor(ctwDb)
    .WithEnvironment("Ctw__OwnerEmail", ownerEmail)
    .WithEnvironment("Ctw__OwnerPassword", ownerPassword)
    .WithEnvironment("Ctw__DataProtectionCertBase64", dataProtectionCertB64)
    .WithEnvironment("Ctw__DataProtectionCertPassword", dataProtectionCertPass)
    .WithExternalHttpEndpoints();

var mcp = builder.AddProject<Projects.Mcp>("mcp")
    .WithReference(ctwDb)
    .WaitFor(ctwDb)
    .WithEnvironment("Ctw__DataProtectionCertBase64", dataProtectionCertB64)
    .WithEnvironment("Ctw__DataProtectionCertPassword", dataProtectionCertPass)
    .WithExternalHttpEndpoints();

builder.Build().Run();
