var builder = DistributedApplication.CreateBuilder(args);

var ownerEmail = builder.AddParameter("OwnerEmail");
var ownerPassword = builder.AddParameter("OwnerPassword", secret: true);
var dataProtectionCertB64 = builder.AddParameter("DataProtectionCertBase64", secret: true);
var dataProtectionCertPass = builder.AddParameter("DataProtectionCertPassword", secret: true);

var pgPassword = builder.AddParameter("PgPassword", secret: true);

// Persist Postgres across dev runs via a named volume (PgDataVolume, default app-pg-data). Postgres only
// applies POSTGRES_PASSWORD when it FIRST initializes an empty data dir, so a persistent volume created
// with one password rejects a later run that uses a different password ("password authentication failed
// for user postgres"). The Aspire smoke test runs with PgDataVolume="" (an ephemeral, per-run store) so it
// never shares — and poisons — the developer's volume with its own password. If you deliberately change
// PgPassword, delete the volume once so it re-initializes: docker volume rm app-pg-data.
var pgDataVolume = builder.Configuration["PgDataVolume"];
var postgres = builder.AddPostgres("postgres", password: pgPassword).WithPgAdmin();
if (!string.IsNullOrWhiteSpace(pgDataVolume))
    postgres = postgres.WithDataVolume(pgDataVolume);

var appDb = postgres.AddDatabase("appdb");

var copyEnabled = builder.Configuration["CopyEnabled"] ?? "false";

var web = builder.AddProject<Projects.Web>("web")
    .WithReference(appDb)
    .WaitFor(appDb)
    .WithEnvironment("App__OwnerEmail", ownerEmail)
    .WithEnvironment("App__OwnerPassword", ownerPassword)
    .WithEnvironment("App__DataProtectionCertBase64", dataProtectionCertB64)
    .WithEnvironment("App__DataProtectionCertPassword", dataProtectionCertPass)
    .WithEnvironment("App__Copy__Enabled", copyEnabled)
    .WithExternalHttpEndpoints();

var mcp = builder.AddProject<Projects.Mcp>("mcp")
    .WithReference(appDb)
    .WaitFor(appDb)
    .WithEnvironment("App__DataProtectionCertBase64", dataProtectionCertB64)
    .WithEnvironment("App__DataProtectionCertPassword", dataProtectionCertPass)
    .WithExternalHttpEndpoints();

builder.Build().Run();
