using MimeKit;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using System.Net;
using System.Text;

namespace EMLWorker
{
    public interface IEMLParser
    {
        Task<List<FileContainer>> GetAttachments();
    }

    public class EMLParser : IEMLParser
    {
        private MimeMessage _message;

        public async Task LoadEML(byte[] emlContent)
        {
            using (var memory = new MemoryStream(emlContent))
            {
                _message = await MimeMessage.LoadAsync(memory);
            }
        }

        public async Task<List<FileContainer>> GetAttachments()
        {
            var fileContainerList = new List<FileContainer>();

            var attachments = _message.Attachments.ToList();

            foreach (var attachment in attachments)
            {
                using (var memory = new MemoryStream())
                {
                    if (attachment is MimePart)
                        ((MimePart)attachment).Content.DecodeTo(memory);
                    else
                        ((MessagePart)attachment).Message.WriteTo(memory);

                    var fileName = attachment.ContentDisposition?.FileName
                            ?? attachment.ContentType.Name;

                    var fileContainer = new FileContainer(fileName, memory.ToArray());

                    if (fileContainer.FileType == FileType.EML)
                    {
                        var emlParser = new EMLParser();
                        await emlParser.LoadEML(fileContainer.OriginalContent);
                        fileContainerList.AddRange(await emlParser.GetAttachments());
                    }

                    if (fileContainer.FileType != FileType.Unknown)
                    {
                        fileContainerList.Add(fileContainer);
                    }
                }
            }

            var sortedFiles = fileContainerList
            .OrderByDescending(f => f.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .ToList();

            return fileContainerList;
        }

        public async Task<byte[]> RenderEMailToPdf()
        {
            //await new BrowserFetcher().DownloadAsync();

            var executablePath = Path.Combine(
                AppContext.BaseDirectory,
                "ChromeHeadlessShell",
                "chrome-headless-shell.exe");

            await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                ExecutablePath = executablePath
            });

            var page = await browser.NewPageAsync();
            await page.SetContentAsync(CreateEmailHtml(_message));

            var options = new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true
            };

            return await page.PdfDataAsync(options);
        }

        private string CreateEmailHtml(MimeMessage message)
        {
            var sb = new StringBuilder();

            sb.AppendLine("""
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset="utf-8">
                    <style>
                        body {
                            font-family: Arial, sans-serif;
                            font-size: 11pt;
                        }

                        .header {
                            border: 1px solid #ccc;
                            padding: 10px;
                            margin-bottom: 20px;
                            background-color: #f5f5f5;
                        }

                        .header table {
                            width: 100%;
                            border-collapse: collapse;
                        }

                        .header td:first-child {
                            font-weight: bold;
                            width: 120px;
                        }

                        .attachments {
                            margin-top: 10px;
                        }

                        .body {
                            margin-top: 20px;
                        }
                    </style>
                </head>
                <body>
            """);

            sb.AppendLine("<div class='header'>");
            sb.AppendLine("<table>");

            sb.AppendLine($"<tr><td>Sender:</td><td>{WebUtility.HtmlEncode(message.From.ToString())}</td></tr>");
            sb.AppendLine($"<tr><td>Empfänger:</td><td>{WebUtility.HtmlEncode(message.To.ToString())}</td></tr>");
            sb.AppendLine($"<tr><td>Betreff:</td><td>{WebUtility.HtmlEncode(message.Subject)}</td></tr>");

            sb.AppendLine(
                $"<tr><td>Uhrzeit:</td><td>{message.Date.LocalDateTime:dd.MM.yyyy HH:mm:ss}</td></tr>");

            sb.AppendLine("<tr><td>Anhänge:</td><td>");

            var attachments = message.Attachments
                .Select(a =>
                {
                    return a.ContentDisposition?.FileName ?? 
                    a.ContentType.Name ?? 
                    "Unbekannt";
                })
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (attachments.Any())
            {
                sb.AppendLine("<ul>");

                foreach (var attachment in attachments)
                {
                    sb.AppendLine($"<li>{WebUtility.HtmlEncode(attachment)}</li>");
                }

                sb.AppendLine("</ul>");
            }
            else
            {
                sb.AppendLine("Keine Anhänge");
            }

            sb.AppendLine("</td></tr>");
            sb.AppendLine("</table>");
            sb.AppendLine("</div>");

            //sb.AppendLine("<div class='body'>");

            if (!string.IsNullOrWhiteSpace(message.HtmlBody))
            {
                sb.AppendLine(message.HtmlBody);
            }
            else
            {
                sb.AppendLine("<pre>");
                sb.AppendLine(WebUtility.HtmlEncode(message.TextBody));
                sb.AppendLine("</pre>");
            }

            //sb.AppendLine("</div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        public async Task<string> CreateJPL(string emlFileName, string attachmentFileName)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("########################################################");
            stringBuilder.Append(Environment.NewLine);
            stringBuilder.Append($"From = \"{ConvertToPLValue(_message.From.Mailboxes.FirstOrDefault()?.Address)}\"");
            stringBuilder.Append(Environment.NewLine);
            stringBuilder.Append($"To = \"{ConvertListToJPLValue(_message.To.Mailboxes.Select(x => x.Address).ToList())}\"");
            stringBuilder.Append(Environment.NewLine);
            stringBuilder.Append($"ReplyTo = \"{ConvertToPLValue(_message.ReplyTo.ToString())}\"");
            stringBuilder.Append(Environment.NewLine);
            stringBuilder.Append($"OriginalFileName = \"{ConvertToPLValue(emlFileName)}\"");
            stringBuilder.Append(Environment.NewLine);
            stringBuilder.Append($"OriginalAttachmentFileName = \"{ConvertToPLValue(attachmentFileName)}\"");
            stringBuilder.Append(Environment.NewLine);
            stringBuilder.Append($"MessageId = \"{ConvertToPLValue(_message.MessageId)}\"");
            stringBuilder.Append(Environment.NewLine);
            stringBuilder.Append($"Subject = \"{ConvertToPLValue(_message.Subject)}\"");
            stringBuilder.Append(Environment.NewLine);
            stringBuilder.Append($"CC = \"{ConvertListToJPLValue(_message.Cc.Mailboxes.Select(x => x.Address.ToString()).ToList())}\"");
            stringBuilder.Append(Environment.NewLine);
            if (_message.Date.LocalDateTime != DateTime.MinValue)
            {
                stringBuilder.Append($"DateReceived = \"{ConvertToPLValue(_message.Date.LocalDateTime.ToString("dd.MM.yyyy"))}\"");
                stringBuilder.Append(Environment.NewLine);
            }
            stringBuilder.Append($"MAILBOX = \"{ConvertToPLValue(_message.To.Mailboxes.FirstOrDefault()?.Address)}\"");
            stringBuilder.Append(Environment.NewLine);
            stringBuilder.Append("########################################################");
            stringBuilder.Append(Environment.NewLine);

            return stringBuilder.ToString();
        }

        internal static string ConvertToPLValue(string val)
        {
            if (string.IsNullOrEmpty(val))
                return "";
            val = val.Trim();
            val = val.Replace("\\", "\\\\");
            val = val.Replace(":", "\\:");
            val = val.Replace("\"", "\\\"");
            val = val.Replace(Environment.NewLine, "\\0D\\0A");
            return val;
        }

        private static string ConvertListToJPLValue(List<string> listValues)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (string listValue in listValues)
            {
                string jpValue = ConvertToPLValue(listValue);
                if (!string.IsNullOrEmpty(jpValue))
                {
                    if (stringBuilder.Length > 0)
                        stringBuilder.Append(";");
                    stringBuilder.Append(jpValue);
                }
            }
            return stringBuilder.ToString();
        }
    }
}
