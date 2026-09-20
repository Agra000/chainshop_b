using chainshop_b.Model;
using Microsoft.EntityFrameworkCore;

namespace chainshop_b.Data
{
    public class ApplicationDBContext : DbContext
    {
        public ApplicationDBContext(DbContextOptions<ApplicationDBContext> options) : base(options)
        {
        }

        public DbSet<MsUsers> MsUsers { get; set; }
        public DbSet<MsAddresses> MsAddresses { get; set; }
        public DbSet<MsSellers> MsSellers { get; set; }
        public DbSet<MsCategories> MsCategories { get; set; }
        public DbSet<TrProducts> TrProducts { get; set; }
        public DbSet<LtProductImages> LtProductImages { get; set; }
        public DbSet<LtProductSpecifications> LtProductSpecifications { get; set; }
        public DbSet<TrCartItems> TrCartItems { get; set; }
        public DbSet<MsCourierCompanies> MsCourierCompanies { get; set; }
        public DbSet<TrDrivers> TrDrivers { get; set; }
    }
}
