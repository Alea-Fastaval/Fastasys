using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fastasys.ApiService.Data.Migrations
{
    /// <inheritdoc />
    public partial class PortParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF guessed these as renames; only Address is one. The other scaffold columns have no Infosys
            // counterpart that is still used (medical_note is always empty, the barcode is computed from the id,
            // IsCheckedIn duplicated CheckedInAt, and Country becomes the CountryCode foreign key).
            migrationBuilder.RenameColumn(
                name: "Address",
                table: "participants",
                newName: "AddressLine1");

            migrationBuilder.DropColumn(name: "MedicalInfo", table: "participants");
            migrationBuilder.DropColumn(name: "IsCheckedIn", table: "participants");
            migrationBuilder.DropColumn(name: "Country", table: "participants");
            migrationBuilder.DropColumn(name: "Barcode", table: "participants");

            migrationBuilder.AlterColumn<string>(
                name: "AddressLine1",
                table: "participants",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "AdminNote",
                table: "participants",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MessageToParticipant",
                table: "participants",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PaymentNote",
                table: "participants",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "participants",
                type: "text",
                nullable: false,
                defaultValue: "Danish");

            migrationBuilder.AddColumn<bool>(
                name: "SuperRulesGuide",
                table: "participants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "ZipCode",
                table: "participants",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "participants",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Nickname",
                table: "participants",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "participants",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "participants",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "participants",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "participants",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "BirthDate",
                table: "participants",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string[]>(
                name: "ActivityLanguages",
                table: "participants",
                type: "text[]",
                nullable: false,
                defaultValue: new[] { "Danish" });

            migrationBuilder.AddColumn<string>(
                name: "AddressLine2",
                table: "participants",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AmountPaid",
                table: "participants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "participants",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "CommentNote",
                table: "participants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "participants",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "DK");

            migrationBuilder.AddColumn<int>(
                name: "DesiredActivities",
                table: "participants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DesiredHeroTasks",
                table: "participants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExtraVouchers",
                table: "participants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "FinancialSupport",
                table: "participants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "GameActivityId",
                table: "participants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeroForceNote",
                table: "participants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActivityLocked",
                table: "participants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAnnulled",
                table: "participants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAuthorOrDesigner",
                table: "participants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsNoShow",
                table: "participants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "JuniorComment",
                table: "participants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JuniorContact",
                table: "participants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "JuniorGameMaster",
                table: "participants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxGameMasterRuns",
                table: "participants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxRulesGuideGames",
                table: "participants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrganizerAreaId",
                table: "participants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PhotoConsent",
                table: "participants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Pronouns",
                table: "participants",
                type: "character varying(4)",
                maxLength: 4,
                nullable: false,
                defaultValue: "none");

            migrationBuilder.AddColumn<bool>(
                name: "SetupHelpMonday",
                table: "participants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SetupHelpTuesday",
                table: "participants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedUpAt",
                table: "participants",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<string[]>(
                name: "SleepingAreas",
                table: "participants",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "SleepingNote",
                table: "participants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SmsSignup",
                table: "participants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SuperGameMaster",
                table: "participants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SuperHero",
                table: "participants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "participants",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<string>(
                name: "YouthClub",
                table: "participants",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "countries",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    NameDanish = table.Column<string>(type: "text", nullable: false),
                    NameEnglish = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_countries", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "organizer_areas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NameDanish = table.Column<string>(type: "text", nullable: false),
                    NameEnglish = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizer_areas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "participant_categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsOrganizer = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participant_categories", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "countries",
                columns: new[] { "Code", "NameDanish", "NameEnglish" },
                values: new object[,]
                {
                    { "AD", "Andorra", "Andorra" },
                    { "AE", "De Forenede Arabiske Emirater", "United Arab Emirates" },
                    { "AF", "Afghanistan", "Afghanistan" },
                    { "AG", "Antigua og Barbuda", "Antigua & Barbuda" },
                    { "AI", "Anguilla", "Anguilla" },
                    { "AL", "Albanien", "Albania" },
                    { "AM", "Armenien", "Armenia" },
                    { "AO", "Angola", "Angola" },
                    { "AR", "Argentina", "Argentina" },
                    { "AS", "Amerikansk Samoa", "American Samoa" },
                    { "AT", "Østrig", "Austria" },
                    { "AU", "Australien", "Australia" },
                    { "AW", "Aruba", "Aruba" },
                    { "AX", "Åland", "Åland Islands" },
                    { "AZ", "Aserbajdsjan", "Azerbaijan" },
                    { "BA", "Bosnien-Hercegovina", "Bosnia & Herzegovina" },
                    { "BB", "Barbados", "Barbados" },
                    { "BD", "Bangladesh", "Bangladesh" },
                    { "BE", "Belgien", "Belgium" },
                    { "BF", "Burkina Faso", "Burkina Faso" },
                    { "BG", "Bulgarien", "Bulgaria" },
                    { "BH", "Bahrain", "Bahrain" },
                    { "BI", "Burundi", "Burundi" },
                    { "BJ", "Benin", "Benin" },
                    { "BL", "Saint Barthélemy", "St. Barthélemy" },
                    { "BM", "Bermuda", "Bermuda" },
                    { "BN", "Brunei", "Brunei" },
                    { "BO", "Bolivia", "Bolivia" },
                    { "BQ", "De tidligere Nederlandske Antiller", "Caribbean Netherlands" },
                    { "BR", "Brasilien", "Brazil" },
                    { "BS", "Bahamas", "Bahamas" },
                    { "BT", "Bhutan", "Bhutan" },
                    { "BW", "Botswana", "Botswana" },
                    { "BY", "Hviderusland", "Belarus" },
                    { "BZ", "Belize", "Belize" },
                    { "CA", "Canada", "Canada" },
                    { "CC", "Cocosøerne", "Cocos (Keeling) Islands" },
                    { "CD", "Congo-Kinshasa", "Congo - Kinshasa" },
                    { "CF", "Den Centralafrikanske Republik", "Central African Republic" },
                    { "CG", "Congo-Brazzaville", "Congo - Brazzaville" },
                    { "CH", "Schweiz", "Switzerland" },
                    { "CI", "Elfenbenskysten", "Côte d’Ivoire" },
                    { "CK", "Cookøerne", "Cook Islands" },
                    { "CL", "Chile", "Chile" },
                    { "CM", "Cameroun", "Cameroon" },
                    { "CN", "Kina", "China" },
                    { "CO", "Colombia", "Colombia" },
                    { "CR", "Costa Rica", "Costa Rica" },
                    { "CU", "Cuba", "Cuba" },
                    { "CV", "Kap Verde", "Cape Verde" },
                    { "CW", "Curaçao", "Curaçao" },
                    { "CX", "Juleøen", "Christmas Island" },
                    { "CY", "Cypern", "Cyprus" },
                    { "CZ", "Tjekkiet", "Czechia" },
                    { "DE", "Tyskland", "Germany" },
                    { "DG", "Diego Garcia", "Diego Garcia" },
                    { "DJ", "Djibouti", "Djibouti" },
                    { "DK", "Danmark", "Denmark" },
                    { "DM", "Dominica", "Dominica" },
                    { "DO", "Den Dominikanske Republik", "Dominican Republic" },
                    { "DZ", "Algeriet", "Algeria" },
                    { "EA", "Ceuta og Melilla", "Ceuta & Melilla" },
                    { "EC", "Ecuador", "Ecuador" },
                    { "EE", "Estland", "Estonia" },
                    { "EG", "Egypten", "Egypt" },
                    { "EH", "Vestsahara", "Western Sahara" },
                    { "ER", "Eritrea", "Eritrea" },
                    { "ES", "Spanien", "Spain" },
                    { "ET", "Etiopien", "Ethiopia" },
                    { "FI", "Finland", "Finland" },
                    { "FJ", "Fiji", "Fiji" },
                    { "FK", "Falklandsøerne", "Falkland Islands" },
                    { "FM", "Mikronesien", "Micronesia" },
                    { "FO", "Færøerne", "Faroe Islands" },
                    { "FR", "Frankrig", "France" },
                    { "GA", "Gabon", "Gabon" },
                    { "GB", "Storbritannien", "United Kingdom" },
                    { "GD", "Grenada", "Grenada" },
                    { "GE", "Georgien", "Georgia" },
                    { "GF", "Fransk Guyana", "French Guiana" },
                    { "GG", "Guernsey", "Guernsey" },
                    { "GH", "Ghana", "Ghana" },
                    { "GI", "Gibraltar", "Gibraltar" },
                    { "GL", "Grønland", "Greenland" },
                    { "GM", "Gambia", "Gambia" },
                    { "GN", "Guinea", "Guinea" },
                    { "GP", "Guadeloupe", "Guadeloupe" },
                    { "GQ", "Ækvatorialguinea", "Equatorial Guinea" },
                    { "GR", "Grækenland", "Greece" },
                    { "GT", "Guatemala", "Guatemala" },
                    { "GU", "Guam", "Guam" },
                    { "GW", "Guinea-Bissau", "Guinea-Bissau" },
                    { "GY", "Guyana", "Guyana" },
                    { "HK", "SAR Hongkong", "Hong Kong SAR China" },
                    { "HN", "Honduras", "Honduras" },
                    { "HR", "Kroatien", "Croatia" },
                    { "HT", "Haiti", "Haiti" },
                    { "HU", "Ungarn", "Hungary" },
                    { "IC", "Kanariske øer", "Canary Islands" },
                    { "ID", "Indonesien", "Indonesia" },
                    { "IE", "Irland", "Ireland" },
                    { "IL", "Israel", "Israel" },
                    { "IM", "Isle of Man", "Isle of Man" },
                    { "IN", "Indien", "India" },
                    { "IO", "Det Britiske Territorium i Det Indiske Ocean", "British Indian Ocean Territory" },
                    { "IQ", "Irak", "Iraq" },
                    { "IR", "Iran", "Iran" },
                    { "IS", "Island", "Iceland" },
                    { "IT", "Italien", "Italy" },
                    { "JE", "Jersey", "Jersey" },
                    { "JM", "Jamaica", "Jamaica" },
                    { "JO", "Jordan", "Jordan" },
                    { "JP", "Japan", "Japan" },
                    { "KE", "Kenya", "Kenya" },
                    { "KG", "Kirgisistan", "Kyrgyzstan" },
                    { "KH", "Cambodja", "Cambodia" },
                    { "KI", "Kiribati", "Kiribati" },
                    { "KM", "Comorerne", "Comoros" },
                    { "KN", "Saint Kitts og Nevis", "St. Kitts & Nevis" },
                    { "KP", "Nordkorea", "North Korea" },
                    { "KR", "Sydkorea", "South Korea" },
                    { "KW", "Kuwait", "Kuwait" },
                    { "KY", "Caymanøerne", "Cayman Islands" },
                    { "KZ", "Kasakhstan", "Kazakhstan" },
                    { "LA", "Laos", "Laos" },
                    { "LB", "Libanon", "Lebanon" },
                    { "LC", "Saint Lucia", "St. Lucia" },
                    { "LI", "Liechtenstein", "Liechtenstein" },
                    { "LK", "Sri Lanka", "Sri Lanka" },
                    { "LR", "Liberia", "Liberia" },
                    { "LS", "Lesotho", "Lesotho" },
                    { "LT", "Litauen", "Lithuania" },
                    { "LU", "Luxembourg", "Luxembourg" },
                    { "LV", "Letland", "Latvia" },
                    { "LY", "Libyen", "Libya" },
                    { "MA", "Marokko", "Morocco" },
                    { "MC", "Monaco", "Monaco" },
                    { "MD", "Moldova", "Moldova" },
                    { "ME", "Montenegro", "Montenegro" },
                    { "MF", "Saint Martin", "St. Martin" },
                    { "MG", "Madagaskar", "Madagascar" },
                    { "MH", "Marshalløerne", "Marshall Islands" },
                    { "MK", "Nordmakedonien", "North Macedonia" },
                    { "ML", "Mali", "Mali" },
                    { "MM", "Myanmar (Burma)", "Myanmar (Burma)" },
                    { "MN", "Mongoliet", "Mongolia" },
                    { "MO", "SAR Macao", "Macao SAR China" },
                    { "MP", "Nordmarianerne", "Northern Mariana Islands" },
                    { "MQ", "Martinique", "Martinique" },
                    { "MR", "Mauretanien", "Mauritania" },
                    { "MS", "Montserrat", "Montserrat" },
                    { "MT", "Malta", "Malta" },
                    { "MU", "Mauritius", "Mauritius" },
                    { "MW", "Malawi", "Malawi" },
                    { "MX", "Mexico", "Mexico" },
                    { "MY", "Malaysia", "Malaysia" },
                    { "MZ", "Mozambique", "Mozambique" },
                    { "NA", "Namibia", "Namibia" },
                    { "NC", "Ny Kaledonien", "New Caledonia" },
                    { "NE", "Niger", "Niger" },
                    { "NF", "Norfolk Island", "Norfolk Island" },
                    { "NG", "Nigeria", "Nigeria" },
                    { "NI", "Nicaragua", "Nicaragua" },
                    { "NL", "Holland", "Netherlands" },
                    { "NO", "Norge", "Norway" },
                    { "NP", "Nepal", "Nepal" },
                    { "NR", "Nauru", "Nauru" },
                    { "NU", "Niue", "Niue" },
                    { "NZ", "New Zealand", "New Zealand" },
                    { "OM", "Oman", "Oman" },
                    { "PA", "Panama", "Panama" },
                    { "PE", "Peru", "Peru" },
                    { "PF", "Fransk Polynesien", "French Polynesia" },
                    { "PG", "Papua Ny Guinea", "Papua New Guinea" },
                    { "PH", "Filippinerne", "Philippines" },
                    { "PK", "Pakistan", "Pakistan" },
                    { "PL", "Polen", "Poland" },
                    { "PM", "Saint Pierre og Miquelon", "St. Pierre & Miquelon" },
                    { "PN", "Pitcairn", "Pitcairn Islands" },
                    { "PR", "Puerto Rico", "Puerto Rico" },
                    { "PS", "De palæstinensiske områder", "Palestinian Territories" },
                    { "PT", "Portugal", "Portugal" },
                    { "PW", "Palau", "Palau" },
                    { "PY", "Paraguay", "Paraguay" },
                    { "QA", "Qatar", "Qatar" },
                    { "RE", "Réunion", "Réunion" },
                    { "RO", "Rumænien", "Romania" },
                    { "RS", "Serbien", "Serbia" },
                    { "RU", "Rusland", "Russia" },
                    { "RW", "Rwanda", "Rwanda" },
                    { "SA", "Saudi-Arabien", "Saudi Arabia" },
                    { "SB", "Salomonøerne", "Solomon Islands" },
                    { "SC", "Seychellerne", "Seychelles" },
                    { "SD", "Sudan", "Sudan" },
                    { "SE", "Sverige", "Sweden" },
                    { "SG", "Singapore", "Singapore" },
                    { "SH", "St. Helena", "St. Helena" },
                    { "SI", "Slovenien", "Slovenia" },
                    { "SJ", "Svalbard og Jan Mayen", "Svalbard & Jan Mayen" },
                    { "SK", "Slovakiet", "Slovakia" },
                    { "SL", "Sierra Leone", "Sierra Leone" },
                    { "SM", "San Marino", "San Marino" },
                    { "SN", "Senegal", "Senegal" },
                    { "SO", "Somalia", "Somalia" },
                    { "SR", "Surinam", "Suriname" },
                    { "SS", "Sydsudan", "South Sudan" },
                    { "ST", "São Tomé og Príncipe", "São Tomé & Príncipe" },
                    { "SV", "El Salvador", "El Salvador" },
                    { "SX", "Sint Maarten", "Sint Maarten" },
                    { "SY", "Syrien", "Syria" },
                    { "SZ", "Eswatini", "Eswatini" },
                    { "TC", "Turks- og Caicosøerne", "Turks & Caicos Islands" },
                    { "TD", "Tchad", "Chad" },
                    { "TG", "Togo", "Togo" },
                    { "TH", "Thailand", "Thailand" },
                    { "TJ", "Tadsjikistan", "Tajikistan" },
                    { "TK", "Tokelau", "Tokelau" },
                    { "TL", "Timor-Leste", "Timor-Leste" },
                    { "TM", "Turkmenistan", "Turkmenistan" },
                    { "TN", "Tunesien", "Tunisia" },
                    { "TO", "Tonga", "Tonga" },
                    { "TR", "Tyrkiet", "Turkey" },
                    { "TT", "Trinidad og Tobago", "Trinidad & Tobago" },
                    { "TV", "Tuvalu", "Tuvalu" },
                    { "TW", "Taiwan", "Taiwan" },
                    { "TZ", "Tanzania", "Tanzania" },
                    { "UA", "Ukraine", "Ukraine" },
                    { "UG", "Uganda", "Uganda" },
                    { "UM", "Amerikanske oversøiske øer", "U.S. Outlying Islands" },
                    { "US", "USA", "United States" },
                    { "UY", "Uruguay", "Uruguay" },
                    { "UZ", "Usbekistan", "Uzbekistan" },
                    { "VA", "Vatikanstaten", "Vatican City" },
                    { "VC", "Saint Vincent og Grenadinerne", "St. Vincent & Grenadines" },
                    { "VE", "Venezuela", "Venezuela" },
                    { "VG", "De Britiske Jomfruøer", "British Virgin Islands" },
                    { "VI", "De Amerikanske Jomfruøer", "U.S. Virgin Islands" },
                    { "VN", "Vietnam", "Vietnam" },
                    { "VU", "Vanuatu", "Vanuatu" },
                    { "WF", "Wallis og Futuna", "Wallis & Futuna" },
                    { "WS", "Samoa", "Samoa" },
                    { "XK", "Kosovo", "Kosovo" },
                    { "YE", "Yemen", "Yemen" },
                    { "YT", "Mayotte", "Mayotte" },
                    { "ZA", "Sydafrika", "South Africa" },
                    { "ZM", "Zambia", "Zambia" },
                    { "ZW", "Zimbabwe", "Zimbabwe" }
                });

            migrationBuilder.InsertData(
                table: "organizer_areas",
                columns: new[] { "Id", "NameDanish", "NameEnglish" },
                values: new object[,]
                {
                    { 1, "Scenarieforfatter", null },
                    { 2, "Brætspilsdesigner", null },
                    { 3, "Arrangørkærlighed", null },
                    { 4, "Arrangørmessen", null },
                    { 5, "AV & Tech", null },
                    { 6, "Bar", null },
                    { 7, "Brætspilscafé", null },
                    { 8, "Brætspilsdesignansvarlig", null },
                    { 9, "Opsætning/nedpilning", null },
                    { 10, "Butikker", null },
                    { 11, "DirtBuster", null },
                    { 12, "Brætspilsdommer", null },
                    { 13, "Scenariedommer", null },
                    { 14, "Fastafamily", null },
                    { 15, "FastaQuests", null },
                    { 16, "Fastaval Junior", null },
                    { 17, "FTTF (Fastaval-translator-task-force)", null },
                    { 18, "Game Rush", null },
                    { 19, "TV", null },
                    { 20, "Heltestyrken", null },
                    { 21, "Holdlægning", null },
                    { 22, "Hovedarrangørgruppe", null },
                    { 23, "Indkøb & Transport", null },
                    { 24, "Infoen", null },
                    { 25, "International", null },
                    { 26, "IT", null },
                    { 27, "Kaffekro", null },
                    { 28, "Kiosk", null },
                    { 29, "Kommunikation", null },
                    { 30, "Køkken", null },
                    { 31, "Levnedsmiddel", null },
                    { 32, "Madudlevering", null },
                    { 33, "Miljø/affaldshåndtering", null },
                    { 34, "Morgenmad", null },
                    { 35, "Økonomi", null },
                    { 36, "Opvask", null },
                    { 37, "Otto-produktion", null },
                    { 38, "Ottofest", null },
                    { 39, "Ottos stue", null },
                    { 40, "Receptioner", null },
                    { 41, "Regelformidleransvarlig", null },
                    { 42, "Scenarieansvarlig", null },
                    { 43, "Sildesalat", null },
                    { 44, "SoMe", null },
                    { 45, "Spillederansvarlig", null },
                    { 46, "Spilstart (scenarier + brætspil)", null },
                    { 47, "Streaming (under Fastaval)", null },
                    { 48, "Streaming (under Ottoshow)", null },
                    { 49, "Super Søndag", null },
                    { 50, "Synopseakademiet", null },
                    { 51, "Tema og Wear", null },
                    { 52, "Tryghed & Tryghedsvært", null },
                    { 53, "Ungdom og Efterskolerne", null },
                    { 54, "Æresgæst", null },
                    { 55, "Æresotto", null },
                    { 56, "Grafik", null }
                });

            migrationBuilder.InsertData(
                table: "participant_categories",
                columns: new[] { "Id", "Description", "IsOrganizer", "Name" },
                values: new object[,]
                {
                    { 1, null, false, "Deltager" },
                    { 2, null, true, "Arrangør" },
                    { 3, null, false, "Junior" },
                    { 4, null, false, "Forældre" },
                    { 5, null, false, "Freeloader" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_participants_CategoryId",
                table: "participants",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_participants_CountryCode",
                table: "participants",
                column: "CountryCode");

            migrationBuilder.CreateIndex(
                name: "IX_participants_GameActivityId",
                table: "participants",
                column: "GameActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_participants_OrganizerAreaId",
                table: "participants",
                column: "OrganizerAreaId");

            migrationBuilder.AddForeignKey(
                name: "FK_participants_activities_GameActivityId",
                table: "participants",
                column: "GameActivityId",
                principalTable: "activities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_participants_countries_CountryCode",
                table: "participants",
                column: "CountryCode",
                principalTable: "countries",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_participants_organizer_areas_OrganizerAreaId",
                table: "participants",
                column: "OrganizerAreaId",
                principalTable: "organizer_areas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_participants_participant_categories_CategoryId",
                table: "participants",
                column: "CategoryId",
                principalTable: "participant_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_participants_activities_GameActivityId",
                table: "participants");

            migrationBuilder.DropForeignKey(
                name: "FK_participants_countries_CountryCode",
                table: "participants");

            migrationBuilder.DropForeignKey(
                name: "FK_participants_organizer_areas_OrganizerAreaId",
                table: "participants");

            migrationBuilder.DropForeignKey(
                name: "FK_participants_participant_categories_CategoryId",
                table: "participants");

            migrationBuilder.DropTable(
                name: "countries");

            migrationBuilder.DropTable(
                name: "organizer_areas");

            migrationBuilder.DropTable(
                name: "participant_categories");

            migrationBuilder.DropIndex(
                name: "IX_participants_CategoryId",
                table: "participants");

            migrationBuilder.DropIndex(
                name: "IX_participants_CountryCode",
                table: "participants");

            migrationBuilder.DropIndex(
                name: "IX_participants_GameActivityId",
                table: "participants");

            migrationBuilder.DropIndex(
                name: "IX_participants_OrganizerAreaId",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "ActivityLanguages",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "AddressLine2",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "AmountPaid",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "CommentNote",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "DesiredActivities",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "DesiredHeroTasks",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "ExtraVouchers",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "FinancialSupport",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "GameActivityId",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "HeroForceNote",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "IsActivityLocked",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "IsAnnulled",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "IsAuthorOrDesigner",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "IsNoShow",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "JuniorComment",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "JuniorContact",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "JuniorGameMaster",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "MaxGameMasterRuns",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "MaxRulesGuideGames",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "OrganizerAreaId",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "PhotoConsent",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "Pronouns",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "SetupHelpMonday",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "SetupHelpTuesday",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "SignedUpAt",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "SleepingAreas",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "SleepingNote",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "SmsSignup",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "SuperGameMaster",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "SuperHero",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "YouthClub",
                table: "participants");

            migrationBuilder.DropColumn(name: "AdminNote", table: "participants");
            migrationBuilder.DropColumn(name: "MessageToParticipant", table: "participants");
            migrationBuilder.DropColumn(name: "PaymentNote", table: "participants");
            migrationBuilder.DropColumn(name: "PreferredLanguage", table: "participants");
            migrationBuilder.DropColumn(name: "SuperRulesGuide", table: "participants");

            migrationBuilder.AlterColumn<string>(
                name: "AddressLine1",
                table: "participants",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.RenameColumn(
                name: "AddressLine1",
                table: "participants",
                newName: "Address");

            migrationBuilder.AddColumn<string>(
                name: "MedicalInfo",
                table: "participants",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsCheckedIn",
                table: "participants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "participants",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "participants",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "ZipCode",
                table: "participants",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "participants",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "Nickname",
                table: "participants",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "participants",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "participants",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "participants",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512);

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "participants",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<DateTime>(
                name: "BirthDate",
                table: "participants",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);
        }
    }
}
