using System;
using System.Security.Cryptography.X509Certificates;

namespace StorageAccountSync
{
    public static class CertificateStore
    {
        /// <summary>
        /// Finds 
        /// </summary>
        /// <param name="findType">One of the System.Security.Cryptography.X509Certificates.X509FindType values.</param>
        /// <param name="findValue">The search criteria as an object.</param>
        /// <param name="validOnly">true to allow only valid certificates to be returned from the search; otherwise, false.</param>
        /// <param name="storeName">One of the enumeration values that specifies the name of the X.509 certificate store.</param>
        /// <param name="storeLocation">One of the enumeration values that specifies the location of the X.509 certificate store.</param>
        /// <param name="certificateSelector">Function used to pick the best certificate if multiple certificates match.</param>
        /// <returns></returns>
        public static X509Certificate2 Find(
            X509FindType findType,
            object findValue, 
            bool validOnly = true, 
            StoreName storeName = StoreName.My,
            StoreLocation storeLocation = StoreLocation.CurrentUser, 
            Func<X509Certificate2Collection, X509Certificate2> certificateSelector = null)
        {
            if (findValue == null) throw new ArgumentNullException(nameof(findValue));

            if (certificateSelector == null)
            {
                // use the default selector
                certificateSelector = DefaultCertificateSelector;
            }
            
            using (X509Store store = new X509Store(storeName, storeLocation))
            {
                store.Open(OpenFlags.ReadOnly);

                X509Certificate2Collection certificates = store.Certificates.Find(findType, findValue, validOnly);

                if (certificates.Count == 0)
                {
                    return null;
                }

                X509Certificate2 certificate = certificateSelector(certificates);
                return certificate;
            }
        }

        private static X509Certificate2 DefaultCertificateSelector(X509Certificate2Collection certificates)
        {
            X509Certificate2 certificate = certificates[0];
            for (int i = 1; i < certificates.Count; i++)
            {
                // TODO: prefer certs that have a private key over ones that do not

                X509Certificate2 current = certificates[i];
                
                // the current certificate is valid longer than the previous one and it has a private key
                if (certificate.NotAfter < current.NotAfter && current.HasPrivateKey)
                {
                    certificate = current;
                }
            }

            return certificate;
        }
    }
}
