using PdfSharp.Pdf.IO;

namespace Mail2EInvoice
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly EMLWorkerConfiguration _configuration;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            _configuration = EMLWorkerConfigurationHelper.Load();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var xmlParser = new XMLParser(_configuration.EInvoiceConverterURL, _configuration.EInvoiceConverterApiKey);

            _logger.LogInformation($"ProcessingWorker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var configurationFolder in _configuration.ConfigurationFolders)
                {
                    if (!Directory.Exists(configurationFolder.SourceDirectory))
                    {
                        _logger.LogInformation($"Soruce Directory Does Not Exist: {configurationFolder.SourceDirectory}");
                        continue;
                    }

                    var emlFiles = Directory.GetFiles(configurationFolder.SourceDirectory, "*.eml");

                    foreach (var emlFile in emlFiles)
                    {
                        var emlFileName = Path.GetFileName(emlFile);
                        _logger.LogInformation($"Start Process EML File: {emlFileName}");
                        try
                        {
                            var emlContent = await File.ReadAllBytesAsync(emlFile);

                            var emlParser = new EMLParser();

                            await emlParser.LoadEML(emlContent);

                            var fileContainerList = await emlParser.GetAttachments();

                            byte[] emailBodyContent = null;
                            if (configurationFolder.AttachEMail || (fileContainerList.Count == 0 && configurationFolder.ProcessEmlWithoutAttachment))
                            {
                                emailBodyContent = await emlParser.RenderEMailToPdf();
                            }

                            if (!configurationFolder.SupportXml)
                            {
                                fileContainerList.RemoveAll(x => x.FileType == FileType.XML);
                            }
                            else if (configurationFolder.UsePdfForXml)
                            {
                                var containersToRemove = new List<string>();
                                foreach (var xmlFileContainer in fileContainerList.Where(f => 
                                            f.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
                                {
                                    var pdfSearchFileName = Path.GetFileNameWithoutExtension(xmlFileContainer.FileName) + ".pdf";

                                    var pdfFileContainer = fileContainerList.SingleOrDefault(f => 
                                                                f.FileName.Equals(pdfSearchFileName, StringComparison.OrdinalIgnoreCase));

                                    if (pdfFileContainer != null)
                                    {
                                        xmlFileContainer.PdfContent = pdfFileContainer.OriginalContent;
                                        containersToRemove.Add(pdfFileContainer.FileName);
                                    }
                                }
                                fileContainerList.RemoveAll(x => containersToRemove.Contains(x.FileName));
                            }

                            if (fileContainerList.Count == 0 && configurationFolder.ProcessEmlWithoutAttachment)
                            {
                                _logger.LogInformation($"No Attachment Found. Process EMail Without Attachment");

                                var emlWithoutAttachmentFileContainer = new FileContainer(Path.ChangeExtension(emlFileName, ".pdf"), emailBodyContent);
                                fileContainerList.Add(emlWithoutAttachmentFileContainer);
                                emailBodyContent = null;
                            }

                            foreach (var fileContainer in fileContainerList)
                            {
                                switch (fileContainer.FileType)
                                {
                                    case FileType.PDF:
                                        fileContainer.PdfContent = fileContainer.OriginalContent;
                                        break;
                                    case FileType.XML:
                                        try
                                        {
                                            await xmlParser.ProcessXml(fileContainer);
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogError(ex, "Failed To Process XML. Start Retry");
                                            await Task.Delay(3000);
                                            try
                                            {
                                                await xmlParser.ProcessXml(fileContainer);
                                            }
                                            catch (Exception ex2)
                                            {
                                                _logger.LogError(ex2, "Retry Failed. Use Default PDF");

                                                fileContainer.PdfContent = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Default.pdf"));
                                            }
                                        }                                          
                                        break;
                                }

                                if (emailBodyContent != null)
                                {
                                    await AttachEMailBody(fileContainer, emailBodyContent);
                                }

                                string emailJplContent = null;
                                if (configurationFolder.CreateJpl)
                                {
                                    emailJplContent = await emlParser.CreateJPL(emlFileName, fileContainer.FileName);
                                }

                                await ExportFileContainer(fileContainer, emlContent, emailJplContent, configurationFolder.TargetDirectory);
                            }

                            File.Move(emlFile, GetUniqueFilePath(configurationFolder.BackupDirectory, emlFileName));

                            _logger.LogInformation($"Finished Process EML File: {emlFileName}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Failed To Process EML File: {emlFileName}");

                            try
                            {
                                File.Move(emlFile, GetUniqueFilePath(configurationFolder.ErrorDirectory, emlFileName));
                            }
                            catch (Exception ex2)
                            {
                                _logger.LogError(ex2, $"Failed To Move EML File in Error Folder: {emlFileName}");
                            }       
                        }
                    }
                }

                try
                {
                    await Task.Delay(5000, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation($"ProcessingWorker stopping.");
        }

        private async Task AttachEMailBody(FileContainer fileContainer, byte[] emailContent)
        {
            using var targetStream = new MemoryStream(fileContainer.PdfContent);

            var targetPdf = PdfReader.Open(targetStream, PdfDocumentOpenMode.Modify);

            using var sourceStream = new MemoryStream(emailContent);
            var sourcePdf = PdfReader.Open(sourceStream, PdfDocumentOpenMode.Import);

            foreach (var page in sourcePdf.Pages)
            {
                targetPdf.AddPage(page);
            }

            using var resultStream = new MemoryStream();
            targetPdf.Save(resultStream);

            fileContainer.PdfContent = resultStream.ToArray();
        }

        private async Task ExportFileContainer(FileContainer file, byte[] emlContent, string jplContent, string targetDirectory)
        {
            string targetFilePath = GetUniqueFilePath(targetDirectory, file.FileName);

            await File.WriteAllBytesAsync(Path.ChangeExtension(targetFilePath, ".eml"), emlContent);

            if (!string.IsNullOrEmpty(jplContent))
            {
                await File.WriteAllTextAsync(Path.ChangeExtension(targetFilePath, ".jpl"), jplContent);
            }

            if (file.FileType == FileType.XML)
            {
                await File.WriteAllBytesAsync(Path.ChangeExtension(targetFilePath, ".xml"), file.OriginalContent);

                if (file.CmlContent != null)
                {
                    await File.WriteAllBytesAsync(Path.ChangeExtension(targetFilePath, ".cml"), file.CmlContent);
                }
            }

            await File.WriteAllBytesAsync(Path.ChangeExtension(targetFilePath, ".pdf"), file.PdfContent);

            _logger.LogInformation($"Exported EML Attachment: {file.FileName}");
        }

        private string GetUniqueFilePath(string directory, string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);

            string uniqueFileName = $"{name}{extension}";
            int counter = 1;

            while (File.Exists(Path.Combine(directory, uniqueFileName)))
            {
                uniqueFileName = $"{name}_{counter}{extension}";
                counter++;
            }

            return Path.Combine(directory, uniqueFileName);
        }
    }
}
