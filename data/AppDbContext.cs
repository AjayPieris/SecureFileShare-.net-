using Microsoft.EntityFrameworkCore;
using SecureFileShare.Models;

namespace SecureFileShare.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<FileRecord> Files { get; set; }
    }
}