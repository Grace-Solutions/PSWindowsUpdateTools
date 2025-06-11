Import-Module '.\Module\PSWindowsImageTools\PSWindowsImageTools.psd1' -Force

Write-Host '=== Testing Recipe Workflow: New -> Modify -> Export -> Import ===' -ForegroundColor Green

# Test 1: Create a new recipe object
Write-Host '=== Test 1: New-WindowsImageBuildRecipe ===' -ForegroundColor Yellow
$recipe = New-WindowsImageBuildRecipe -Name "My Custom Recipe" -Description "Test recipe for workflow" -Author "Test User" -IncludeDefaults -Verbose

Write-Host "Recipe created: $($recipe.Metadata.Name)" -ForegroundColor White
Write-Host "Enabled sections: $(($recipe.PSObject.Properties | Where-Object { $_.Value.Enabled -eq $true }).Count)" -ForegroundColor White

# Test 2: Modify the recipe object
Write-Host '=== Test 2: Modifying Recipe Object ===' -ForegroundColor Yellow
$recipe.Metadata.Description = "Modified description"
$recipe.CopyFiles.Enabled = $true
# Create a proper CopyFileItem object
$copyItem = New-Object PSWindowsImageTools.Models.CopyFileItem
$copyItem.Source = "C:\Source\file.txt"
$copyItem.Destination = "C:\Windows\Temp\file.txt"
$copyItem.Overwrite = $true
$recipe.CopyFiles.Items.Add($copyItem)

Write-Host "Modified recipe description and added copy file operation" -ForegroundColor White

# Test 3: Export the recipe to JSON
Write-Host '=== Test 3: Export-WindowsImageBuildRecipe ===' -ForegroundColor Yellow
$exportPath = "C:\Temp\test-recipe.json"
$exportedFile = Export-WindowsImageBuildRecipe -Recipe $recipe -OutputPath $exportPath -Indent -Force -Verbose

Write-Host "Recipe exported to: $($exportedFile.FullName)" -ForegroundColor White
Write-Host "File size: $($exportedFile.Length) bytes" -ForegroundColor White

# Test 4: Import the recipe back
Write-Host '=== Test 4: Import-WindowsImageBuildRecipe ===' -ForegroundColor Yellow
$importedRecipe = Import-WindowsImageBuildRecipe -Path $exportPath -Validate -Verbose

Write-Host "Recipe imported: $($importedRecipe.Metadata.Name)" -ForegroundColor White
Write-Host "Description: $($importedRecipe.Metadata.Description)" -ForegroundColor White
Write-Host "Copy files enabled: $($importedRecipe.CopyFiles.Enabled)" -ForegroundColor White
Write-Host "Copy files count: $($importedRecipe.CopyFiles.Items.Count)" -ForegroundColor White

# Test 5: Show the JSON content
Write-Host '=== Test 5: JSON Content Preview ===' -ForegroundColor Yellow
$jsonContent = Get-Content $exportPath -Raw
Write-Host "JSON file contains $($jsonContent.Length) characters" -ForegroundColor White
Write-Host "First 200 characters:" -ForegroundColor Cyan
Write-Host $jsonContent.Substring(0, [Math]::Min(200, $jsonContent.Length)) -ForegroundColor Gray

Write-Host '=== Recipe Workflow Test Complete ===' -ForegroundColor Green
Write-Host 'Workflow: New-WindowsImageBuildRecipe -> Modify Object -> Export-WindowsImageBuildRecipe -> Import-WindowsImageBuildRecipe' -ForegroundColor White
