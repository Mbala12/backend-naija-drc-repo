using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Consular.Api.Dtos;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace Consular.Api.Services;

// Serializes the raw export rows (see ReportAggregationService.BuildExportRowsAsync) to the
// three formats ReportsController offers — hand-written CSV (no dependency needed for something
// this simple), XLSX via ClosedXML, and PDF via PdfSharp. Both ClosedXML and PdfSharp are
// MIT-licensed — deliberately avoided anything with revenue-tiered commercial licensing (e.g.
// QuestPDF) given this runs inside an embassy's own system, not a company with a known revenue
// bracket.
public static class ReportExportWriter
{
    private static readonly string[] Headers =
    {
        "Référence", "Type de service", "Statut", "Équipe", "Date de dépôt", "Dernière mise à jour", "Jours de traitement"
    };

    static ReportExportWriter()
    {
        // PdfSharp 6 ships no fonts of its own, and the backend's runtime container has none
        // installed by default — this points it at fonts-dejavu-core (installed in
        // Consular.Api/Dockerfile) instead of relying on a platform font resolver that doesn't
        // exist for a headless Linux container.
        GlobalFontSettings.FontResolver = new DejaVuFontResolver();
    }

    public static string WriteCsv(IEnumerable<ReportExportRowDto> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', Headers.Select(EscapeCsvField)));

        foreach (var row in rows)
        {
            var fields = new[]
            {
                row.NumeroReference,
                row.TypeServiceLibelle,
                row.StatutLibelle,
                row.EquipeAssignee,
                row.DateDepot.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                row.UpdatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                row.ProcessingDays?.ToString(CultureInfo.InvariantCulture) ?? ""
            };
            sb.AppendLine(string.Join(',', fields.Select(EscapeCsvField)));
        }

        return sb.ToString();
    }

    private static string EscapeCsvField(string field)
    {
        if (field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return field;
        return $"\"{field.Replace("\"", "\"\"")}\"";
    }

    public static byte[] WriteExcel(IEnumerable<ReportExportRowDto> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Rapport");

        for (var i = 0; i < Headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = Headers[i];
            sheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        var rowIndex = 2;
        foreach (var row in rows)
        {
            sheet.Cell(rowIndex, 1).Value = row.NumeroReference;
            sheet.Cell(rowIndex, 2).Value = row.TypeServiceLibelle;
            sheet.Cell(rowIndex, 3).Value = row.StatutLibelle;
            sheet.Cell(rowIndex, 4).Value = row.EquipeAssignee;
            sheet.Cell(rowIndex, 5).Value = row.DateDepot;
            sheet.Cell(rowIndex, 5).Style.DateFormat.Format = "yyyy-mm-dd";
            sheet.Cell(rowIndex, 6).Value = row.UpdatedAt;
            sheet.Cell(rowIndex, 6).Style.DateFormat.Format = "yyyy-mm-dd";
            if (row.ProcessingDays.HasValue) sheet.Cell(rowIndex, 7).Value = row.ProcessingDays.Value;
            rowIndex++;
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // Same widths the report's columns actually need — "Type de service"/"Statut" get the most
    // room since their labels (e.g. "Certificat de naissance") run longest. Right-aligned columns
    // (just "Jours de traitement" today) are marked so numbers line up on their ones digit like a
    // spreadsheet, not a paragraph.
    private static readonly double[] ColumnWidths = { 100, 130, 110, 55, 80, 118, 100 };
    private static readonly bool[] RightAlignColumn = { false, false, false, false, false, false, true };

    // Reuses the app's own navy brand color (--navy in index.css) for the header band rather than
    // inventing a new palette for this one document — same principle as the Reports tab's charts.
    private static readonly XColor NavyColor = XColor.FromArgb(0, 114, 206);
    private static readonly XColor HeaderTextColor = XColors.White;
    private static readonly XColor MutedTextColor = XColor.FromArgb(90, 100, 114);
    private static readonly XColor RowStripeColor = XColor.FromArgb(244, 247, 250);
    private static readonly XColor RowLineColor = XColor.FromArgb(216, 220, 226);

    private const double Margin = 30;
    private const double RowHeight = 18;
    private const double HeaderRowHeight = 22;
    private const double CellPadding = 5;
    private const double FooterHeight = 24;

    public static byte[] WritePdf(IEnumerable<ReportExportRowDto> rows, string title)
    {
        var rowList = rows as IReadOnlyList<ReportExportRowDto> ?? rows.ToList();

        var document = new PdfDocument();
        document.Info.Title = title;

        var titleFont = new XFont("DejaVu Sans", 14, XFontStyleEx.Bold);
        var subtitleFont = new XFont("DejaVu Sans", 8.5, XFontStyleEx.Regular);
        var headerFont = new XFont("DejaVu Sans", 8, XFontStyleEx.Bold);
        var cellFont = new XFont("DejaVu Sans", 7.5, XFontStyleEx.Regular);
        var footerFont = new XFont("DejaVu Sans", 7.5, XFontStyleEx.Regular);

        var pageWidth = XUnit.FromPoint(0);
        var pageHeight = XUnit.FromPoint(0);
        var tableWidth = ColumnWidths.Sum();
        const double titleBlockHeight = 48; // title line + subtitle line + spacing before the table

        // Deterministic pagination: every page has the same title/header/footer chrome, so the
        // usable row budget per page is a constant — no need for a two-pass render just to know
        // "page N of M" while drawing page N.
        double UsableRowsPerPage(double totalPageHeight) =>
            Math.Floor((totalPageHeight - Margin - titleBlockHeight - HeaderRowHeight - FooterHeight - Margin) / RowHeight);

        PdfPage page = null!;
        XGraphics gfx = null!;
        double y = 0;
        var pageIndex = 0;
        var totalPages = 1;

        string Truncate(string text, XFont font, double maxWidth)
        {
            if (gfx.MeasureString(text, font).Width <= maxWidth) return text;
            const string ellipsis = "…";
            var truncated = text;
            while (truncated.Length > 1 && gfx.MeasureString(truncated + ellipsis, font).Width > maxWidth)
            {
                truncated = truncated[..^1];
            }
            return truncated + ellipsis;
        }

        void DrawFooter()
        {
            var footerY = page.Height.Point - Margin + 4;
            gfx.DrawLine(new XPen(RowLineColor, 0.75), Margin, footerY, Margin + tableWidth, footerY);
            var pageLabel = $"Page {pageIndex} sur {totalPages}";
            gfx.DrawString(pageLabel, footerFont, new XSolidBrush(MutedTextColor),
                new XRect(Margin, footerY + 4, tableWidth, 14), XStringFormats.TopRight);
        }

        void DrawHeaderRow()
        {
            gfx.DrawRectangle(new XSolidBrush(NavyColor), Margin, y, tableWidth, HeaderRowHeight);
            var x = Margin;
            for (var i = 0; i < Headers.Length; i++)
            {
                var maxWidth = ColumnWidths[i] - 2 * CellPadding;
                var text = Truncate(Headers[i], headerFont, maxWidth);
                var cellRect = new XRect(x + CellPadding, y, maxWidth, HeaderRowHeight);
                gfx.DrawString(text, headerFont, new XSolidBrush(HeaderTextColor), cellRect,
                    RightAlignColumn[i] ? XStringFormats.CenterRight : XStringFormats.CenterLeft);
                x += ColumnWidths[i];
            }
            y += HeaderRowHeight;
        }

        void StartPage()
        {
            pageIndex++;
            page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            page.Orientation = PdfSharp.PageOrientation.Landscape;
            pageWidth = page.Width;
            pageHeight = page.Height;
            gfx = XGraphics.FromPdfPage(page);
            y = Margin;

            gfx.DrawString(title, titleFont, XBrushes.Black, new XRect(Margin, y, pageWidth.Point - 2 * Margin, 20), XStringFormats.TopLeft);
            y += 20;
            var subtitle = $"{rowList.Count} dossier(s) — généré le {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC";
            gfx.DrawString(subtitle, subtitleFont, new XSolidBrush(MutedTextColor), new XRect(Margin, y, pageWidth.Point - 2 * Margin, 16), XStringFormats.TopLeft);
            y += 20;

            DrawHeaderRow();
        }

        // Every page has the same geometry (A4 landscape, fixed chrome heights), so the row budget
        // per page is a constant — compute totalPages from A4's known short side (210mm, the
        // landscape height) rather than instantiating a throwaway page just to read it back.
        var landscapeHeightPt = XUnit.FromMillimeter(210).Point;
        var rowsPerPage = Math.Max(1, (int)UsableRowsPerPage(landscapeHeightPt));
        totalPages = Math.Max(1, (int)Math.Ceiling(rowList.Count / (double)rowsPerPage));

        StartPage();

        for (var i = 0; i < rowList.Count; i++)
        {
            if (y + RowHeight > pageHeight.Point - Margin - FooterHeight)
            {
                DrawFooter();
                StartPage();
            }

            var row = rowList[i];
            if (i % 2 == 1)
            {
                gfx.DrawRectangle(new XSolidBrush(RowStripeColor), Margin, y, tableWidth, RowHeight);
            }

            var cells = new[]
            {
                row.NumeroReference,
                row.TypeServiceLibelle,
                row.StatutLibelle,
                row.EquipeAssignee,
                row.DateDepot.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                row.UpdatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                row.ProcessingDays?.ToString("0.0", CultureInfo.InvariantCulture) ?? "—"
            };

            var x = Margin;
            for (var col = 0; col < cells.Length; col++)
            {
                var maxWidth = ColumnWidths[col] - 2 * CellPadding;
                var text = Truncate(cells[col], cellFont, maxWidth);
                var cellRect = new XRect(x + CellPadding, y, maxWidth, RowHeight);
                gfx.DrawString(text, cellFont, XBrushes.Black, cellRect, RightAlignColumn[col] ? XStringFormats.CenterRight : XStringFormats.CenterLeft);
                x += ColumnWidths[col];
            }

            y += RowHeight;
            gfx.DrawLine(new XPen(RowLineColor, 0.5), Margin, y, Margin + tableWidth, y);
        }

        DrawFooter();

        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    // Reads the two DejaVu Sans faces straight off disk rather than depending on fontconfig
    // (which a minimal ASP.NET runtime container doesn't have configured) — see the static
    // constructor above for where this gets registered.
    private class DejaVuFontResolver : IFontResolver
    {
        private const string RegularPath = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";
        private const string BoldPath = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf";

        public byte[] GetFont(string faceName) => File.ReadAllBytes(faceName == "DejaVuSans#Bold" ? BoldPath : RegularPath);

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
            new(isBold ? "DejaVuSans#Bold" : "DejaVuSans#Regular");
    }
}
