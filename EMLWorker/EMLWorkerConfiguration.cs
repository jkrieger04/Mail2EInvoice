using System.Text.Json;

namespace EMLWorker
{
    public static class EMLWorkerConfigurationHelper
    {
        public static EMLWorkerConfiguration Load()
        {
            var configuration = JsonSerializer.Deserialize<EMLWorkerConfiguration>(
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Configuration.json")));

            configuration.EInvoiceConverterApiKey = CryptHelper.Decrypt(configuration.EInvoiceConverterApiKey, out bool needEncryption);

            if (needEncryption)
            {
                Save(configuration);
            }

            return configuration;
        }

        public static void Save(EMLWorkerConfiguration configuration)
        {
            configuration.EInvoiceConverterApiKey = CryptHelper.Encrypt(configuration.EInvoiceConverterApiKey);

            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "Configuration.json"), 
                JsonSerializer.Serialize(configuration, new JsonSerializerOptions() { WriteIndented = true }));

            configuration.EInvoiceConverterApiKey = CryptHelper.Decrypt(configuration.EInvoiceConverterApiKey, out bool needEncryption);
        }
    }

    public class EMLWorkerConfiguration
    {
        public List<EMLWorkerConfigurationFolder> ConfigurationFolders { get; set; } = new List<EMLWorkerConfigurationFolder>();
        public string EInvoiceConverterURL { get; set; } = "";
        public string EInvoiceConverterApiKey { get; set; } = "";
    }

    public class EMLWorkerConfigurationFolder
    {
        public string SourceDirectory { get; set; }
        public string TargetDirectory { get; set; }
        public string BackupDirectory { get; set; }
        public string ErrorDirectory { get; set; }
        public bool AttachEMail { get; set; }
        public bool UsePdfForXml { get; set; }
        public bool CreateJpl { get; set; }
        public bool ProcessEmlWithoutAttachment { get; set; }
    }
}
