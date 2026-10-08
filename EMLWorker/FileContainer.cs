namespace EMLWorker
{
    public class FileContainer
    {
        public string FileName { get; set; }
        public byte[] OriginalContent { get; set; }
        public FileType FileType { get; set; }

        public byte[] PdfContent { get; set; }
        public byte[] CmlContent { get; set; }

        public FileContainer() { }

        public FileContainer(string fileName, byte[] content)
        {
            FileName = fileName;
            OriginalContent = content;

            string extension = Path.GetExtension(fileName).ToLowerInvariant();
            FileType = extension switch
            {
                ".pdf" => FileType.PDF,
                ".xml" => FileType.XML,
                ".eml" => FileType.EML,
                _ => FileType.Unknown
            };
        }
    }

    public enum FileType
    {
        Unknown,
        PDF,
        XML,
        EML
    }
}
