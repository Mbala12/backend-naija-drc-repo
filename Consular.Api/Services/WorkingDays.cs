namespace Consular.Api.Services;

// Weekend-skipping day arithmetic for the applicant self-service deadlines: 10 working days to
// submit missing documents (from entering MISSING_DOCUMENTS) or to appeal a rejection (from
// entering REJECTED). Doesn't account for public holidays — good enough for MVP scope.
public static class WorkingDays
{
    public const int AppealAndDocumentsDeadlineDays = 10;

    public static DateTime AddWorkingDays(DateTime start, int days)
    {
        var date = start;
        var added = 0;
        while (added < days)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
            {
                added++;
            }
        }
        return date;
    }
}
