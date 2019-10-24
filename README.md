# Azure-Storage-Account-Sync

Simple utility to use a SAS key stored in KeyVault to read an Azure Stack storage account using AzCopy. This same functionality could be 
implemented in PowerShell but would require the correct PowerShell modules to be installed.  This program compiles to a single EXE 
with no external dependencies.

## Building

From a command prompt run,

```
dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true
```

## Usage

```
StorageAccountSync.exe ^
  --client-id 00000000-0000-0000-0000-000000000000 ^
  --thumbprint 0000000000000000000000000000000000000000 ^
  --key-vault-url https://backup-vault.vault.<region>.<FQDN> ^
  --secret-name StorageAccountSasConnectionString ^
  --verbose
```

## Setup

1. Create X509 self-signed or other certificate for key exchange. Make a note of the certificate thumbprint.
1. Create a Azure Active Directory Registered App and upload the public key created from the step above. Make a note off the Application Id/Client Id.
1. Create a Key Vault and grant access to the registered app created above via Access Policies. At minimum the app requires Get Secret permission. Make a note of the DNS name of the key vault
1. Create a shared access signature (SAS) on the storage account you wish to synchronize. See below for the minimum requirements.
1. Upload the generated **Connection String** (should start with 'BlobEndpoint=https://') to a secret in your Key Vault

### Create a self-signed certificate

To authenticate to KeyVault you require a service principal with a certificate.  The PowerShell commands below will create a certificate
and export the public key that can be uploaded to the Azure Portal.

```powershell
# 
# create a X509 certificate for to authenticate with, this example creates a certificate valid for 5 years
# 
$dnsName = "Phil Bolduc - SQL Provider Database Backups"
$notAfter = (Get-Date).AddYears(5)
$certificate = New-SelfSignedCertificate -KeyExportPolicy Exportable -CertStoreLocation cert:\CurrentUser\My -DnsName $dnsName -NotAfter $notAfter -KeySpec KeyExchange

#
# export the certificate public key to the 'My Documents' folder for upload to registered app
# 
$MyDocuments = [Environment]::GetFolderPath("MyDocuments")
$cerFile = $certificate.Thumbprint + ".cer"
$filePath = Join-Path -Path $MyDocuments -ChildPath $cerFile

Export-Certificate -Cert $rootCert -FilePath $filePath | Out-null
Write-Host "Certificate saved to $filePath"
```

### Create a Registered App

Create a registered application in Azure Active Directory and upload the public key (cer) file to the 'Certificates & secrets'

Go To:

 https://portal.azure.com/ -> Azure Active Directory -> App registrations -> New Registration -> Certificates & secrets -> Upload certificate


### Grant Access to your secrets in Key Valult

### Create a Shared Access Signature (SAS)

#### Minimum Required SAS settings
```
Allowed Services       : Blob
Allowed Resource Types : Service, Container, Object
Allowed Permissions    : Read, List
```

### Upload generated **Connection string** to Key Vault

## WindowsAzure.Storage

The Nuget package WindowsAzure.Storage is at version 6.2.2-preview.  The 6.2 version is the last version that supports Azure Stack API level.
Newer versions of this page use unsupported API versions.

## Proxy Servers
If you have to use a proxy server, you can use these variables to control the use of the proxy server

| Envrionment Variable | Description |
| -------------------- | ----------- |
| HTTPS_PROXY          | the url of your proxy server for HTTPS connection |
| NO_PROXY             | accepts a comma-separated list of hosts, IP addresses, or IP ranges |

## AzCopy 10.1 configuration and limits for Azure Stack

https://docs.microsoft.com/en-us/azure-stack/user/azure-stack-storage-transfer?view=azs-1908#azcopy

set AZCOPY_DEFAULT_SERVICE_API_VERSION=2017-11-09

https://docs.microsoft.com/en-us/azure/storage/common/storage-use-azcopy-v10#use-azcopy-in-a-script
