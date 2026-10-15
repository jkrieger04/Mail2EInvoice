using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Mail2EInvoice
{
    public class XMLParser
    {
        private readonly HttpClient _httpClient;
        private readonly string _url;

        public XMLParser(string url, string apiKey)
        {
            /*var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };*/

            _httpClient = new HttpClient();
            _url = url + "/classcon-einvoice/api/v1";
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        public async Task ProcessXml(FileContainer fileContainer)
        {
            string requestContent = JsonSerializer.Serialize(new ProcessRequest()
            {
                filename = Path.GetFileNameWithoutExtension(fileContainer.FileName).ToLowerInvariant(),
                attachments = new List<string> { "input_attachments" },
                additionalPages = new List<string>(),
                content = Convert.ToBase64String(fileContainer.OriginalContent),
                cmlOnly = fileContainer.PdfContent != null
            });

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_url}/documents")
            {
                Content = new StringContent(requestContent, Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(httpRequestMessage);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"EInvoice Converter Create Job Failed. Status Code: " +
                    $"{response.StatusCode}, Content: {await response.Content.ReadAsStringAsync()}");
            }

            var processResponse = JsonSerializer.Deserialize<ProcessResponse>(await response.Content.ReadAsStringAsync());

            var startTime = DateTime.Now;
            do
            {
                await Task.Delay(1000);

                var statusRequestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_url}/documents/{processResponse.id}");
                var statusResponse = await _httpClient.SendAsync(statusRequestMessage);
                if (!statusResponse.IsSuccessStatusCode)
                {
                    throw new Exception($"EInvoice Converter Get Job Status Failed. Status Code: " +
                        $"{statusResponse.StatusCode}, Content: {await statusResponse.Content.ReadAsStringAsync()}");
                }

                processResponse = JsonSerializer.Deserialize<ProcessResponse>(await statusResponse.Content.ReadAsStringAsync());
            } while (!processResponse.status.Equals("Finished") && !processResponse.status.Equals("Error") 
                        && DateTime.Now - startTime < TimeSpan.FromSeconds(300));

            if (processResponse.status.Equals("Error"))
            {
                throw new Exception($"EInvoice Converter Processing Failed. Error Message: {processResponse.errorMessage}");
            }

            if (processResponse.status.Equals("Finished"))
            {
                var formatRequestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_url}/documents/{processResponse.id}/cml");
                var formatResponse = await _httpClient.SendAsync(formatRequestMessage);

                if (!formatResponse.IsSuccessStatusCode)
                {
                    throw new Exception($"EInvoice Converter Get Documents Failed. Status Code: " +
                        $"{formatResponse.StatusCode}, Content: {await formatResponse.Content.ReadAsStringAsync()}");
                }

                var documents = JsonSerializer.Deserialize<List<DocumentResponse>>(await formatResponse.Content.ReadAsStringAsync());

                foreach (var document in documents)
                {
                    switch ((document.extension ?? "").ToLower())
                    {
                        case "pdf":
                            fileContainer.PdfContent = Convert.FromBase64String(document.content);
                            break;
                        case "xml":
                            fileContainer.CmlContent = Convert.FromBase64String(document.content);
                            break;
                    }
                }
            }
            else
            {
                throw new Exception($"EInvoice Converter Not Finished");
            }
        }

        public async Task CreateDefaultPdf(FileContainer fileContainer)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            QuestPDF.Settings.UseSystemFonts = true;
            string xmlContent = Encoding.UTF8.GetString(fileContainer.OriginalContent);

            using var resultStream = new MemoryStream();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontFamily("Arial"));

                    page.Content().Column(column =>
                    {
                        column.Spacing(40);

                        column.Item()
                            .AlignCenter()
                            .Text("PDF-Darstellung der E-Rechnung nicht verfügbar")
                            .FontSize(20)
                            .SemiBold();

                        column.Item().AlignCenter().Text(
                            "Für die vorliegende E-Rechnung konnte keine darstellbare PDF-Ansicht erzeugt werden.");

                        column.Item().AlignCenter().Text(
                            "Die Ursache kann ein technisches Problem bei der Verarbeitung der Rechnung oder eine fehlerhafte bzw. ungültige E-Rechnungsdatei sein. Daher ist eine automatische Visualisierung der enthaltenen Rechnungsdaten derzeit nicht möglich.");

                        column.Item().AlignCenter().Text(
                            "Bitte prüfen Sie die zugrunde liegende XML-Datei oder wenden Sie sich an den Rechnungssteller, um eine korrigierte Version der E-Rechnung zu erhalten.");

                        column.Item().AlignCenter().AlignCenter().PaddingTop(30).Text("Hinweis:")
                            .Bold();

                        column.Item().AlignCenter().Text("Die Nichtverfügbarkeit der PDF-Darstellung bedeutet nicht zwangsläufig, dass die Rechnungsdaten unvollständig sind. Für die weitere Analyse ist die originale XML-Datei heranzuziehen.");

                        column.Item().AlignCenter().Text("Der Inhalt der XML-Datei ist auf den nachfolgenden Seiten abgebildet.");
                    });
                });

                container.Page(page =>
                {
                    page.Margin(20);

                    page.Content()
                        .Text(xmlContent)
                        .FontFamily("Arial")
                        .FontSize(10);
                });
            })
            .GeneratePdf(resultStream);

            fileContainer.PdfContent = resultStream.ToArray();
        }
    }

    public class ProcessRequest
    {
        public string filename { get; set; }

        public List<string> attachments { get; set; }

        public List<string> additionalPages { get; set; }

        public string content { get; set; }

        public bool cmlOnly { get; set; }
    }

    public class ProcessResponse
    {
        public string id { get; set; }

        public string status { get; set; }

        public string errorMessage { get; set; }
    }

    public class DocumentResponse
    {
        public string id { get; set; }

        public string filename { get; set; }

        public string format { get; set; }

        public string extension { get; set; }

        public string content { get; set; }
    }
}
