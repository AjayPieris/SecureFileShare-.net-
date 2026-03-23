using Microsoft.EntityFrameworkCore;
using SecureFileShare.Models;

namespace SecureFileShare.Data
{
    public class AppDbContext : DbContext
    {
        // This constructor passes the database configuration to the base DbContext
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // This tells Entity Framework to create a table named 'Files' based on our FileRecord blueprint
        public DbSet<FileRecord> Files { get; set; }
    }
}