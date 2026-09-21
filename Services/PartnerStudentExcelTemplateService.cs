using OfficeOpenXml;

namespace QdratNew.Services
{
    public class PartnerStudentExcelTemplateService
    {
        public byte[] GenerateTemplate()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var sheet = package.Workbook.Worksheets.Add("Students");

            // Header
            sheet.Cells[1, 1].Value = "FullName";
            sheet.Cells[1, 2].Value = "NationalID";
            sheet.Cells[1, 3].Value = "Phone";
            sheet.Cells[1, 4].Value = "Gender";
            sheet.Cells[1, 5].Value = "Level";

            using (var range = sheet.Cells[1, 1, 1, 5])
            {
                range.Style.Font.Bold = true;
                range.AutoFitColumns();
            }

            // Example row (اختياري – مفيد)
            sheet.Cells[2, 1].Value = "محمد أحمد علي";
            sheet.Cells[2, 2].Value = "1023456789";
            sheet.Cells[2, 3].Value = "0500000000";
            sheet.Cells[2, 4].Value = "ذكر";
            sheet.Cells[2, 5].Value = "ثالث ثانوي";

            return package.GetAsByteArray();
        }
    }
}
