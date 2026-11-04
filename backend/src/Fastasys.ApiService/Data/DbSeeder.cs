using Fastasys.ApiService.Data.Entities;

namespace Fastasys.ApiService.Data;

public static class DbSeeder
{
    public static void Seed(InfosysDbContext db)
    {
        // Roles & Privileges
        var adminRole = db.Roles.FirstOrDefault(r => r.Name == "Admin");
        if (adminRole == null)
        {
            adminRole = new Role
            {
                Name = "Admin",
                Description = "Administrator with full system access"
            };
            db.Roles.Add(adminRole);
            db.SaveChanges();
        }

        var requiredPrivileges = new[]
        { // for x_edit, its both adding more x's and updating existing x's
            ("Assign Entrance to Participant", "entrance_participant_edit"),
            ("Assign Food to Participant", "food_participant_edit"),
            ("Assign Hero Force to Participant", "hero_force_participant_edit"),
            ("Assign Run to Participant", "run_participant_edit"),
            ("Assign Payment to Participant","payment_participant_edit"),
            ("Assign Sleeping to participant","sleeping_participant_edit"),
            ("Assign Wear to Participant", "wear_participant_edit"),
            ("Distribute Boardgame", "boardgame_distribute_edit"),
            ("Distribute Food", "food_distribute_edit"),
            ("Distribute Wear", "wear_distribute_edit"),
            ("Edit Budget and accounting", "accounting_edit"),
            ("Edit Roles and privileges", "role_edit"),
            ("Assign Roles and privileges", "role_user_edit"),
            ("Edit Activity", "activity_edit"),
            ("Edit Boardgame", "boardgame_edit"),
            ("Edit Entrance", "entrance_edit"),
            ("Edit Food marketplace", "food_marketplace_edit"),
            ("Edit Food", "food_edit"),
            ("Edit Hero Force", "hero_force_edit"),
            ("Edit Name Tag Template", "tag_template_edit"),
            ("Edit Participant", "participant_edit"),
            ("Edit Room", "room_edit"),
            ("Edit Run", "run_edit"),
            ("Edit Signup Form", "signup_form_edit"),
            ("Edit User", "user_edit"),
            ("Edit Wear", "wear_edit"),
            ("Reset Entrance Assignments", "entrance_assignment_reset_edit"),
            ("Reset Food Distribution", "food_distribute_reset_edit"),
            ("Send email to participant","send_participant_email_edit"),
            ("Send notification to participant","send_participant_notification_edit"),
            ("Start run", "run_start_edit"),
            ("View Activity", "activity_view"),
            ("View Boardgames", "boardgame_view"),
            ("View Budget and Accounting", "accounting_view"),
            ("View Checkin Statistics", "checkin_stats_view"),
            ("View Entrance", "entrance_view"),
            ("View Food marketplace", "food_marketplace_view"),
            ("View Food Statistics", "food_stats_view"),
            ("View Food", "food_view"),
            ("View Game Votes", "vote_list_view"),
            ("View Hero Force Assignments", "hero_force_participant_list_view"),
            ("View Hero Force", "hero_force_view"),
            ("View Participant", "participant_view"),
            ("View Payments For Participant","payment_participant_view"),
            ("View Roles and privileges", "role_view"),
            ("View Room Assignments","room_assignment_view"),
            ("View Room", "room_view"),
            ("View Run", "run_view"),
            ("View Signup Statistics", "signup_stats_view"),
            ("View sleeping assignments","sleeping_assignment_view"),
            ("View System Log", "syslog_view"),
            ("View Users", "users_view"),
            ("View wear for Participant", "wear_participant_view"),
            ("View Wear", "wear_view"),
            ("View activity leader list", "activity_leader_list_view"),
            ("View karma list", "karma_list_view"),
            ("View name tag for Participant", "nametag_participant_view"),
            ("View Participant schedule", "participant_schedule_view"),
            ("View full wear list", "wear_list_view"),
        };

        foreach (var (name, key) in requiredPrivileges)
        {
            var priv = db.Privileges.FirstOrDefault(p => p.Key == key);
            if (priv == null)
            {
                priv = new Privilege { Name = name, Key = key };
                db.Privileges.Add(priv);
                db.SaveChanges();
            }

            if (!db.RolePrivileges.Any(rp => rp.RoleId == adminRole.Id && rp.PrivilegeId == priv.Id))
            {
                db.RolePrivileges.Add(new RolePrivilege { RoleId = adminRole.Id, PrivilegeId = priv.Id });
                db.SaveChanges();
            }
        }

        // Default Roles with default privileges

        seedRole(db, "Infonaut", "", []);
        seedRole(db, "Infoboss", "", []);
        seedRole(db, "Food distribution", "Food distribution role", []);
        seedRole(db, "Activity planner", "Activity planner, Scenario responsible, boardgame responsible", []);
        seedRole(db, "Boardgame distribution", "Boardgame distribution role", []);
        seedRole(db, "Signup", "Sign up", []);
        seedRole(db, "Hero Force", "Hero Force management", []);
        seedRole(db, "Activity Leader", "Game master, rule communicator, activity leader", []);
        seedRole(db, "Boardgame jury", "Jury member for boardgames", []);
        seedRole(db, "Scenario jury", "Jury member for scenarios", []);
        seedRole(db, "Read-only", "All reading privileges", [.. requiredPrivileges.Select(x => x.Item2).Where(x => x.EndsWith("_view"))]);
        seedRole(db, "Accounting", "Accounting and budget", []);

        // Default Admin User with the special admin role
        var adminUser = db.Users.FirstOrDefault(u => u.Username == "admin");
        if (adminUser == null)
        {
            adminUser = new User
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Email = "admin@fastaval.dk",
                FirstName = "Admin",
                LastName = "User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(adminUser);
            db.SaveChanges();

            db.UserRoles.Add(new UserRole { UserId = adminUser.Id, RoleId = adminRole.Id });
            db.SaveChanges();
        }
        else if (!adminUser.IsActive)
        {
            adminUser.IsActive = true;
            db.SaveChanges();
        }

        // Demo Data Seeding
        if (!db.Participants.Any())
        {
            var p1 = new Participant
            {
                FirstName = "Mads",
                LastName = "Hansen",
                Email = "mads@fastaval.dk",
                PhoneNumber = "+4512345678",
                BirthDate = new DateTime(1995, 5, 12, 0, 0, 0, DateTimeKind.Utc),
                Address = "Vestergade 12",
                ZipCode = "8000",
                City = "Aarhus",
                Country = "Denmark",
                Barcode = "FAST-2026-0001",
                IsCheckedIn = true,
                CheckedInAt = DateTime.UtcNow.AddHours(-2)
            };

            var p2 = new Participant
            {
                FirstName = "Sofie",
                LastName = "Nielsen",
                Email = "sofie@fastaval.dk",
                PhoneNumber = "+4587654321",
                BirthDate = new DateTime(1998, 9, 24, 0, 0, 0, DateTimeKind.Utc),
                Address = "Nørrebrogade 45",
                ZipCode = "2200",
                City = "København N",
                Country = "Denmark",
                Barcode = "FAST-2026-0002",
                IsCheckedIn = false
            };

            db.Participants.AddRange(p1, p2);

            // Demo Activities
            var act1 = new Activity
            {
                Title = "Shadows Over Fastaval",
                TitleEnglish = "Shadows Over Fastaval",
                Description = "An immersive Call of Cthulhu RPG scenario set in 1920s Arkham.",
                Author = "Lars Jensen",
                Category = "RPG",
                MinParticipants = 3,
                MaxParticipants = 6,
                DurationMinutes = 240,
                IsActive = true
            };

            var act2 = new Activity
            {
                Title = "Fastaval Board Game Championship",
                TitleEnglish = "Fastaval Board Game Championship",
                Description = "Competitive board gaming tournament featuring strategy classics.",
                Author = "Fastaval Board Game Team",
                Category = "Board Game",
                MinParticipants = 4,
                MaxParticipants = 16,
                DurationMinutes = 180,
                IsActive = true
            };

            db.Activities.AddRange(act1, act2);

            // Demo Hero Force Shifts
            var heroForceCategory = new HeroForceCategory
            {
                Name = "Info Desk",
                Description = "Information desk and check-in shifts",
                ColorHex = "#3f51b5"
            };
            db.HeroForceCategories.Add(heroForceCategory);
            db.SaveChanges();

            var shift1 = new HeroForceShift
            {
                CategoryId = heroForceCategory.Id,
                Title = "Info Desk Morning Shift",
                Description = "Help participants check in and answer questions.",
                StartTime = DateTime.UtcNow.AddDays(1).AddHours(8),
                EndTime = DateTime.UtcNow.AddDays(1).AddHours(12),
                MaxParticipants = 2
            };

            db.HeroForceShifts.Add(shift1);

            // Demo Boardgames
            db.Boardgames.AddRange(
                new Boardgame { Title = "Catan", Author = "Klaus Teuber", Publisher = "Kosmos", MinPlayers = 3, MaxPlayers = 4, PlayingTimeMinutes = 90, Barcode = "BG001", IsPresent = true },
                new Boardgame { Title = "Ticket to Ride", Author = "Alan R. Moon", Publisher = "Days of Wonder", MinPlayers = 2, MaxPlayers = 5, PlayingTimeMinutes = 60, Barcode = "BG002", IsPresent = true },
                new Boardgame { Title = "Codenames", Author = "Vlaada Chvátil", Publisher = "CGE", MinPlayers = 2, MaxPlayers = 8, PlayingTimeMinutes = 15, Barcode = "BG003", IsPresent = true }
            );

            // Demo Shop Products
            db.Products.AddRange(
                new Product { Name = "Fastaval 2026 T-Shirt", Description = "Official Fastaval 2026 cotton t-shirt", Price = 150.00m, Stock = 50, Category = "Wear", IsActive = true },
                new Product { Name = "Dice Set (7 pcs)", Description = "Polyhedral dice set with Fastaval logo", Price = 60.00m, Stock = 100, Category = "Merchandise", IsActive = true },
                new Product { Name = "Soda / Soft Drink", Description = "Cold 33cl soft drink", Price = 20.00m, Stock = 200, Category = "Kiosk", IsActive = true }
            );

            // Demo Food Types
            db.FoodTypes.AddRange(
                new FoodType { Name = "Morgenmad", NameEnglish = "Breakfast", Price = 45.00m, IsActive = true },
                new FoodType { Name = "Aftensmad (Kød)", NameEnglish = "Dinner (Meat)", Price = 85.00m, IsActive = true },
                new FoodType { Name = "Aftensmad (Vegansk)", NameEnglish = "Dinner (Vegan)", Price = 85.00m, IsActive = true }
            );

            // Demo Wear Items
            db.WearItems.AddRange(
                new WearItem { Name = "Fastaval Hoodie", Description = "Warm hoodie with embroidered logo", Price = 350.00m, Size = "L", Stock = 25 },
                new WearItem { Name = "Fastaval Cap", Description = "Adjustable snapback cap", Price = 120.00m, Size = "One Size", Stock = 40 }
            );

            // Demo Rooms
            db.Rooms.AddRange(
                new Room { Name = "Lokale A1", Location = "Bygning A, 1. sal", Capacity = 10, Description = "Quiet RPG room with 1 table and 8 chairs." },
                new Room { Name = "Brætspilshallen", Location = "Hovedbygningen", Capacity = 100, Description = "Large open hall for board games." }
            );

            // Demo Communication: Newsletters & Tickets
            db.Newsletters.Add(new Newsletter
            {
                Subject = "Velkommen til Fastaval 2026!",
                Body = "Information om program, spilafvikling og Hero Force / Heltestyrken vagter.",
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                SentAt = DateTime.UtcNow.AddDays(-5),
                RecipientCount = 2
            });

            db.Tickets.Add(new Ticket
            {
                Title = "Glemt kodeord til tilmelding",
                Description = "Kan ikke nulstille min adgangskode via email.",
                Status = TicketStatus.Open,
                CreatedById = adminUser.Id,
                CreatedAt = DateTime.UtcNow.AddHours(-6)
            });

            db.SaveChanges();
        }
    }

    private static void seedRole(InfosysDbContext db, string roleName, string roleDescription, string[] privilegeKeys)
    {
        var role = db.Roles.FirstOrDefault(r => r.Name == roleName);
        if (role == null)
        {
            role = new Role
            {
                Name = roleName,
                Description = roleDescription
            };
            db.Roles.Add(role);
            db.SaveChanges();

            // Give rights/privileges to the role
            foreach (var pKey in privilegeKeys)
            {
                var p = db.Privileges.FirstOrDefault(pr => pr.Key == pKey);
                if (p != null)
                {
                    db.RolePrivileges.Add(new RolePrivilege { RoleId = role.Id, PrivilegeId = p.Id });
                }
            }
        }
        db.SaveChanges();

    }
}
