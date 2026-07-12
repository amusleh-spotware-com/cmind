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

        g.MapGet("/", async (DataContext db) =>
            await db.Users
                .Select(u => new { u.Id, u.Email, Role = u.RoleName, u.IsLockedOut, u.CreatedAt })
                .ToListAsync());

        g.MapGet("/pending", async (DataContext db) =>
            await db.Users
                .Where(u => u.ActivationState == UserActivationState.PendingApproval)
                .Select(u => new { u.Id, u.Email, Role = u.RoleName, u.CreatedAt })
                .ToListAsync());

        g.MapPost("/{id:guid}/approve", async (Guid id, DataContext db) =>
        {
            var uid = UserId.From(id);
            var target = await db.Users.FirstOrDefaultAsync(u => u.Id == uid);
            if (target is null) return Results.NotFound();
            try
            {
                target.Approve();
            }
            catch (Core.Domain.DomainException ex)
            {
                return Results.BadRequest(new { error = ex.Code });
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { target.Id });
        });

        g.MapPost("/", async (CreateUserRequest req, DataContext db, IPasswordHasher hasher, ICurrentUser current) =>
        {
            // Rank: 0=Owner,1=Admin,2=User,3=Viewer
            if (req.Role == 0) return Results.Forbid();
            if (req.Role == 1 && !current.IsInRole("Owner")) return Results.Forbid();
            var normalized = req.Email.ToUpperInvariant();
            if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized))
                return Results.Conflict("email exists");

            var email = new Email(req.Email);
            var stamp = RandomNumberGenerator.GetBytes(32);
            var passwordHash = hasher.Hash(req.Password);
            AppUser u = req.Role switch
            {
                1 => AdminUser.Create(email, passwordHash, stamp, createdBy: current.UserId),
                2 => RegularUser.Create(email, passwordHash, stamp, createdBy: current.UserId),
                3 => ViewerUser.Create(email, passwordHash, stamp, req.ViewerSeeAllInstances,
                    createdBy: current.UserId),
                _ => throw new InvalidOperationException("invalid role")
            };

            db.Users.Add(u);
            await db.SaveChangesAsync();
            return Results.Ok(new { u.Id });
        });

        g.MapPost("/{id:guid}/reset-password", async (Guid id, ResetPasswordRequest req,
            DataContext db, IPasswordHasher hasher, ICurrentUser current) =>
        {
            var uid = UserId.From(id);
            var target = await db.Users.FirstOrDefaultAsync(u => u.Id == uid);
            if (target is null) return Results.NotFound();
            if (target is OwnerUser && !current.IsInRole("Owner")) return Results.Forbid();
            target.ResetPassword(hasher.Hash(req.NewPassword), RandomNumberGenerator.GetBytes(32));
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        g.MapDelete("/{id:guid}", async (Guid id, DataContext db, ICurrentUser current) =>
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
