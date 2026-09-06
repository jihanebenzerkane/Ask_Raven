using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Raven.Services
{
    public class CsvExportService
    {
        public byte[] GenerateCsv(string[] headers, string[][] data)
        {
            using var memoryStream = new MemoryStream();
            using var writer = new StreamWriter(memoryStream, new UTF8Encoding(true)); // with BOM for Excel

            // Write headers
            writer.WriteLine(string.Join(",", EscapeCsvRow(headers)));

            // Write data
            foreach (var row in data)
            {
                writer.WriteLine(string.Join(",", EscapeCsvRow(row)));
            }

            writer.Flush();
            return memoryStream.ToArray();
        }

        private IEnumerable<string> EscapeCsvRow(string[] row)
        {
            var escapedRow = new string[row.Length];
            for (int i = 0; i < row.Length; i++)
            {
                string field = row[i] ?? string.Empty;
                // Escape quotes
                if (field.Contains("\""))
                {
                    field = field.Replace("\"", "\"\"");
                }
                // Wrap in quotes if it contains comma, quote, or newline
                if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
                {
                    field = $"\"{field}\"";
                }
                escapedRow[i] = field;
            }
            return escapedRow;
        }
    }
}

