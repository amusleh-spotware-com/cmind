using System.Security.Cryptography;
using Core;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

public record CreateUserRequest(string Email, string Password, int Role, bool ViewerSeeAllInstances = false);
public record ResetPasswordRequest(string NewPassword);

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/users").RequireAuthorization("AdminOrAbove");

        g.MapGet("/", async (CtwDbContext db) =>
        {
            var rows = await db.Users
                .Select(u => new { u.Id, u.Email, u.Role, u.IsLockedOut, u.CreatedAt })
                .ToListAsync();
            return rows.Select(u => new { u.Id, u.Email, Role = u.Role.Name, u.IsLockedOut, u.CreatedAt }).ToList();
        });

        g.MapPost("/", async (CreateUserRequest req, CtwDbContext db, IPasswordHasher hasher, ICurrentUser current) =>
        {
            var role = UserRole.All.FirstOrDefault(r => r.Rank == req.Role);
            if (role is null) return Results.BadRequest("invalid role");
            if (role == UserRole.Owner) return Results.Forbid();
            if (role == UserRole.Admin && current.Role != UserRole.Owner) return Results.Forbid();
            var normalized = req.Email.ToUpperInvariant();
            if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized))
                return Results.Conflict("email exists");

            var u = new AppUser
            {
                Email = req.Email,
                NormalizedEmail = normalized,
                PasswordHash = hasher.Hash(req.Password),
                Role = role,
                ViewerSeeAllInstances = req.ViewerSeeAllInstances,
                MustChangePassword = true,
                CreatedByUserId = current.UserId,
                SecurityStamp = RandomNumberGenerator.GetBytes(32)
            };
            db.Users.Add(u);
            await db.SaveChangesAsync();
            return Results.Ok(new { u.Id });
        });

        g.MapPost("/{id:guid}/reset-password", async (Guid id, ResetPasswordRequest req,
            CtwDbContext db, IPasswordHasher hasher, ICurrentUser current) =>
        {
            var target = await db.Users.FindAsync(id);
            if (target is null) return Results.NotFound();
            if (target.Role == UserRole.Owner && current.Role != UserRole.Owner) return Results.Forbid();
            target.PasswordHash = hasher.Hash(req.NewPassword);
            target.MustChangePassword = true;
            target.IsLockedOut = false;
            target.AccessFailedCount = 0;
            target.SecurityStamp = RandomNumberGenerator.GetBytes(32);
            target.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        g.MapDelete("/{id:guid}", async (Guid id, CtwDbContext db, ICurrentUser current) =>
        {
            var target = await db.Users.FindAsync(id);
            if (target is null) return Results.NotFound();
            if (target.Role == UserRole.Owner) return Results.Forbid();
            if (target.Role == UserRole.Admin && current.Role != UserRole.Owner) return Results.Forbid();
            db.Users.Remove(target);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}
