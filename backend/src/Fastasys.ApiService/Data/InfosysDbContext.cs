using Fastasys.ApiService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Data;

public class InfosysDbContext : DbContext
{
    public InfosysDbContext(DbContextOptions<InfosysDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Privilege> Privileges => Set<Privilege>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePrivilege> RolePrivileges => Set<RolePrivilege>();

    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<ParticipantSchedule> ParticipantSchedules => Set<ParticipantSchedule>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<HeroForceCategory> HeroForceCategories => Set<HeroForceCategory>();
    public DbSet<HeroForceShift> HeroForceShifts => Set<HeroForceShift>();
    public DbSet<HeroForceShiftParticipant> HeroForceShiftParticipants => Set<HeroForceShiftParticipant>();

    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<FoodType> FoodTypes => Set<FoodType>();
    public DbSet<ParticipantFood> ParticipantFoods => Set<ParticipantFood>();
    public DbSet<WearItem> WearItems => Set<WearItem>();
    public DbSet<ParticipantWear> ParticipantWears => Set<ParticipantWear>();
    public DbSet<EntranceType> EntranceTypes => Set<EntranceType>();

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Sale> Sales => Set<Sale>();

    public DbSet<Boardgame> Boardgames => Set<Boardgame>();
    public DbSet<BoardgameLoan> BoardgameLoans => Set<BoardgameLoan>();
    public DbSet<LoanItem> LoanItems => Set<LoanItem>();

    public DbSet<Newsletter> Newsletters => Set<Newsletter>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<SmsLog> SmsLogs => Set<SmsLog>();

    public DbSet<SignupPage> SignupPages => Set<SignupPage>();
    public DbSet<SignupPageElement> SignupPageElements => Set<SignupPageElement>();
    public DbSet<SignupConfig> SignupConfigs => Set<SignupConfig>();
    public DbSet<SignupSubmission> SignupSubmissions => Set<SignupSubmission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Composite Keys & Junction Tables (Provider agnostic)
        modelBuilder.Entity<UserRole>()
            .HasKey(ur => new { ur.UserId, ur.RoleId });

        modelBuilder.Entity<RolePrivilege>()
            .HasKey(rp => new { rp.RoleId, rp.PrivilegeId });

        modelBuilder.Entity<ParticipantSchedule>()
            .HasKey(ps => new { ps.ParticipantId, ps.ScheduleId });

        modelBuilder.Entity<Activity>(activity =>
        {
            activity.Property(a => a.Type).HasConversion<string>();
            activity.Property(a => a.PlayerLanguage).HasConversion<string>();
            activity.Property(a => a.GameMasterLanguage).HasConversion<string>();
            activity.Property(a => a.AutoSignupCategory).HasConversion<string>();
        });

        // Infosys refuses to delete an activity that still has runs (afviklinger FK without cascade)
        modelBuilder.Entity<Schedule>()
            .HasOne(s => s.Activity)
            .WithMany(a => a.Schedules)
            .HasForeignKey(s => s.ActivityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<HeroForceShiftParticipant>()
            .HasKey(gsp => new { gsp.ShiftId, gsp.ParticipantId });

        modelBuilder.Entity<ParticipantFood>()
            .HasKey(pf => new { pf.ParticipantId, pf.FoodTypeId, pf.Date });

        modelBuilder.Entity<ParticipantWear>()
            .HasKey(pw => new { pw.ParticipantId, pw.WearItemId });
    }
}
