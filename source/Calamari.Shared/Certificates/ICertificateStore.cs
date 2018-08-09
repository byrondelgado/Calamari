﻿using System.Security.Cryptography.X509Certificates;

namespace Calamari.Shared.Certificates
{
    public interface ICertificateStore
    {
        X509Certificate2 GetOrAdd(string thumbprint, string bytes);
        X509Certificate2 GetOrAdd(string thumbprint, string bytes, StoreName storeName);
    }
}