using System.Text.Json;
using Fastasys.ApiService.Controllers;

namespace Fastasys.Tests;

// Pins the totals of the Infosys Pris breakdown box (Deltagere::calcSignupTotal / calcRealTotal in
// entities/deltagere.php). The amounts behind them come when tickets, food, wear and activity signups are ported.
public class ParticipantPriceTests
{
    private static readonly ParticipantPriceDto Price = new(
        OriginalPrice: 1000,
        Entry: 400,
        Food: 150,
        Wear: 200,
        SignedUpActivities: 90,
        AssignedActivities: 30,
        Other: 75,
        Alea: 50);

    [Fact]
    public void SignupTotal_CountsTheActivitiesSignedUpFor()
    {
        // calcTotal(true): entry + food + activities from signup wishes + wear + Alea + other
        Assert.Equal(400 + 150 + 90 + 200 + 50 + 75, Price.SignupTotal);
    }

    [Fact]
    public void CheckInTotal_CountsTheActivitiesAssigned()
    {
        // calcTotal(false): the same with activities from the participant's seats
        Assert.Equal(400 + 150 + 30 + 200 + 50 + 75, Price.CheckInTotal);
    }

    [Fact]
    public void Totals_LeaveTheOriginalPriceOut()
    {
        // "Oprindelig pris fra tilmelding" is shown for comparison only
        var cheaper = Price with { OriginalPrice = 0 };

        Assert.Equal(Price.SignupTotal, cheaper.SignupTotal);
        Assert.Equal(Price.CheckInTotal, cheaper.CheckInTotal);
    }

    [Fact]
    public void Totals_AreSentToTheFrontend()
    {
        var json = JsonSerializer.Serialize(Price, JsonSerializerOptions.Web);

        Assert.Contains("\"signupTotal\":965", json);
        Assert.Contains("\"checkInTotal\":905", json);
    }
}
