# Mail2EInvoice
The application converts EML files and their attachments for further processing into PDF documents. The "eInvoice Converter" generates the necessary representations for electronic invoices.

# Configuration
	- EInvoiceConverterURL: https://hostname
	- EInvoiceConverterApiKey: API-Key. Is encrypted once the application has started.
	- ConfigurationFolders: List of configurations for the directories to be monitored.
		- SourceDirectory: Directory that is monitored for EML files.
		- TargetDirectory: Directory where the documents are stored.
		- BackupDirectory: Directory where the EML files are stored once they have been successfully processed. They are deleted after 10 days.
		- AttachEMail: Generate a PDF from an EML file, including metadata, and append the extracted email attachments to the end of the PDF document.
		- SupportXml: Support for processing XML files, which are interpreted as e-invoices and used to generate PDFs.
		- UsePdfForXml: If a PDF and an XML file with the same name are attached to an email, the PDF document is used for the e-invoice. Otherwise, both attachments are processed separately.
		- CreateJpl: Create a JPL file using metadata of email.
		- ProcessEmlWithoutAttachment: Process EML files without attachments as well.

# Used third components
	- MimeKit
	- PDFsharp
	- PuppeteerSharp
	- Serilog