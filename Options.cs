using CommandLine;

namespace StorageAccountSync
{
    public class Options
    {
        [Option('c', "client-id", Required = true, HelpText = "The service principal client id to authenticate to KeyVault")]
        public string ClientId { get; set; }

        [Option('t', "thumbprint", Required = true, HelpText = "The certificate thumbprint used to authenticate with cleint-id")]
        public string Thumbprint { get; set; }

        [Option('k', "key-vault-url", Required = true, HelpText = "The URL to the KeyVault instance")]
        public string KeyVaultUrl { get; set; }

        [Option('s', "secret-name", Required = true, HelpText = "The secret name in KeyVault containing the SAS connection string")]
        public string SecretName { get; set; }

        [Option('a', "azcopy-options", Required = false, HelpText = "Extra options to pass to azcopy")]
        public string AzCopyOptions { get; set; }


        [Option('v', "verbose", Default = false, HelpText = "Prints verbpose messages to standard output.")]
        public bool Verbose { get; set; }

        [Option('w', "what-if", Default = false, HelpText = "Skips running azcopy, but will display the commands it would execute")]
        public bool WhatIf { get; set; }
    }
}
