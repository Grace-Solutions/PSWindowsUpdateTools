using System;
using System.IO;
using System.Management.Automation;
using Newtonsoft.Json;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Exports a Windows image build recipe object to JSON file
    /// </summary>
    [Cmdlet(VerbsData.Export, "WindowsImageBuildRecipe")]
    [OutputType(typeof(FileInfo))]
    public class ExportWindowsImageBuildRecipeCmdlet : PSCmdlet
    {
        /// <summary>
        /// Recipe object to export
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        [ValidateNotNull]
        public BuildRecipe Recipe { get; set; } = null!;

        /// <summary>
        /// Output path for the JSON file
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        [ValidateNotNullOrEmpty]
        public string OutputPath { get; set; } = null!;

        /// <summary>
        /// Overwrite existing file if it exists
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Use indented JSON formatting for readability
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter Indent { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            var startTime = DateTime.UtcNow;

            try
            {
                LoggingService.LogOperationStart(this, "ExportBuildRecipe", $"Exporting recipe '{Recipe.Metadata.Name}' to: {OutputPath}");

                // Validate output path
                var outputFile = new FileInfo(OutputPath);
                
                // Check if file exists and Force is not specified
                if (outputFile.Exists && !Force.IsPresent)
                {
                    var errorMessage = $"File already exists: {OutputPath}. Use -Force to overwrite.";
                    LoggingService.WriteError(this, errorMessage);
                    ThrowTerminatingError(new ErrorRecord(
                        new InvalidOperationException(errorMessage),
                        "FileExists",
                        ErrorCategory.ResourceExists,
                        OutputPath));
                    return;
                }

                // Create directory if it doesn't exist
                if (outputFile.Directory != null && !outputFile.Directory.Exists)
                {
                    outputFile.Directory.Create();
                    LoggingService.WriteVerbose(this, $"Created directory: {outputFile.Directory.FullName}");
                }

                // Update modified timestamp
                Recipe.Metadata.ModifiedUtc = DateTime.UtcNow;

                // Configure JSON serialization
                var jsonSettings = new JsonSerializerSettings
                {
                    Formatting = Indent.IsPresent ? Formatting.Indented : Formatting.None,
                    NullValueHandling = NullValueHandling.Ignore,
                    DefaultValueHandling = DefaultValueHandling.Ignore
                };

                // Serialize recipe to JSON
                var jsonContent = JsonConvert.SerializeObject(Recipe, jsonSettings);
                
                LoggingService.WriteVerbose(this, $"Serialized recipe to JSON ({jsonContent.Length} characters)");

                // Write to file
                File.WriteAllText(OutputPath, jsonContent);
                
                LoggingService.WriteVerbose(this, $"Recipe exported successfully to: {OutputPath}");

                // Verify file was created
                outputFile.Refresh();
                if (!outputFile.Exists)
                {
                    var errorMessage = "Export appeared to succeed but file was not found";
                    LoggingService.WriteError(this, errorMessage);
                    ThrowTerminatingError(new ErrorRecord(
                        new InvalidOperationException(errorMessage),
                        "ExportVerificationFailed",
                        ErrorCategory.NotSpecified,
                        OutputPath));
                    return;
                }

                // Output file information
                WriteObject(outputFile);

                var duration = DateTime.UtcNow - startTime;
                LoggingService.LogOperationComplete(this, "ExportBuildRecipe", duration, 
                    $"Recipe '{Recipe.Metadata.Name}' exported to {OutputPath} ({outputFile.Length} bytes)");
            }
            catch (Exception ex)
            {
                LoggingService.LogOperationFailure(this, "ExportBuildRecipe", ex);
                ThrowTerminatingError(new ErrorRecord(
                    ex,
                    "ExportBuildRecipeFailed",
                    ErrorCategory.NotSpecified,
                    Recipe));
            }
        }
    }
}
