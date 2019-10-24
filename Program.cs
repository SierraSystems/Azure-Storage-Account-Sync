using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Azure.KeyVault;
using Microsoft.WindowsAzure.Storage;

namespace StorageAccountSync
{

    class Program
    {
        // app.exe 
        //          --client-id <client-id>
        //          --thumbprint <certificate-thumbprint> 
        //          --key-vault-url https://my-key-vault.vault.<region>.<FQDN> 
        //          --secret-name <sas-key-secret-name> 
        //          --azcopy-options <options>
        //          --verbose
        //          --what-if

        static int Main(string[] args)
        {
            try
            {
                var rc = Parser.Default.ParseArguments<Options>(args)
                    .WithParsed<Options>(opts => Run(opts));

                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return int.MinValue;
            }
        }

        static int Run(Options options) => RunAsync(options).ConfigureAwait(false).GetAwaiter().GetResult();

        static async Task<int> RunAsync(Options options)
        {
            if (options.WhatIf)
            {
                Console.WriteLine($"Info: What-If mode enabled, azcopy will not be run");
            }

            // ------------------------------------------------------------------
            // authenticate to key vault with a service principal and certificate
            VerboseMessage(options, $"Loading certificate with thumbprint {options.Thumbprint}");
            var certificate = CertificateStore.Find(X509FindType.FindByThumbprint, options.Thumbprint, false);

            if (certificate == null)
            {
                ErrorMessage($"Could not find certificate with thumbprint {options.Thumbprint} in the current user's personal store");
                return -1;
            }

            KeyVaultClient client = new KeyVaultClient((authority, resource, scope) =>
            {
                return AccessTokenAccessor.Get(authority, resource, scope, options.ClientId, certificate);
            });

            if (!Uri.TryCreate(options.KeyVaultUrl, UriKind.Absolute, out var keyVaultUri))
            {
                ErrorMessage($"key-vault-url is not a valid url: {options.KeyVaultUrl}");
                return -2;
            }

            // get the SAS token secret from Key Vault
            string keyVault = keyVaultUri.ToString().TrimEnd('/');
            VerboseMessage(options, $"Getting secret {options.SecretName} from KeyVault {keyVault}");
            var secret = await client.GetSecretAsync(keyVault, options.SecretName);

            // Required SAS settings
            // ------------------------------------------------------
            // Allowed Services       : Blob
            // Allowed Resource Types : Service, Container, Object
            // Allowed Permissions    : Read, List

            // secret must the store connection string sas, ie starts with BlobEndpoint=https://
            VerboseMessage(options, $"Parsing storage account connection string");
            if (!CloudStorageAccount.TryParse(secret.Value, out CloudStorageAccount storageAccount))
            {
                ErrorMessage($"KeyVault secret '{options.SecretName}' is not a valid storage account connection string.");
                return -3;
            }

            if (string.IsNullOrEmpty(storageAccount.Credentials.SASToken))
            {
                Console.Error.WriteLine($"KeyVault secret '{options.SecretName}' does not contain a valid a SAS token.");
                return -4;
            }

            VerboseMessage(options, $"Retrieving storage account container names");
            List<string> containers = await storageAccount.ListContainersAsync();

            if (options.Verbose)
            {
                string containerNames = string.Join(", ", containers.ToArray());
                VerboseMessage(options, $"Found containers: {containerNames}");
            }

            // -----------------------------------------------------------------
            // sync each container
            foreach (var source in GetSources(storageAccount, containers))
            {
                SynchronizeContainer(options, source);
            }

            return 0;
        }

        private static int SynchronizeContainer(Options options, Tuple<string, Uri> source)
        {
            Directory.CreateDirectory(source.Item1);

            VerboseMessage(options, $"Processing container {source.Item1}");
            ProcessStartInfo startInfo = CreateAzCopyStartInfo(source, options.AzCopyOptions);

            // should we be hidding the SAS key in output?
            VerboseMessage(options, $"Running command {startInfo.FileName} {startInfo.Arguments}");

            if (!options.WhatIf)
            {
                using (Process process = new Process())
                {
                    bool haveOutData = false;
                    bool haveErrorData = false;

                    process.StartInfo = startInfo;

                    // if executable not in path or in same directory, then this throws
                    // System.ComponentModel.Win32Exception (2): The system cannot find the file specified
                    try
                    {
                        // redirect stdout
                        process.StartInfo.RedirectStandardOutput = true;
                        process.OutputDataReceived += (sender, args) =>
                        {
                            haveOutData = true;
                            Console.Out.WriteLine(args.Data);
                        };

                        // capture stderr
                        process.StartInfo.RedirectStandardError = true;
                        process.ErrorDataReceived += (sender, args) =>
                        {
                            haveErrorData = true;
                            Console.Error.WriteLine(args.Data);
                        };

                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                    }
                    catch (Win32Exception exception) when (exception.ErrorCode == -2147467259 /*0x80004005*/)
                    {
                        Console.Error.WriteLine("Could not run azcopy. Is it in the current working directory or in the path?");
                        Console.Error.WriteLine(exception);
                        return int.MinValue;
                    }

                    process.WaitForExit();

                    if (haveOutData)
                    {
                        //Console.Out.WriteLine();
                    }

                    if (haveErrorData)
                    {
                        //Console.Error.WriteLine();
                    }

                    if (process.ExitCode != 0)
                    {
                        // TODO: determine if we should retry, azcopy will resume the sync
                        //       as it stores a journal

                        // TODO: do we need to clean up failed job journal info?

                        return process.ExitCode;
                    }
                }
            }

            return 0;
        }

        /// <summary>
        /// Gets a collection of azcopy source url and container name
        /// </summary>
        /// <param name="storageAccount"></param>
        /// <param name="containers"></param>
        /// <returns></returns>
        private static IEnumerable<Tuple<string, Uri>> GetSources(CloudStorageAccount storageAccount, List<string> containers)
        {
            // -----------------------------------------------------------------
            // Get a list of azcopy source url and container name
            UriBuilder builder = new UriBuilder(storageAccount.BlobEndpoint);
            builder.Query = storageAccount.Credentials.SASToken;

            foreach (var container in containers)
            {
                builder.Path = container;
                yield return Tuple.Create(container, builder.Uri);
            }
        }

        private static void ErrorMessage(string message)
        {
            Console.Error.WriteLine($"Error: {message}");
        }

        private static void VerboseMessage(Options options, string message)
        {
            if (options.Verbose)
            {
                // TODO: add date and time
                Console.WriteLine($"Verbose: {message}");
            }
        }

        private static ProcessStartInfo CreateAzCopyStartInfo(Tuple<string, Uri> source, string azcopyOptions)      
        {
            ProcessStartInfo start = new ProcessStartInfo();
            //start.WorkingDirectory = ""; // caller should start in the correct working directory
            start.FileName = "azcopy.exe";
            start.CreateNoWindow = true;
            start.UseShellExecute = false;

            // sync source destination
            start.Arguments = string.IsNullOrEmpty(azcopyOptions)
                ? $"sync {source.Item2} .\\{source.Item1}"
                : $"sync {source.Item2} .\\{source.Item1} {azcopyOptions}";

            return start;
        }
    }
}
