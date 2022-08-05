// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace Microsoft.AspNetCore.Certificates.Generation;

internal sealed class MacOSCertificateManager : CertificateManager
{
    // User keychain. Guard with quotes when using in command lines since users may have set
    // their user profile (HOME) directory to a non-standard path that includes whitespace.
    private static readonly string MacOSUserKeychain = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/Library/Keychains/login.keychain-db";

    // System keychain. We no longer store certificates or create trust rules in the system
    // keychain, but check for their presence here so that we can clean up state left behind
    // by pre-.NET 7 versions of this tool.
    private const string MacOSSystemKeychain = "/Library/Keychains/System.keychain";

    // Well-known location on disk where dev-certs are stored.
    private static readonly string MacOSUserHttpsCertificateLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aspnet", "dev-certs", "https");

    // Verify the certificate {0} for the SSL and X.509 Basic Policy.
    private const string MacOSVerifyCertificateCommandLine = "security";
    private const string MacOSVerifyCertificateCommandLineArgumentsFormat = $"verify-cert -c {{0}} -p basic -p ssl";

    // Delete a certificate with the specified SHA-256 (or SHA-1) hash {0} from keychain {1}.
    private const string MacOSDeleteCertificateCommandLine = "sudo";
    private const string MacOSDeleteCertificateCommandLineArgumentsFormat = "security delete-certificate -Z {0} \"{1}\"";

    // Add a certificate to the per-user trust settings in the user keychain. The trust policy
    // for the certificate will be set to be always trusted for SSL and X.509 Basic Policy.
    // Note: This operation will require user authentication.
    private const string MacOSTrustCertificateCommandLine = "security";
    private static readonly string MacOSTrustCertificateCommandLineArguments = $"add-trusted-cert -p basic -p ssl -k \"{MacOSUserKeychain}\" ";

    // Import a pkcs12 certificate into the user keychain using the unwrapping passphrase {1}, and
    // allow any application to access the imported key without warning.
    private const string MacOSAddCertificateToKeyChainCommandLine = "security";
    private static readonly string MacOSAddCertificateToKeyChainCommandLineArgumentsFormat = "import {0} -k \"" + MacOSUserKeychain + "\" -t cert -f pkcs12 -P {1} -A";

    // Remove a certificate from the admin trust settings. We no longer add certificates to the
    // admin trust settings, but need this for cleaning up certs generated by pre-.NET 7 versions
    // of this tool that used to create trust settings in the system keychain.
    // Note: This operation will require user authentication.
    private const string MacOSUntrustLegacyCertificateCommandLine = "sudo";
    private const string MacOSUntrustLegacyCertificateCommandLineArguments = "security remove-trusted-cert -d {0}";

    // Find all matching certificates on the keychain {1} that have the name {0} and print
    // print their SHA-256 and SHA-1 hashes.
    private const string MacOSFindCertificateOnKeychainCommandLine = "security";
    private const string MacOSFindCertificateOnKeychainCommandLineArgumentsFormat = "find-certificate -c {0} -a -Z -p \"{1}\"";

    // Format used by the tool when printing SHA-1 hashes.
    private const string MacOSFindCertificateOutputRegex = "SHA-1 hash: ([0-9A-Z]+)";

    public const string InvalidCertificateState =
        "The ASP.NET Core developer certificate is in an invalid state. " +
        "To fix this issue, run 'dotnet dev-certs https --clean' and 'dotnet dev-certs https' " +
        "to remove all existing ASP.NET Core development certificates " +
        "and create a new untrusted developer certificate. " +
        "On macOS or Windows, use 'dotnet dev-certs https --trust' to trust the new certificate.";

    public const string KeyNotAccessibleWithoutUserInteraction =
        "The application is trying to access the ASP.NET Core developer certificate key. " +
        "A prompt might appear to ask for permission to access the key. " +
        "When that happens, select 'Always Allow' to grant 'dotnet' access to the certificate key in the future.";

    public MacOSCertificateManager()
    {
    }

    internal MacOSCertificateManager(string subject, int version)
        : base(subject, version)
    {
    }

    protected override void TrustCertificateCore(X509Certificate2 publicCertificate)
    {
        if (IsTrusted(publicCertificate))
        {
            Log.MacOSCertificateAlreadyTrusted();
            return;
        }

        var tmpFile = Path.GetTempFileName();
        try
        {
            ExportCertificate(publicCertificate, tmpFile, includePrivateKey: false, password: null, CertificateKeyExportFormat.Pfx);
            if (Log.IsEnabled())
            {
                Log.MacOSTrustCommandStart($"{MacOSTrustCertificateCommandLine} {MacOSTrustCertificateCommandLineArguments}{tmpFile}");
            }
            using (var process = Process.Start(MacOSTrustCertificateCommandLine, MacOSTrustCertificateCommandLineArguments + tmpFile))
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    Log.MacOSTrustCommandError(process.ExitCode);
                    throw new InvalidOperationException("There was an error trusting the certificate.");
                }
            }
            Log.MacOSTrustCommandEnd();
        }
        finally
        {
            try
            {
                File.Delete(tmpFile);
            }
            catch
            {
                // We don't care if we can't delete the temp file.
            }
        }
    }

    internal override CheckCertificateStateResult CheckCertificateState(X509Certificate2 candidate, bool interactive)
    {
        return File.Exists(GetCertificateFilePath(candidate)) ?
            new CheckCertificateStateResult(true, null) :
            new CheckCertificateStateResult(false, InvalidCertificateState);
    }

    internal override void CorrectCertificateState(X509Certificate2 candidate)
    {
        try
        {
            // Ensure that the directory exists before writing to the file.
            Directory.CreateDirectory(MacOSUserHttpsCertificateLocation);

            var certificatePath = GetCertificateFilePath(candidate);
            ExportCertificate(candidate, certificatePath, includePrivateKey: true, null, CertificateKeyExportFormat.Pfx);
        }
        catch (Exception ex)
        {
            Log.MacOSAddCertificateToUserProfileDirError(candidate.Thumbprint, ex.Message);
        }
    }

    // Use verify-cert to verify the certificate for the SSL and X.509 Basic Policy.
    public override bool IsTrusted(X509Certificate2 certificate)
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            ExportCertificate(certificate, tmpFile, includePrivateKey: false, password: null, CertificateKeyExportFormat.Pem);

            using var checkTrustProcess = Process.Start(new ProcessStartInfo(
                MacOSVerifyCertificateCommandLine,
                string.Format(CultureInfo.InvariantCulture, MacOSVerifyCertificateCommandLineArgumentsFormat, tmpFile))
            {
                RedirectStandardOutput = true,
                // Do this to avoid showing output to the console when the cert is not trusted. It is trivial to export
                // the cert and replicate the command to see details.
                RedirectStandardError = true,
            });
            checkTrustProcess!.WaitForExit();
            return checkTrustProcess.ExitCode == 0;
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    protected override void RemoveCertificateFromTrustedRoots(X509Certificate2 certificate)
    {
        if (IsCertOnKeychain(MacOSSystemKeychain, certificate))
        {
            // Pre-.NET 7 versions of this tool used to store certs and trust settings on the
            // system keychain. Check if that's the case for this cert, and if so, remove the
            // trust rule and the cert from the system keychain.
            try
            {
                RemoveAdminTrustRule(certificate);
                RemoveCertificateFromKeychain(MacOSSystemKeychain, certificate);
            }
            catch
            {
            }
        }

        RemoveCertificateFromUserStoreCore(certificate);
    }

    // Remove the certificate from the admin trust settings.
    private static void RemoveAdminTrustRule(X509Certificate2 certificate)
    {
        Log.MacOSRemoveCertificateTrustRuleStart(GetDescription(certificate));
        var certificatePath = Path.GetTempFileName();
        try
        {
            var certBytes = certificate.Export(X509ContentType.Cert);
            File.WriteAllBytes(certificatePath, certBytes);
            var processInfo = new ProcessStartInfo(
                MacOSUntrustLegacyCertificateCommandLine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    MacOSUntrustLegacyCertificateCommandLineArguments,
                    certificatePath
                ));

            using var process = Process.Start(processInfo);
            process!.WaitForExit();

            if (process.ExitCode != 0)
            {
                Log.MacOSRemoveCertificateTrustRuleError(process.ExitCode);
            }

            Log.MacOSRemoveCertificateTrustRuleEnd();
        }
        finally
        {
            try
            {
                File.Delete(certificatePath);
            }
            catch
            {
                // We don't care if we can't delete the temp file.
            }
        }
    }

    private static void RemoveCertificateFromKeychain(string keychain, X509Certificate2 certificate)
    {
        var processInfo = new ProcessStartInfo(
            MacOSDeleteCertificateCommandLine,
            string.Format(
                CultureInfo.InvariantCulture,
                MacOSDeleteCertificateCommandLineArgumentsFormat,
                certificate.Thumbprint.ToUpperInvariant(),
                keychain
            ))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (Log.IsEnabled())
        {
            Log.MacOSRemoveCertificateFromKeyChainStart(keychain, GetDescription(certificate));
        }

        using (var process = Process.Start(processInfo))
        {
            var output = process!.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Log.MacOSRemoveCertificateFromKeyChainError(process.ExitCode);
                throw new InvalidOperationException($@"There was an error removing the certificate with thumbprint '{certificate.Thumbprint}'.

{output}");
            }
        }

        Log.MacOSRemoveCertificateFromKeyChainEnd();
    }

    private static bool IsCertOnKeychain(string keychain, X509Certificate2 certificate)
    {
        TimeSpan MaxRegexTimeout = TimeSpan.FromMinutes(1);
        const string CertificateSubjectRegex = "CN=(.*[^,]+).*";

        var subjectMatch = Regex.Match(certificate.Subject, CertificateSubjectRegex, RegexOptions.Singleline, MaxRegexTimeout);
        if (!subjectMatch.Success)
        {
            throw new InvalidOperationException($"Can't determine the subject for the certificate with subject '{certificate.Subject}'.");
        }

        var subject = subjectMatch.Groups[1].Value;

        // Run the find-certificate command, and look for the cert's hash in the output
        using var findCertificateProcess = Process.Start(new ProcessStartInfo(
            MacOSFindCertificateOnKeychainCommandLine,
            string.Format(CultureInfo.InvariantCulture, MacOSFindCertificateOnKeychainCommandLineArgumentsFormat, subject, keychain))
        {
            RedirectStandardOutput = true
        });

        var output = findCertificateProcess!.StandardOutput.ReadToEnd();
        findCertificateProcess.WaitForExit();

        var matches = Regex.Matches(output, MacOSFindCertificateOutputRegex, RegexOptions.Multiline, MaxRegexTimeout);
        var hashes = matches.OfType<Match>().Select(m => m.Groups[1].Value).ToList();

        return hashes.Any(h => string.Equals(h, certificate.Thumbprint, StringComparison.Ordinal));
    }

    // We don't have a good way of checking on the underlying implementation if it is exportable, so just return true.
    protected override bool IsExportable(X509Certificate2 c) => true;

    protected override X509Certificate2 SaveCertificateCore(X509Certificate2 certificate, StoreName storeName, StoreLocation storeLocation)
    {
        SaveCertificateToUserKeychain(certificate);

        try
        {
            var certBytes = certificate.Export(X509ContentType.Pfx);

            if (Log.IsEnabled())
            {
                Log.MacOSAddCertificateToUserProfileDirStart(MacOSUserKeychain, GetDescription(certificate));
            }

            // Ensure that the directory exists before writing to the file.
            Directory.CreateDirectory(MacOSUserHttpsCertificateLocation);

            File.WriteAllBytes(GetCertificateFilePath(certificate), certBytes);
        }
        catch (Exception ex)
        {
            Log.MacOSAddCertificateToUserProfileDirError(certificate.Thumbprint, ex.Message);
        }

        Log.MacOSAddCertificateToKeyChainEnd();
        Log.MacOSAddCertificateToUserProfileDirEnd();

        return certificate;
    }

    private static void SaveCertificateToUserKeychain(X509Certificate2 certificate)
    {
        var passwordBytes = new byte[48];
        RandomNumberGenerator.Fill(passwordBytes.AsSpan()[0..35]);
        var password = Convert.ToBase64String(passwordBytes, 0, 36);
        var certBytes = certificate.Export(X509ContentType.Pfx, password);
        var certificatePath = Path.GetTempFileName();
        File.WriteAllBytes(certificatePath, certBytes);

        var processInfo = new ProcessStartInfo(
            MacOSAddCertificateToKeyChainCommandLine,
            string.Format(CultureInfo.InvariantCulture, MacOSAddCertificateToKeyChainCommandLineArgumentsFormat, certificatePath, password))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (Log.IsEnabled())
        {
            Log.MacOSAddCertificateToKeyChainStart(MacOSUserKeychain, GetDescription(certificate));
        }

        using (var process = Process.Start(processInfo))
        {
            var output = process!.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Log.MacOSAddCertificateToKeyChainError(process.ExitCode, output);
                throw new InvalidOperationException("Failed to add the certificate to the keychain. Are you running in a non-interactive session perhaps?");
            }
        }

        Log.MacOSAddCertificateToKeyChainEnd();
    }

    private static string GetCertificateFilePath(X509Certificate2 certificate) =>
        Path.Combine(MacOSUserHttpsCertificateLocation, $"aspnetcore-localhost-{certificate.Thumbprint}.pfx");

    protected override IList<X509Certificate2> GetCertificatesToRemove(StoreName storeName, StoreLocation storeLocation)
    {
        return ListCertificates(StoreName.My, StoreLocation.CurrentUser, isValid: false);
    }

    protected override void PopulateCertificatesFromStore(X509Store store, List<X509Certificate2> certificates)
    {
        if (store.Name! == StoreName.My.ToString() && store.Location == store.Location && Directory.Exists(MacOSUserHttpsCertificateLocation))
        {
            var certsFromDisk = GetCertsFromDisk();

            var certsFromStore = new List<X509Certificate2>();
            base.PopulateCertificatesFromStore(store, certsFromStore);

            // Certs created by pre-.NET 7.
            var onlyOnKeychain = certsFromStore.Except(certsFromDisk, ThumbprintComparer.Instance);

            // Certs created (or "upgraded") by .NET 7+.
            // .NET 7+ installs the certificate on disk as well as on the user keychain (for backwards
            // compatibility with pre-.NET 7).
            var onDiskAndKeychain = certsFromDisk.Intersect(certsFromStore, ThumbprintComparer.Instance);

            // The only times we can find a certificate on the keychain and a certificate on keychain+disk
            // are when the certificate on disk and keychain has expired and a pre-.NET 7 SDK has been
            // used to create a new certificate, or when a pre-.NET 7 certificate has expired and .NET 7+
            // has been used to create a new certificate. In both cases, the caller filters the invalid
            // certificates out, so only the valid certificate is selected.
            certificates.AddRange(onlyOnKeychain);
            certificates.AddRange(onDiskAndKeychain);
        }
        else
        {
            base.PopulateCertificatesFromStore(store, certificates);
        }
    }

    private sealed class ThumbprintComparer : IEqualityComparer<X509Certificate2>
    {
        public static readonly IEqualityComparer<X509Certificate2> Instance = new ThumbprintComparer();

#pragma warning disable CS8769 // Nullability of reference types in type of parameter doesn't match implemented member (possibly because of nullability attributes).
        bool IEqualityComparer<X509Certificate2>.Equals(X509Certificate2 x, X509Certificate2 y) =>
            EqualityComparer<string>.Default.Equals(x?.Thumbprint, y?.Thumbprint);
#pragma warning restore CS8769 // Nullability of reference types in type of parameter doesn't match implemented member (possibly because of nullability attributes).

        int IEqualityComparer<X509Certificate2>.GetHashCode([DisallowNull] X509Certificate2 obj) =>
            EqualityComparer<string>.Default.GetHashCode(obj.Thumbprint);
    }

    private static ICollection<X509Certificate2> GetCertsFromDisk()
    {
        var certsFromDisk = new List<X509Certificate2>();
        if (!Directory.Exists(MacOSUserHttpsCertificateLocation))
        {
            Log.MacOSDiskStoreDoesNotExist();
        }
        else
        {
            var certificateFiles = Directory.EnumerateFiles(MacOSUserHttpsCertificateLocation, "aspnetcore-localhost-*.pfx");
            foreach (var file in certificateFiles)
            {
                try
                {
                    var certificate = new X509Certificate2(file);
                    certsFromDisk.Add(certificate);
                }
                catch (Exception)
                {
                    Log.MacOSFileIsNotAValidCertificate(file);
                    throw;
                }
            }
        }

        return certsFromDisk;
    }

    protected override void RemoveCertificateFromUserStoreCore(X509Certificate2 certificate)
    {
        try
        {
            var certificatePath = GetCertificateFilePath(certificate);
            if (File.Exists(certificatePath))
            {
                File.Delete(certificatePath);
            }
        }
        catch (Exception ex)
        {
            Log.MacOSRemoveCertificateFromUserProfileDirError(certificate.Thumbprint, ex.Message);
        }

        if (IsCertOnKeychain(MacOSUserKeychain, certificate))
        {
            RemoveCertificateFromKeychain(MacOSUserKeychain, certificate);
        }
    }
}
