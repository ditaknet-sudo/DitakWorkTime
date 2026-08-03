using System.Globalization;
using ClosedXML.Excel;
using Ditak.Attendance.Core.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ditak.Attendance.Core.Services;

public record DailyReportRow(DateOnly WorkDate, int WorkedMinutes, bool HasOpenShift, string WorkedHoursDisplay);
public record MonthlyReportDto(Guid EmployeeId, string FullName, int Year, int Month, int TotalMinutes, IReadOnlyList<DailyReportRow> Days);

public class ReportService
{
    private readonly AttendanceDbContext _db;

    public ReportService(AttendanceDbContext db)
    {
        _db = db;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<IReadOnlyList<DailyReportRow>> GetDailyAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var rows = await _db.AttendanceDaySummaries.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.WorkDate >= from && x.WorkDate <= to)
            .OrderBy(x => x.WorkDate)
            .ToListAsync(ct);

        return rows.Select(x => new DailyReportRow(
            x.WorkDate,
            x.WorkedMinutes,
            x.HasOpenShift,
            FormatHours(x.WorkedMinutes))).ToList();
    }

    public async Task<MonthlyReportDto> GetMonthlyAsync(Guid employeeId, int year, int month, CancellationToken ct = default)
    {
        var emp = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId, ct)
            ?? throw new InvalidOperationException("Employee not found.");

        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var days = await GetDailyAsync(employeeId, from, to, ct);
        return new MonthlyReportDto(emp.Id, emp.FullName, year, month, days.Sum(d => d.WorkedMinutes), days);
    }

    public async Task<byte[]> ExportExcelAsync(Guid employeeId, int year, int month, CancellationToken ct = default)
    {
        var report = await GetMonthlyAsync(employeeId, year, month, ct);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Attendance");
        ws.Cell(1, 1).Value = "Employee";
        ws.Cell(1, 2).Value = report.FullName;
        ws.Cell(2, 1).Value = "Period";
        ws.Cell(2, 2).Value = $"{report.Year}-{report.Month:00}";
        ws.Cell(3, 1).Value = "Total hours";
        ws.Cell(3, 2).Value = FormatHours(report.TotalMinutes);

        ws.Cell(5, 1).Value = "Date";
        ws.Cell(5, 2).Value = "Minutes";
        ws.Cell(5, 3).Value = "Hours";
        ws.Cell(5, 4).Value = "Open shift";
        var r = 6;
        foreach (var day in report.Days)
        {
            ws.Cell(r, 1).Value = day.WorkDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            ws.Cell(r, 2).Value = day.WorkedMinutes;
            ws.Cell(r, 3).Value = day.WorkedHoursDisplay;
            ws.Cell(r, 4).Value = day.HasOpenShift ? "Yes" : "No";
            r++;
        }

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportPdfAsync(Guid employeeId, int year, int month, CancellationToken ct = default)
    {
        var report = await GetMonthlyAsync(employeeId, year, month, ct);
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Header().Text($"Attendance — {report.FullName}").SemiBold().FontSize(18);
                page.Content().Column(col =>
                {
                    col.Item().Text($"Period: {report.Year}-{report.Month:00}");
                    col.Item().Text($"Total: {FormatHours(report.TotalMinutes)}");
                    col.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                        });
                        table.Header(h =>
                        {
                            h.Cell().Text("Date").SemiBold();
                            h.Cell().Text("Minutes").SemiBold();
                            h.Cell().Text("Hours").SemiBold();
                            h.Cell().Text("Open").SemiBold();
                        });
                        foreach (var day in report.Days)
                        {
                            table.Cell().Text(day.WorkDate.ToString("yyyy-MM-dd"));
                            table.Cell().Text(day.WorkedMinutes.ToString());
                            table.Cell().Text(day.WorkedHoursDisplay);
                            table.Cell().Text(day.HasOpenShift ? "Yes" : "No");
                        }
                    });
                });
            });
        });

        return doc.GeneratePdf();
    }

    public static string FormatHours(int minutes)
    {
        var h = minutes / 60;
        var m = minutes % 60;
        return $"{h}:{m:00}";
    }
}
