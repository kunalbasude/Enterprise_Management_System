using EnterpriseManagement.Application.Common.Extensions;
using EnterpriseManagement.Application.Common.Interfaces;
using EnterpriseManagement.Application.Common.Models;
using EnterpriseManagement.Application.Features.AuditLogs.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseManagement.Application.Features.AuditLogs.Services;

public class AuditLogQueryService : IAuditLogQueryService
{
    private readonly IApplicationDbContext _context;

    public AuditLogQueryService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<AuditLogDto>> GetAsync(
        AuditLogQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs.AsNoTracking().AsQueryable();

        if (parameters.UserId.HasValue)
        {
            query = query.Where(a => a.UserId == parameters.UserId.Value);
        }

        if (parameters.Action.HasValue)
        {
            query = query.Where(a => a.Action == parameters.Action.Value);
        }

        if (!string.IsNullOrWhiteSpace(parameters.EntityType))
        {
            var entityType = parameters.EntityType.Trim();

            query = query.Where(a => a.EntityType == entityType);
        }

        if (parameters.EntityId.HasValue)
        {
            query = query.Where(a => a.EntityId == parameters.EntityId.Value);
        }

        if (parameters.From.HasValue)
        {
            // Normalised to UTC: Npgsql rejects a non-UTC DateTime for
            // timestamptz, so an unspecified-kind value from the query string
            // would throw at execution rather than filter.
            var from = DateTime.SpecifyKind(parameters.From.Value, DateTimeKind.Utc);

            query = query.Where(a => a.CreatedAt >= from);
        }

        if (parameters.To.HasValue)
        {
            var to = DateTime.SpecifyKind(parameters.To.Value, DateTimeKind.Utc);

            query = query.Where(a => a.CreatedAt <= to);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var term = parameters.Search.Trim().ToLowerInvariant();

            query = query.Where(a =>
                (a.UserEmail != null && a.UserEmail.ToLower().Contains(term)) ||
                a.EntityType.ToLower().Contains(term));
        }

        var projected = query.Select(a => new AuditLogDto
        {
            Id = a.Id,
            UserId = a.UserId,
            UserEmail = a.UserEmail,
            Action = a.Action,
            EntityType = a.EntityType,
            EntityId = a.EntityId,
            Metadata = a.Metadata,
            IpAddress = a.IpAddress,
            CorrelationId = a.CorrelationId,
            CreatedAt = a.CreatedAt
        });

        // Newest first by default, matching ix_audit_logs_created_at DESC so the
        // common case walks the index instead of sorting the whole table.
        var sorted = parameters.IsDescending || string.IsNullOrWhiteSpace(parameters.SortBy)
            ? projected.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            : projected.OrderBy(a => a.CreatedAt).ThenBy(a => a.Id);

        return await sorted.ToPagedResultAsync(parameters.Page, parameters.PageSize, cancellationToken);
    }
}
