using Microsoft.EntityFrameworkCore;

namespace Coordinator.Models.Contexts
{
    public class TwoPhaseCommitContext : DbContext
    {
        public TwoPhaseCommitContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<Node> Nodes { get; set; }
        public DbSet<NodeState> NodeStates { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Node>().HasData(
                    new Node("Order.API") { Id = Guid.NewGuid() },
                    new Node("Stock.API") { Id = Guid.NewGuid() },
                    new Node("Payment.API") { Id = Guid.NewGuid() }
                );
        }
    }
}
