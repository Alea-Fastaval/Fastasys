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
        {
            ("View Users", "users_view"),
            ("Edit/Create Users", "users_edit"),
            ("View Boardgames", "boardgames_view"),
            ("Edit/Create Boardgames", "boardgames_edit"),
            ("View Participants", "participants_view"),
            ("Edit/Create Participants", "participants_edit"),
            ("View Activities", "activities_view"),
            ("Edit/Create Activities", "activities_edit"),
            ("View Hero Force", "hero_force_view"),
            ("Edit/Create Hero Force", "hero_force_edit"),
            ("View Food", "food_view"),
            ("Edit/Create Food", "food_edit"),
            ("View Wear", "wear_view"),
            ("Edit/Create Wear", "wear_edit"),
            ("View Rooms", "rooms_view"),
            ("Edit/Create Rooms", "rooms_edit")
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
            }
        }

        // Seed Organizer Role
        var organizerRole = db.Roles.FirstOrDefault(r => r.Name == "Organizer");
        if (organizerRole == null)
        {
            organizerRole = new Role
            {
                Name = "Organizer",
                Description = "Event Organizer with view & manage rights for convention operations"
            };
            db.Roles.Add(organizerRole);
            db.SaveChanges();

            // Give view rights to Organizer
            var viewKeys = new[] { "users_view", "boardgames_view", "participants_view", "activities_view", "hero_force_view", "food_view", "wear_view", "rooms_view" };
            foreach (var vKey in viewKeys)
            {
                var p = db.Privileges.FirstOrDefault(pr => pr.Key == vKey);
                if (p != null)
                {
                    db.RolePrivileges.Add(new RolePrivilege { RoleId = organizerRole.Id, PrivilegeId = p.Id });
                }
            }
        }
        db.SaveChanges();

        // Default Admin User
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
                Title = "Skygger over Fastaval",
                TitleEnglish = "Shadows Over Fastaval",
                Description = "Et stemningsfuldt rædselsscenarie i 1920'ernes Arkham.",
                DescriptionEnglish = "An immersive horror scenario set in 1920s Arkham.",
                Author = "Lars Jensen",
                Type = ActivityType.Scenario,
                DurationHours = 4,
                MinPlayersPerTeam = 3,
                MaxPlayersPerTeam = 5,
                GameMastersPerTeam = 1,
                Price = 30,
                MinAge = 15,
                KarmaType = KarmaType.Big,
                PlayerLanguage = ActivityLanguage.Danish,
                GameMasterLanguage = ActivityLanguage.DanishAndEnglish
            };

            var act2 = new Activity
            {
                Title = "Fastaval Brætspilsmesterskab",
                TitleEnglish = "Fastaval Board Game Championship",
                Description = "Turnering i klassiske strategispil.",
                DescriptionEnglish = "Competitive board gaming tournament featuring strategy classics.",
                Author = "Fastaval Board Game Team",
                Type = ActivityType.BoardGame,
                DurationHours = 3,
                MinPlayersPerTeam = 4,
                MaxPlayersPerTeam = 16,
                GameMastersPerTeam = 1,
                Price = 20,
                MaxSignups = 64,
                IsReplayable = true,
                KarmaType = KarmaType.Small,
                PlayerLanguage = ActivityLanguage.DanishAndEnglish,
                GameMasterLanguage = ActivityLanguage.DanishAndEnglish
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
}
