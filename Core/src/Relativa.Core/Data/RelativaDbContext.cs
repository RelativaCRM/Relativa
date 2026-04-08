using Microsoft.EntityFrameworkCore;

namespace Relativa.Core.Data;

public sealed class RelativaDbContext(DbContextOptions<RelativaDbContext> options) : DbContext(options)
{
}
