Write-Host ""
Write-Host "Loading azd .env file from current environment"
Write-Host ""

$output = azd env get-values
#Write-Host $output

foreach ($line in $output) {
    $name, $value = $line.Split("=")
    $value = $value -replace '^\"|\"$'
    [Environment]::SetEnvironmentVariable($name, $value)
}

Write-Host "Environment variables set."
Write-Host ""

while ($true){

    Write-Host "Cleaning up existing data..... "
    Write-Host ""

    Remove-Item -Path .\data\*.pdf 
    Remove-Item -Path .\data\partner\*.pdf 
    Write-Host "-----------------------------------------------------------"
    Write-Host "Wait the training request and download uploaded documents from Blob storage."
    Write-Host "Waiting..... "
    .\scripts\storage-blobs-dotnet-quickstart.exe .\data\partner\
    
    Write-Host "Started....."
    Write-Host "Download completed.."
    Write-Host ""

    Get-Location | Select-Object -ExpandProperty Path
    dotnet run --project "app/prepdocs/PrepareDocs/PrepareDocs.csproj" -- `
        './data/*.pdf' `
        --removeall `
        --verbose `
        --storageendpoint $env:AZURE_STORAGE_BLOB_ENDPOINT `
        --container $env:AZURE_STORAGE_CONTAINER `
        --searchendpoint $env:AZURE_SEARCH_SERVICE_ENDPOINT `
        --searchindex $env:AZURE_SEARCH_INDEX `
        --formrecognizerendpoint $env:AZURE_FORMRECOGNIZER_SERVICE_ENDPOINT `
        --tenantid $env:AZURE_TENANT_ID `
        --verbose

       
    azd env set AZD_PREPDOCS_RAN "false"

    Write-Host "Copy default documents."
    Write-Host ""
    Copy-Item -Path .\data\default\*.pdf -Destination .\data\
    Copy-Item -Path .\data\partner\*.pdf -Destination .\data\

    Write-Host ""
    Write-Host ""

    if ([string]::IsNullOrEmpty($env:AZD_PREPDOCS_RAN) -or $env:AZD_PREPDOCS_RAN -eq "false") {
        Write-Host 'Running "PrepareDocs.dll"'

        Get-Location | Select-Object -ExpandProperty Path

        dotnet run --project "app/prepdocs/PrepareDocs/PrepareDocs.csproj" -- `
            './data/*.pdf' `
            --storageendpoint $env:AZURE_STORAGE_BLOB_ENDPOINT `
            --container $env:AZURE_STORAGE_CONTAINER `
            --searchendpoint $env:AZURE_SEARCH_SERVICE_ENDPOINT `
            --searchindex $env:AZURE_SEARCH_INDEX `
            --formrecognizerendpoint $env:AZURE_FORMRECOGNIZER_SERVICE_ENDPOINT `
            --tenantid $env:AZURE_TENANT_ID `
            -v

        azd env set AZD_PREPDOCS_RAN "true"
    } else {
        Write-Host "AZD_PREPDOCS_RAN is set to true. Skipping the run."
    }
} #end of while 
