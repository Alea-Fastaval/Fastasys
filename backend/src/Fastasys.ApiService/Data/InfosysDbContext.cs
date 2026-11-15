using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Data.Seed;
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
    public DbSet<ParticipantCategory> ParticipantCategories => Set<ParticipantCategory>();
    public DbSet<OrganizerArea> OrganizerAreas => Set<OrganizerArea>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<ScheduleSignup> ScheduleSignups => Set<ScheduleSignup>();
    public DbSet<Run> Runs => Set<Run>();
    public DbSet<RunSeat> RunSeats => Set<RunSeat>();

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

        // Infosys deltagere: brugerkategori_id, country and game_id have foreign keys without ON DELETE CASCADE;
        // work_area has ON UPDATE CASCADE only
        modelBuilder.Entity<Participant>(participant =>
        {
            participant.Property(p => p.PreferredLanguage).HasConversion<string>();
            participant.PrimitiveCollection(p => p.ActivityLanguages).ElementType().HasConversion<string>();
            participant.PrimitiveCollection(p => p.SleepingAreas).ElementType().HasConversion<string>();
            participant.HasOne(p => p.Category)
                .WithMany()
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            participant.HasOne(p => p.Country)
                .WithMany()
                .HasForeignKey(p => p.CountryCode)
                .OnDelete(DeleteBehavior.Restrict);
            participant.HasOne(p => p.OrganizerArea)
                .WithMany()
                .HasForeignKey(p => p.OrganizerAreaId)
                .OnDelete(DeleteBehavior.Restrict);
            participant.HasOne(p => p.GameActivity)
                .WithMany()
                .HasForeignKey(p => p.GameActivityId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ParticipantCategory>().HasData(ParticipantLookupSeed.Categories);
        modelBuilder.Entity<OrganizerArea>().HasData(ParticipantLookupSeed.OrganizerAreas);
        modelBuilder.Entity<Country>().HasData(CountrySeed.All);

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

        // Infosys hold: unique (afvikling_id, holdnummer); its foreign keys have no ON DELETE CASCADE
        modelBuilder.Entity<Run>(run =>
        {
            run.HasOne(r => r.Schedule)
                .WithMany(s => s.Runs)
                .HasForeignKey(r => r.ScheduleId)
                .OnDelete(DeleteBehavior.Restrict);
            run.HasOne(r => r.Room)
                .WithMany()
                .HasForeignKey(r => r.RoomId)
                .OnDelete(DeleteBehavior.Restrict);
            run.HasIndex(r => new { r.ScheduleId, r.Number }).IsUnique();
        });

        // Infosys pladser: primary key (hold_id, pladsnummer), unique (hold_id, deltager_id), no ON DELETE CASCADE
        modelBuilder.Entity<RunSeat>(seat =>
        {
            seat.HasKey(s => new { s.RunId, s.SeatNumber });
            seat.HasOne(s => s.Run)
                .WithMany(r => r.Seats)
                .HasForeignKey(s => s.RunId)
                .OnDelete(DeleteBehavior.Restrict);
            seat.HasOne(s => s.Participant)
                .WithMany()
                .HasForeignKey(s => s.ParticipantId)
                .OnDelete(DeleteBehavior.Restrict);
            seat.HasIndex(s => new { s.RunId, s.ParticipantId }).IsUnique();
            seat.Property(s => s.Role).HasConversion<string>();
        });

        // Infosys deltagere_tilmeldinger has the primary key (deltager_id, prioritet, afvikling_id) and foreign keys
        // without ON DELETE CASCADE. Agreed on 2026-11-13: the key is participant + schedule + role instead; deleting a
        // participant removes their wishes (Infosys's code does that), and a schedule with wishes can't be deleted.
        modelBuilder.Entity<ScheduleSignup>(signup =>
        {
            signup.HasKey(s => new { s.ParticipantId, s.ScheduleId, s.Role });
            signup.HasOne(s => s.Participant)
                .WithMany(p => p.Signups)
                .HasForeignKey(s => s.ParticipantId)
                .OnDelete(DeleteBehavior.Cascade);
            signup.HasOne(s => s.Schedule)
                .WithMany(s => s.Signups)
                .HasForeignKey(s => s.ScheduleId)
                .OnDelete(DeleteBehavior.Restrict);
            signup.Property(s => s.Role).HasConversion<string>();
        });

        modelBuilder.Entity<HeroForceShiftParticipant>()
            .HasKey(gsp => new { gsp.ShiftId, gsp.ParticipantId });

        modelBuilder.Entity<ParticipantFood>()
            .HasKey(pf => new { pf.ParticipantId, pf.FoodTypeId, pf.Date });

        modelBuilder.Entity<ParticipantWear>()
            .HasKey(pw => new { pw.ParticipantId, pw.WearItemId });
    }
}
