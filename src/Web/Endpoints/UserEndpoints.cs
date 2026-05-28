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
            await db.Users
                .Select(u => new { u.Id, u.Email, Role = u.RoleName, u.IsLockedOut, u.CreatedAt })
                .ToListAsync());

        g.MapPost("/", async (CreateUserRequest req, CtwDbContext db, IPasswordHasher hasher, ICurrentUser current) =>
        {
            // Rank: 0=Owner,1=Admin,2=User,3=Viewer
            if (req.Role == 0) return Results.Forbid();
            if (req.Role == 1 && !current.IsInRole("Owner")) return Results.Forbid();
            var normalized = req.Email.ToUpperInvariant();
            if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized))
                return Results.Conflict("email exists");

            AppUser u = req.Role switch
            {
                1 => new AdminUser(),
                2 => new RegularUser(),
                3 => new ViewerUser { SeeAllInstances = req.ViewerSeeAllInstances },
                _ => throw new InvalidOperationException("invalid role")
            };
            u.Email = req.Email;
            u.NormalizedEmail = normalized;
            u.PasswordHash = hasher.Hash(req.Password);
            u.MustChangePassword = true;
            u.CreatedByUserId = current.UserId;
            u.SecurityStamp = RandomNumberGenerator.GetBytes(32);

            db.Users.Add(u);
            await db.SaveChangesAsync();
            return Results.Ok(new { u.Id });
        });

        g.MapPost("/{id:guid}/reset-password", async (Guid id, ResetPasswordRequest req,
            CtwDbContext db, IPasswordHasher hasher, ICurrentUser current) =>
        {
            var uid = UserId.From(id);
            var target = await db.Users.FirstOrDefaultAsync(u => u.Id == uid);
            if (target is null) return Results.NotFound();
            if (target is OwnerUser && !current.IsInRole("Owner")) return Results.Forbid();
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
            var uid = UserId.From(id);
            var target = await db.Users.FirstOrDefaultAsync(u => u.Id == uid);
            if (target is null) return Results.NotFound();
            if (target is OwnerUser) return Results.Forbid();
            if (target is AdminUser && !current.IsInRole("Owner")) return Results.Forbid();
            db.Users.Remove(target);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}
