using Consular.Api.Services;
using Consular.Shared.Entities;
using Consular.Shared.Enums;
using Xunit;

namespace Consular.Api.Tests;

public class AppointmentSlotAvailabilityTests
{
    private static AppointmentSlotTemplate MakeTemplate(TimeOnly startTime, int capaciteMax) => new()
    {
        Region = Region.North,
        Categorie = TypeServiceCategorie.Passeport,
        DayOfWeek = DayOfWeek.Monday,
        StartTime = startTime,
        CapaciteMax = capaciteMax
    };

    private static DateTime BookingAt(TimeOnly time) => new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc) + time.ToTimeSpan();

    [Fact]
    public void ComputeForDate_NoBookings_ReturnsFullCapacityForEachTemplate()
    {
        var templates = new[] { MakeTemplate(new TimeOnly(9, 0), 5), MakeTemplate(new TimeOnly(10, 0), 3) };

        var result = AppointmentSlotAvailability.ComputeForDate(templates, Array.Empty<DateTime>());

        Assert.Equal(2, result.Count);
        Assert.Equal(5, result[0].Remaining);
        Assert.Equal(3, result[1].Remaining);
    }

    [Fact]
    public void ComputeForDate_BookingsAtMatchingTime_ReduceRemaining()
    {
        var templates = new[] { MakeTemplate(new TimeOnly(9, 0), 5) };
        var booked = new[] { BookingAt(new TimeOnly(9, 0)), BookingAt(new TimeOnly(9, 0)) };

        var result = AppointmentSlotAvailability.ComputeForDate(templates, booked);

        Assert.Equal(3, Assert.Single(result).Remaining);
    }

    [Fact]
    public void ComputeForDate_BookingsAtOtherTime_DoNotAffectDifferentSlot()
    {
        var templates = new[] { MakeTemplate(new TimeOnly(9, 0), 5), MakeTemplate(new TimeOnly(10, 0), 5) };
        var booked = new[] { BookingAt(new TimeOnly(10, 0)) };

        var result = AppointmentSlotAvailability.ComputeForDate(templates, booked);

        Assert.Equal(5, result.Single(a => a.StartTime == new TimeOnly(9, 0)).Remaining);
        Assert.Equal(4, result.Single(a => a.StartTime == new TimeOnly(10, 0)).Remaining);
    }

    [Fact]
    public void ComputeForDate_BookingsExceedCapacity_RemainingClampedToZero()
    {
        var templates = new[] { MakeTemplate(new TimeOnly(9, 0), 2) };
        var booked = new[] { BookingAt(new TimeOnly(9, 0)), BookingAt(new TimeOnly(9, 0)), BookingAt(new TimeOnly(9, 0)) };

        var result = AppointmentSlotAvailability.ComputeForDate(templates, booked);

        Assert.Equal(0, Assert.Single(result).Remaining);
    }

    [Fact]
    public void TryValidateSlot_SlotHasCapacity_ReturnsTrueWithRemainingCount()
    {
        var templates = new[] { MakeTemplate(new TimeOnly(9, 0), 5) };
        var booked = new[] { BookingAt(new TimeOnly(9, 0)) };

        var isValid = AppointmentSlotAvailability.TryValidateSlot(templates, booked, new TimeOnly(9, 0), out var remaining);

        Assert.True(isValid);
        Assert.Equal(4, remaining);
    }

    [Fact]
    public void TryValidateSlot_SlotFull_ReturnsFalse()
    {
        var templates = new[] { MakeTemplate(new TimeOnly(9, 0), 1) };
        var booked = new[] { BookingAt(new TimeOnly(9, 0)) };

        var isValid = AppointmentSlotAvailability.TryValidateSlot(templates, booked, new TimeOnly(9, 0), out var remaining);

        Assert.False(isValid);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public void TryValidateSlot_NoTemplateForRequestedTime_ReturnsFalse()
    {
        var templates = new[] { MakeTemplate(new TimeOnly(9, 0), 5) };

        var isValid = AppointmentSlotAvailability.TryValidateSlot(templates, Array.Empty<DateTime>(), new TimeOnly(11, 0), out var remaining);

        Assert.False(isValid);
        Assert.Equal(0, remaining);
    }
}
