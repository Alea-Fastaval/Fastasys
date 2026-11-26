using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Data.Seed;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Data;

public class InfosysDbContext(DbContextOptions<InfosysDbContext> options) : DbContext(options)
{
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
    public DbSet<ActivitySettings> ActivitySettings => Set<ActivitySettings>();
    public DbSet<SignupSettings> SignupSettings => Set<SignupSettings>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<ScheduleSignup> ScheduleSignups => Set<ScheduleSignup>();
    public DbSet<Run> Runs => Set<Run>();
    public DbSet<RunSeat> RunSeats => Set<RunSeat>();

    public DbSet<IdCardTemplate> IdCardTemplates => Set<IdCardTemplate>();
    public DbSet<IdCardTemplateItem> IdCardTemplateItems => Set<IdCardTemplateItem>();
    public DbSet<ParticipantPhoto> ParticipantPhotos => Set<ParticipantPhoto>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<HeroForceCategory> HeroForceCategories => Set<HeroForceCategory>();
    public DbSet<HeroForceShift> HeroForceShifts => Set<HeroForceShift>();
    public DbSet<HeroForceShiftParticipant> HeroForceShiftParticipants => Set<HeroForceShiftParticipant>();

    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<FoodType> FoodTypes => Set<FoodType>();
    public DbSet<MealTime> MealTimes => Set<MealTime>();
    public DbSet<ParticipantMeal> ParticipantMeals => Set<ParticipantMeal>();
    public DbSet<WearType> WearTypes => Set<WearType>();
    public DbSet<WearPrice> WearPrices => Set<WearPrice>();
    public DbSet<WearAttribute> WearAttributes => Set<WearAttribute>();
    public DbSet<WearVariantAttribute> WearVariantAttributes => Set<WearVariantAttribute>();
    public DbSet<ParticipantWearOrder> ParticipantWearOrders => Set<ParticipantWearOrder>();
    public DbSet<ParticipantWearOrderAttribute> ParticipantWearOrderAttributes => Set<ParticipantWearOrderAttribute>();
    public DbSet<WearImage> WearImages => Set<WearImage>();
    public DbSet<WearImageLink> WearImageLinks => Set<WearImageLink>();
    public DbSet<TicketType> TicketTypes => Set<TicketType>();
    public DbSet<ParticipantTicket> ParticipantTickets => Set<ParticipantTicket>();

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

        // A single row (ActivitySettings.SingletonId), seeded with the default karma points
        modelBuilder.Entity<ActivitySettings>(settings =>
        {
            settings.Property(s => s.Id).ValueGeneratedNever();
            settings.HasData(new ActivitySettings());
        });

        // A single row (SignupSettings.SingletonId), seeded closed
        modelBuilder.Entity<SignupSettings>(settings =>
        {
            settings.Property(s => s.Id).ValueGeneratedNever();
            settings.HasData(new SignupSettings());
        });

        // Composite Keys & Junction Tables (Provider agnostic)
        modelBuilder.Entity<UserRole>().HasKey(ur => new { ur.UserId, ur.RoleId });

        modelBuilder.Entity<RolePrivilege>().HasKey(rp => new { rp.RoleId, rp.PrivilegeId });

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
            // Infosys participantidtemplates cascades on template delete: the participant gets the category's template
            participant.HasOne(p => p.IdCardTemplate)
                .WithMany()
                .HasForeignKey(p => p.IdCardTemplateId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Infosys brugerkategorier_idtemplates cascades on template delete: the category has no template then
        modelBuilder.Entity<ParticipantCategory>()
            .HasOne(c => c.IdCardTemplate)
            .WithMany()
            .HasForeignKey(c => c.IdCardTemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        // Infosys idtemplates_items cascades with its template
        modelBuilder.Entity<IdCardTemplateItem>(item =>
        {
            item.HasOne(i => i.Template)
                .WithMany(t => t.Items)
                .HasForeignKey(i => i.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);
            item.Property(i => i.Type).HasConversion<string>();
            item.Property(i => i.DataSource).HasConversion<string>();
        });

        // Infosys participantphotoidentifiers cascades with the participant
        modelBuilder.Entity<ParticipantPhoto>()
            .HasOne(p => p.Participant)
            .WithOne()
            .HasForeignKey<ParticipantPhoto>(p => p.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);

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
        modelBuilder
            .Entity<Schedule>()
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
            run.HasOne(r => r.Room).WithMany().HasForeignKey(r => r.RoomId).OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<TicketType>().Property(t => t.Kind).HasConversion<string>();

        // Infosys deltagere_indgang has the primary key (deltager_id, indgang_id) and foreign keys without ON DELETE
        // CASCADE. Deleting a participant removes their tickets (Infosys's code does that); a ticket type that
        // participants have can't be deleted.
        modelBuilder.Entity<ParticipantTicket>(ticket =>
        {
            ticket.HasKey(t => new { t.ParticipantId, t.TicketTypeId });
            ticket.HasOne(t => t.Participant)
                .WithMany(p => p.Tickets)
                .HasForeignKey(t => t.ParticipantId)
                .OnDelete(DeleteBehavior.Cascade);
            ticket.HasOne(t => t.TicketType)
                .WithMany()
                .HasForeignKey(t => t.TicketTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HeroForceShiftParticipant>().HasKey(gsp => new { gsp.ShiftId, gsp.ParticipantId });

        // Infosys mad: kategori is unique
        modelBuilder.Entity<FoodType>().HasIndex(f => f.NameDanish).IsUnique();

        // Infosys madtider: unique (mad_id, dato). Its foreign key has no ON DELETE CASCADE, so Infosys couldn't delete a
        // food type with meal times; agreed 2026-11-25: deleting a food type deletes its meal times, which is refused while
        // participants have ordered one of them (participant_meals is RESTRICT).
        modelBuilder.Entity<MealTime>(mealTime =>
        {
            mealTime.HasOne(m => m.FoodType)
                .WithMany(f => f.MealTimes)
                .HasForeignKey(m => m.FoodTypeId)
                .OnDelete(DeleteBehavior.Cascade);
            mealTime.HasIndex(m => new { m.FoodTypeId, m.Time }).IsUnique();
        });

        // Infosys deltagere_madtider has the primary key (deltager_id, madtid_id) and foreign keys without ON DELETE
        // CASCADE. Deleting a participant removes their meals (Infosys's code does that); an ordered meal time can't be
        // deleted.
        modelBuilder.Entity<ParticipantMeal>(meal =>
        {
            meal.HasKey(m => new { m.ParticipantId, m.MealTimeId });
            meal.HasOne(m => m.Participant)
                .WithMany(p => p.Meals)
                .HasForeignKey(m => m.ParticipantId)
                .OnDelete(DeleteBehavior.Cascade);
            meal.HasOne(m => m.MealTime)
                .WithMany()
                .HasForeignKey(m => m.MealTimeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Infosys wearpriser: unique (wear_id, brugerkategori_id), foreign keys without ON DELETE CASCADE; Infosys deleted
        // a wear type's prices before the type. A price that participants have ordered can't be deleted.
        modelBuilder.Entity<WearPrice>(price =>
        {
            price.HasOne(p => p.WearType)
                .WithMany(w => w.Prices)
                .HasForeignKey(p => p.WearTypeId)
                .OnDelete(DeleteBehavior.Cascade);
            price.HasOne(p => p.ParticipantCategory)
                .WithMany()
                .HasForeignKey(p => p.ParticipantCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            price.HasIndex(p => new { p.WearTypeId, p.ParticipantCategoryId }).IsUnique();
        });

        // The 19 Infosys sizes are seeded with ids 1-19, so new attributes start after them
        modelBuilder.Entity<WearAttribute>(attribute =>
        {
            attribute.Property(a => a.Kind).HasConversion<string>();
            attribute.Property(a => a.Id).HasIdentityOptions(startValue: WearAttribute.FirstNewId);
            attribute.HasData(WearSeed.Sizes);
        });

        // Infosys wear_attribute_available: primary key (wear_id, attribute_id, variant), both foreign keys ON DELETE
        // CASCADE. Infosys has no way to delete an attribute, so the attribute side is RESTRICT here.
        modelBuilder.Entity<WearVariantAttribute>(variant =>
        {
            variant.HasKey(v => new { v.WearTypeId, v.Variant, v.AttributeId });
            variant.HasOne(v => v.WearType)
                .WithMany(w => w.VariantAttributes)
                .HasForeignKey(v => v.WearTypeId)
                .OnDelete(DeleteBehavior.Cascade);
            variant.HasOne(v => v.Attribute)
                .WithMany()
                .HasForeignKey(v => v.AttributeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Infosys deltagere_wear_order: foreign keys without ON DELETE CASCADE. Deleting a participant removes their
        // orders (Infosys's code does that, Deltagere::removeAllWear); an ordered price can't be deleted.
        modelBuilder.Entity<ParticipantWearOrder>(order =>
        {
            order.HasOne(o => o.Participant)
                .WithMany(p => p.WearOrders)
                .HasForeignKey(o => o.ParticipantId)
                .OnDelete(DeleteBehavior.Cascade);
            order.HasOne(o => o.WearPrice)
                .WithMany()
                .HasForeignKey(o => o.WearPriceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Infosys deltagere_wear_order_attributes: primary key (order_id, attribute_id), cascades with the order
        modelBuilder.Entity<ParticipantWearOrderAttribute>(attribute =>
        {
            attribute.HasKey(a => new { a.OrderId, a.AttributeId });
            attribute.HasOne(a => a.Order)
                .WithMany(o => o.Attributes)
                .HasForeignKey(a => a.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            attribute.HasOne(a => a.Attribute)
                .WithMany()
                .HasForeignKey(a => a.AttributeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Infosys refused to upload a second file with the same name
        modelBuilder.Entity<WearImage>().HasIndex(i => i.FileName).IsUnique();

        // Infosys wear_image_connection: primary key (image_id, wear_id, attribute_id), cascades with the image and the
        // wear type. "Alle" was a special attribute row; here it is a missing attribute, unique like any other.
        modelBuilder.Entity<WearImageLink>(link =>
        {
            link.HasOne(l => l.WearImage)
                .WithMany()
                .HasForeignKey(l => l.WearImageId)
                .OnDelete(DeleteBehavior.Cascade);
            link.HasOne(l => l.WearType)
                .WithMany(w => w.ImageLinks)
                .HasForeignKey(l => l.WearTypeId)
                .OnDelete(DeleteBehavior.Cascade);
            link.HasOne(l => l.Attribute)
                .WithMany()
                .HasForeignKey(l => l.AttributeId)
                .OnDelete(DeleteBehavior.Restrict);
            link.HasIndex(l => new { l.WearImageId, l.WearTypeId, l.AttributeId }).IsUnique().AreNullsDistinct(false);
        });
    }
}
