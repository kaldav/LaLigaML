using Microsoft.EntityFrameworkCore;

namespace Database
{
    public class LaLigaContext : DbContext
    {
        public DbSet<Match> Matches { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseSqlServer("Data Source=.;Initial Catalog=LaLiga;Integrated Security=SSPI;");
        }
    }
}
