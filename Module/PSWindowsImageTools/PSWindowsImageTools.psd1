@{
    # Script module or binary module file associated with this manifest.
    RootModule = 'bin\PSWindowsImageTools.dll'

    # Version number of this module.
    ModuleVersion = '2025.06.10.1755'

    # Supported PSEditions
    CompatiblePSEditions = @('Desktop', 'Core')

    # ID used to uniquely identify this module
    GUID = 'a1b2c3d4-e5f6-7890-abcd-ef1234567890'

    # Author of this module
    Author = 'PSWindowsImageTools'

    # Company or vendor of this module
    CompanyName = 'PSWindowsImageTools'

    # Copyright statement for this module
    Copyright = 'Copyright (c) 2025 PSWindowsImageTools. All rights reserved.'

    # Description of the functionality provided by this module
    Description = 'PowerShell module for Windows image customization, providing tools for working with ISO, WIM, and ESD files, applying customizations through recipes, and managing Windows Update integration.'

    # Minimum version of the PowerShell engine required by this module
    PowerShellVersion = '5.1'

    # Minimum version of Microsoft .NET Framework required by this module
    DotNetFrameworkVersion = '4.8'

    # Minimum version of the common language runtime (CLR) required by this module
    CLRVersion = '4.0'

    # Functions to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no functions to export.
    FunctionsToExport = @()

    # Cmdlets to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no cmdlets to export.
    CmdletsToExport = @(
        'Set-WindowsImageDatabaseConfiguration',
        'New-WindowsImageDatabase',
        'Clear-WindowsImageDatabase',
        'Get-WindowsImageList',
        'Mount-WindowsImageList',
        'Dismount-WindowsImageList',
        'Convert-ESDToWindowsImage',
        'New-WindowsImageBuildRecipe',
        'Export-WindowsImageBuildRecipe',
        'Import-WindowsImageBuildRecipe',
        'New-WindowsImageBuild',
        'Search-WindowsImageUpdateCatalog',
        'Invoke-WindowsImageDatabaseQuery'
    )

    # Variables to export from this module
    VariablesToExport = @()

    # Aliases to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no aliases to export.
    AliasesToExport = @()

    # Private data to pass to the module specified in RootModule/ModuleToProcess. This may also contain a PSData hashtable with additional module metadata used by PowerShell.
    PrivateData = @{
        PSData = @{
            # Tags applied to this module. These help with module discovery in online galleries.
            Tags = @('Windows', 'Image', 'WIM', 'ESD', 'ISO', 'DISM', 'Customization', 'Updates')

            # A URL to the license for this module.
            LicenseUri = 'https://www.gnu.org/licenses/gpl-3.0.html'

            # A URL to the main website for this project.
            ProjectUri = 'https://github.com/PSWindowsImageTools/PSWindowsImageTools'

            # ReleaseNotes of this module
            ReleaseNotes = 'Initial release of PSWindowsImageTools module with comprehensive Windows image customization capabilities.'
        }
    }
}
