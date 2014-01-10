// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
// <OWNER>[....]</OWNER>
// 
 
//
// RSACryptoServiceProvider.cs 
//
// CSP-based implementation of RSA
//
 
namespace System.Security.Cryptography {
    using System; 
    using System.Globalization; 
    using System.IO;
    using System.Security; 
    using System.Runtime.InteropServices;
    using System.Runtime.CompilerServices;
    using System.Runtime.Versioning;
    using System.Security.Cryptography.X509Certificates; 
    using System.Security.Permissions;
    using System.Diagnostics.Contracts; 
 
#if SILVERLIGHT
    using BCLDebug=System.Diagnostics.Debug; 
#endif // SILVERLIGHT

    // Object layout of the RSAParameters structure
    internal class RSACspObject { 
        internal byte[] Exponent;
        internal byte[] Modulus; 
        internal byte[] P; 
        internal byte[] Q;
        internal byte[] DP; 
        internal byte[] DQ;
        internal byte[] InverseQ;
        internal byte[] D;
    } 

#if !SILVERLIGHT 
    [System.Runtime.InteropServices.ComVisible(true)] 
#endif // !SILVERLIGHT
    public sealed class RSACryptoServiceProvider : RSA 
#if !SILVERLIGHT
        , ICspAsymmetricAlgorithm
#endif // !SILVERLIGHT
    { 
        private int _dwKeySize;
        private CspParameters  _parameters; 
        private bool _randomKeyContainer; 
#if !SILVERLIGHT
        [System.Security.SecurityCritical /*auto-generated*/] 
        private SafeProvHandle _safeProvHandle;
        [System.Security.SecurityCritical /*auto-generated*/]
        private SafeKeyHandle _safeKeyHandle;
#else // !SILVERLIGHT 
        private SafeCspHandle _safeProvHandle;
        private SafeCspKeyHandle _safeKeyHandle; 
#endif // !SILVERLIGHT 

        private static CspProviderFlags s_UseMachineKeyStore = 0; 

        //
        // QCalls
        // 

        [System.Security.SecurityCritical]  // auto-generated 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)] 
        [ResourceExposure(ResourceScope.None)]
        [SuppressUnmanagedCodeSecurity] 
        private static extern void DecryptKey(SafeKeyHandle pKeyContext,
                                              [MarshalAs(UnmanagedType.LPArray)] byte[] pbEncryptedKey,
                                              int cbEncryptedKey,
                                              [MarshalAs(UnmanagedType.Bool)] bool fOAEP, 
                                              ObjectHandleOnStack ohRetDecryptedKey);
 
        [System.Security.SecurityCritical]  // auto-generated 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        [ResourceExposure(ResourceScope.None)] 
        [SuppressUnmanagedCodeSecurity]
        private static extern void EncryptKey(SafeKeyHandle pKeyContext,
                                              [MarshalAs(UnmanagedType.LPArray)] byte[] pbKey,
                                              int cbKey, 
                                              [MarshalAs(UnmanagedType.Bool)] bool fOAEP,
                                              ObjectHandleOnStack ohRetEncryptedKey); 
 
        //
        // public constructors 
        //

#if !SILVERLIGHT
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)] 
        public RSACryptoServiceProvider() 
            : this(0, new CspParameters(Utils.DefaultRsaProviderType, null, null, s_UseMachineKeyStore), true) {
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)] 
        public RSACryptoServiceProvider(int dwKeySize)
            : this(dwKeySize, new CspParameters(Utils.DefaultRsaProviderType, null, null, s_UseMachineKeyStore), false) { 
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        public RSACryptoServiceProvider(CspParameters parameters)
            : this(0, parameters, true) {
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public RSACryptoServiceProvider(int dwKeySize, CspParameters parameters) 
            : this(dwKeySize, parameters, false) { 
        }
#else // !SILVERLIGHT 
        public RSACryptoServiceProvider() : this(0, new CspParameters(), true) {
        }
#endif // !SILVERLIGHT
 
        //
        // private methods 
        // 

        [System.Security.SecurityCritical]  // auto-generated 
        private RSACryptoServiceProvider(int dwKeySize, CspParameters parameters, bool useDefaultKeySize) {
#if !SILVERLIGHT
            if (dwKeySize < 0)
                throw new ArgumentOutOfRangeException("dwKeySize", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum")); 
            Contract.EndContractBlock();
#else 
            Contract.Assert(dwKeySize >= 0, "dwKeySize >= 0"); 
#endif // !SILVERLIGHT
 
            _parameters = Utils.SaveCspParameters(CspAlgorithmType.Rsa, parameters, s_UseMachineKeyStore, ref _randomKeyContainer);

                LegalKeySizesValue = new KeySizes[] { new KeySizes(384, 16384, 8) };
            _dwKeySize = useDefaultKeySize ? 1024 : dwKeySize; 

#if !SILVERLIGHT 
            // If this is not a random container we generate, create it eagerly 
            // in the constructor so we can report any errors now.
            if (!_randomKeyContainer || Environment.GetCompatibilityFlag(CompatibilityFlag.EagerlyGenerateRandomAsymmKeys)) 
                GetKeyPair();
#endif // !SILVERLIGHT
        }
 
#if SILVERLIGHT
        [SecurityCritical] 
#endif // SILVERLIGHT 
        [System.Security.SecurityCritical]  // auto-generated
        private void GetKeyPair () { 
            if (_safeKeyHandle == null) {
                lock (this) {
                    if (_safeKeyHandle == null) {
#if !SILVERLIGHT 
                        // We only attempt to generate a random key on desktop runtimes because the CoreCLR
                        // RSA surface area is limited to simply verifying signatures.  Since generating a 
                        // random key to verify signatures will always lead to failure (unless we happend to 
                        // win the lottery and randomly generate the signing key ...), there is no need
                        // to add this functionality to CoreCLR at this point. 
                        Utils.GetKeyPairHelper(CspAlgorithmType.Rsa, _parameters, _randomKeyContainer, _dwKeySize, ref _safeProvHandle, ref _safeKeyHandle);
#endif  // !SILVERLIGHT
                    }
                } 
            }
        } 
 
#if SILVERLIGHT
        [SecuritySafeCritical] 
#endif // SILVERLIGHT
        [System.Security.SecuritySafeCritical] // overrides public transparent member
        protected override void Dispose(bool disposing)
        { 
            base.Dispose(disposing);
 
            if (_safeKeyHandle != null && !_safeKeyHandle.IsClosed) 
                _safeKeyHandle.Dispose();
            if (_safeProvHandle != null && !_safeProvHandle.IsClosed) 
                _safeProvHandle.Dispose();
        }

        // 
        // public properties
        // 
 
#if !SILVERLIGHT
        [System.Runtime.InteropServices.ComVisible(false)] 
        public bool PublicOnly {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get {
                GetKeyPair(); 
                byte[] publicKey = (byte[]) Utils._GetKeyParameter(_safeKeyHandle, Constants.CLR_PUBLICKEYONLY);
                return (publicKey[0] == 1); 
            } 
        }
 
        [System.Runtime.InteropServices.ComVisible(false)]
        public CspKeyContainerInfo CspKeyContainerInfo {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get { 
                GetKeyPair();
                return new CspKeyContainerInfo(_parameters, _randomKeyContainer); 
            } 
        }
#endif // !SILVERLIGHT 

        public override int KeySize {
#if SILVERLIGHT
            [SecuritySafeCritical] 
#endif // SILVERLIGHT
            [System.Security.SecuritySafeCritical]  // auto-generated 
            get { 
                GetKeyPair();
#if !SILVERLIGHT 
                byte[] keySize = (byte[]) Utils._GetKeyParameter(_safeKeyHandle, Constants.CLR_KEYLEN);
                _dwKeySize = (keySize[0] | (keySize[1] << 8) | (keySize[2] << 16) | (keySize[3] << 24));
                return _dwKeySize;
#else // !SILVERLIGHT 
                return CapiNative.GetKeyPropertyInt32(_safeKeyHandle, CapiNative.KeyProperty.KeyLength);
#endif // !SILVERLIGHT 
            } 
        }
 
#if !SILVERLIGHT
        public override string KeyExchangeAlgorithm {
            get {
                if (_parameters.KeyNumber == Constants.AT_KEYEXCHANGE) 
                    return "RSA-PKCS1-KeyEx";
                return null; 
            } 
        }
 
        public override string SignatureAlgorithm {
            get { return "http://www.w3.org/2000/09/xmldsig#rsa-sha1"; }
        }
 
        public static bool UseMachineKeyStore {
            get { return (s_UseMachineKeyStore == CspProviderFlags.UseMachineKeyStore); } 
            set { s_UseMachineKeyStore = (value ? CspProviderFlags.UseMachineKeyStore : 0); } 
        }
 
        public bool PersistKeyInCsp {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get {
                if (_safeProvHandle == null) { 
                    lock (this) {
                        if (_safeProvHandle == null) 
                            _safeProvHandle = Utils.CreateProvHandle(_parameters, _randomKeyContainer); 
                    }
                } 
                return Utils.GetPersistKeyInCsp(_safeProvHandle);
            }
            [System.Security.SecuritySafeCritical]  // auto-generated
            set { 
                bool oldPersistKeyInCsp = this.PersistKeyInCsp;
                if (value == oldPersistKeyInCsp) 
                    return; 

                KeyContainerPermission kp = new KeyContainerPermission(KeyContainerPermissionFlags.NoFlags); 
                if (!value) {
                    KeyContainerPermissionAccessEntry entry = new KeyContainerPermissionAccessEntry(_parameters, KeyContainerPermissionFlags.Delete);
                    kp.AccessEntries.Add(entry);
                } else { 
                    KeyContainerPermissionAccessEntry entry = new KeyContainerPermissionAccessEntry(_parameters, KeyContainerPermissionFlags.Create);
                    kp.AccessEntries.Add(entry); 
                } 
                kp.Demand();
 
                Utils.SetPersistKeyInCsp(_safeProvHandle, value);
            }
        }
 
        //
        // public methods 
        // 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        public override RSAParameters ExportParameters (bool includePrivateParameters) {
            GetKeyPair();
            if (includePrivateParameters) {
                KeyContainerPermission kp = new KeyContainerPermission(KeyContainerPermissionFlags.NoFlags); 
                KeyContainerPermissionAccessEntry entry = new KeyContainerPermissionAccessEntry(_parameters, KeyContainerPermissionFlags.Export);
                kp.AccessEntries.Add(entry); 
                kp.Demand(); 
            }
            RSACspObject rsaCspObject = new RSACspObject(); 
            int blobType = includePrivateParameters ? Constants.PRIVATEKEYBLOB : Constants.PUBLICKEYBLOB;
            // _ExportKey will check for failures and throw an exception
            Utils._ExportKey(_safeKeyHandle, blobType, rsaCspObject);
            return RSAObjectToStruct(rsaCspObject); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [System.Runtime.InteropServices.ComVisible(false)]
        public byte[] ExportCspBlob (bool includePrivateParameters) { 
            GetKeyPair();
            return Utils.ExportCspBlobHelper(includePrivateParameters, _parameters, _safeKeyHandle);
        }
#endif // !SILVERLIGHT 

#if SILVERLIGHT 
        [SecuritySafeCritical] 
#endif // SILVERLIGHT
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public override void ImportParameters(RSAParameters parameters) {
            // Free the current key handle
            if (_safeKeyHandle != null && !_safeKeyHandle.IsClosed) {
                _safeKeyHandle.Dispose(); 
                _safeKeyHandle = null;
            } 
 
#if !SILVERLIGHT
            RSACspObject rsaCspObject = RSAStructToObject(parameters); 
            _safeKeyHandle = SafeKeyHandle.InvalidHandle;

            if (IsPublic(parameters)) {
                // Use our CRYPT_VERIFYCONTEXT handle, CRYPT_EXPORTABLE is not applicable to public only keys, so pass false 
                Utils._ImportKey(Utils.StaticProvHandle, Constants.CALG_RSA_KEYX, (CspProviderFlags) 0, rsaCspObject, ref _safeKeyHandle);
            } else { 
                KeyContainerPermission kp = new KeyContainerPermission(KeyContainerPermissionFlags.NoFlags); 
                KeyContainerPermissionAccessEntry entry = new KeyContainerPermissionAccessEntry(_parameters, KeyContainerPermissionFlags.Import);
                kp.AccessEntries.Add(entry); 
                kp.Demand();
                if (_safeProvHandle == null)
                    _safeProvHandle = Utils.CreateProvHandle(_parameters, _randomKeyContainer);
                // Now, import the key into the CSP; _ImportKey will check for failures. 
                Utils._ImportKey(_safeProvHandle, Constants.CALG_RSA_KEYX, _parameters.Flags, rsaCspObject, ref _safeKeyHandle);
            } 
#else // !SILVERLIGHT 
            if (!IsPublic(parameters)) {
                throw new NotSupportedException(Environment.GetResourceString("Cryptography_OnlyPublicKeyImport")); 
            }
            Contract.EndContractBlock();

            // Map our flags onto the CAPI flags, leaving out the exportable flag since we only support 
            // public key import
            CapiNative.KeyGenerationFlags keyFlags = CapiNative.KeyGenerationFlags.None; 
            if ((_parameters.Flags & CspProviderFlags.UseArchivableKey) == CspProviderFlags.UseArchivableKey) 
                keyFlags |= CapiNative.KeyGenerationFlags.Archivable;
            if ((_parameters.Flags & CspProviderFlags.UseUserProtectedKey) == CspProviderFlags.UseUserProtectedKey) 
                keyFlags |= CapiNative.KeyGenerationFlags.UserProtected;

            if (_safeProvHandle == null) {
                _safeProvHandle = AcquireCsp(_parameters); 
            }
 
            _safeKeyHandle = CapiNative.ImportKey(_safeProvHandle, parameters, keyFlags); 
#endif // !SILVERLIGHT
        } 

#if !SILVERLIGHT
        [System.Security.SecuritySafeCritical]  // auto-generated
        [System.Runtime.InteropServices.ComVisible(false)] 
        public void ImportCspBlob (byte[] keyBlob) {
            Utils.ImportCspBlobHelper(CspAlgorithmType.Rsa, keyBlob, IsPublic(keyBlob), ref _parameters, _randomKeyContainer, ref _safeProvHandle, ref _safeKeyHandle); 
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        public byte[] SignData(Stream inputStream, Object halg) {
            string oid = Utils.ObjToOidValue(halg);
            HashAlgorithm hash = Utils.ObjToHashAlgorithm(halg);
            byte[] hashVal = hash.ComputeHash(inputStream); 
            return SignHash(hashVal, oid);
        } 
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public byte[] SignData(byte[] buffer, Object halg) { 
            string oid = Utils.ObjToOidValue(halg);
            HashAlgorithm hash = Utils.ObjToHashAlgorithm(halg);
            byte[] hashVal = hash.ComputeHash(buffer);
            return SignHash(hashVal, oid); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public byte[] SignData(byte[] buffer, int offset, int count, Object halg) {
            string oid = Utils.ObjToOidValue(halg); 
            HashAlgorithm hash = Utils.ObjToHashAlgorithm(halg);
            byte[] hashVal = hash.ComputeHash(buffer, offset, count);
            return SignHash(hashVal, oid);
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        public bool VerifyData(byte[] buffer, Object halg, byte[] signature) { 
            string oid = Utils.ObjToOidValue(halg);
            HashAlgorithm hash = Utils.ObjToHashAlgorithm(halg); 
            byte[] hashVal = hash.ComputeHash(buffer);
            return VerifyHash(hashVal, oid, signature);
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public byte[] SignHash(byte[] rgbHash, string str) { 
            if (rgbHash == null) 
                throw new ArgumentNullException("rgbHash");
            Contract.EndContractBlock(); 
            if (PublicOnly)
                throw new CryptographicException(Environment.GetResourceString("Cryptography_CSP_NoPrivateKey"));

            int calgHash = X509Utils.OidToAlgId(str); 
            GetKeyPair();
            if (!CspKeyContainerInfo.RandomlyGenerated) { 
                KeyContainerPermission kp = new KeyContainerPermission(KeyContainerPermissionFlags.NoFlags); 
                KeyContainerPermissionAccessEntry entry = new KeyContainerPermissionAccessEntry(_parameters, KeyContainerPermissionFlags.Sign);
                kp.AccessEntries.Add(entry); 
                kp.Demand();
            }
            return Utils.SignValue(_safeKeyHandle, _parameters.KeyNumber, Constants.CALG_RSA_SIGN, calgHash, rgbHash);
        } 
#endif // !SILVERLIGHT
 
#if SILVERLIGHT 
        [SecuritySafeCritical]
#endif // SILVERLIGHT 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public bool VerifyHash(byte[] rgbHash, string str, byte[] rgbSignature) {
            if (rgbHash == null)
                throw new ArgumentNullException("rgbHash"); 
            if (rgbSignature == null)
                throw new ArgumentNullException("rgbSignature"); 
            Contract.EndContractBlock(); 

            GetKeyPair(); 

#if !SILVERLIGHT
            int calgHash = X509Utils.OidToAlgId(str);
            return Utils.VerifySign(_safeKeyHandle, Constants.CALG_RSA_SIGN, calgHash, rgbHash, rgbSignature); 
#else // !SILVERLIGHT
 
            // If we never got a key imported, then the signature verification will always fail; this is the 
            // same effect as if we had generated a random key for the user.
            if (_safeKeyHandle == null) { 
                return false;
            }

            CapiNative.AlgorithmID hashAlgorithm = CapiNative.AlgorithmID.None; 
            if (String.Equals(str, "SHA1", StringComparison.OrdinalIgnoreCase)) {
                hashAlgorithm = CapiNative.AlgorithmID.Sha1; 
            } 
            else if (String.Equals(str, "SHA256", StringComparison.OrdinalIgnoreCase)) {
                hashAlgorithm = CapiNative.AlgorithmID.Sha256; 
            }
            else if (String.Equals(str, "SHA384", StringComparison.OrdinalIgnoreCase)) {
                hashAlgorithm = CapiNative.AlgorithmID.Sha384;
            } 
            else if (String.Equals(str, "SHA512", StringComparison.OrdinalIgnoreCase)) {
                hashAlgorithm = CapiNative.AlgorithmID.Sha512; 
            } 

            if (hashAlgorithm == CapiNative.AlgorithmID.None) { 
                // An ArgumentException would be preferable here, but to match the desktop behavior
                // we throw CryptographicException instead.
                throw new CryptographicException(Environment.GetResourceString("Cryptography_UnknownHashAlgorithm", str));
            } 

            return CapiNative.VerifySignature(_safeProvHandle, 
                                              _safeKeyHandle, 
                                              CapiNative.AlgorithmID.RsaSign,
                                              hashAlgorithm, 
                                              rgbHash,
                                              rgbSignature);
#endif // !SILVERLIGHT
        } 

#if !SILVERLIGHT 
        /// <summary> 
        ///     Encrypt raw data, generally used for encrypting symmetric key material.
        /// </summary> 
        /// <remarks>
        ///     This method can only encrypt (keySize - 88 bits) of data, so should not be used for encrypting
        ///     arbitrary byte arrays. Instead, encrypt a symmetric key with this method, and use the symmetric
        ///     key to encrypt the sensitive data. 
        /// </remarks>
        /// <param name="rgb">raw data to encryt</param> 
        /// <param name="fOAEP">true to use OAEP padding (PKCS #1 v2), false to use PKCS #1 type 2 padding</param> 
        /// <returns>Encrypted key</returns>
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public byte[] Encrypt(byte[] rgb, bool fOAEP) {
            if (rgb == null)
                throw new ArgumentNullException("rgb");
            Contract.EndContractBlock(); 

            GetKeyPair(); 
 
            byte[] encryptedKey = null;
            EncryptKey(_safeKeyHandle, rgb, rgb.Length, fOAEP, JitHelpers.GetObjectHandleOnStack(ref encryptedKey)); 
            return encryptedKey;
        }

        /// <summary> 
        ///     Decrypt raw data, generally used for decrypting symmetric key material
        /// </summary> 
        /// <param name="rgb">encrypted data</param> 
        /// <param name="fOAEP">true to use OAEP padding (PKCS #1 v2), false to use PKCS #1 type 2 padding</param>
        /// <returns>decrypted data</returns> 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public byte [] Decrypt(byte[] rgb, bool fOAEP) {
            if (rgb == null)
                throw new ArgumentNullException("rgb"); 
            Contract.EndContractBlock();
 
            GetKeyPair(); 

            // size check -- must be at most the modulus size 
            if (rgb.Length > (KeySize / 8))
                throw new CryptographicException(Environment.GetResourceString("Cryptography_Padding_DecDataTooBig", KeySize / 8));

            if (!CspKeyContainerInfo.RandomlyGenerated) { 
                KeyContainerPermission kp = new KeyContainerPermission(KeyContainerPermissionFlags.NoFlags);
                KeyContainerPermissionAccessEntry entry = new KeyContainerPermissionAccessEntry(_parameters, KeyContainerPermissionFlags.Decrypt); 
                kp.AccessEntries.Add(entry); 
                kp.Demand();
            } 

            byte[] decryptedKey = null;
            DecryptKey(_safeKeyHandle, rgb, rgb.Length, fOAEP, JitHelpers.GetObjectHandleOnStack(ref decryptedKey));
            return decryptedKey; 
        }
 
        public override byte[] DecryptValue(byte[] rgb) { 
            throw new NotSupportedException(Environment.GetResourceString("NotSupported_Method"));
        } 

        public override byte[] EncryptValue(byte[] rgb) {
            throw new NotSupportedException(Environment.GetResourceString("NotSupported_Method"));
        } 

        // 
        // private static methods 
        //
 
#endif // !SILVERLIGHT

#if SILVERLIGHT
        [SecurityCritical] 
        private static SafeCspHandle AcquireCsp(CspParameters parameters) {
            // Map our flags onto CAPI flags 
            CapiNative.CryptAcquireContextFlags providerFlags = CapiNative.CryptAcquireContextFlags.None; 
            if ((parameters.Flags & CspProviderFlags.UseMachineKeyStore) == CspProviderFlags.UseMachineKeyStore)
                providerFlags |= CapiNative.CryptAcquireContextFlags.MachineKeyset; 
            if ((parameters.Flags & CspProviderFlags.NoPrompt) == CspProviderFlags.NoPrompt)
                providerFlags |= CapiNative.CryptAcquireContextFlags.Silent;
            if ((parameters.Flags & CspProviderFlags.CreateEphemeralKey) == CspProviderFlags.CreateEphemeralKey)
                providerFlags |= CapiNative.CryptAcquireContextFlags.VerifyContext; 

            return CapiNative.AcquireCsp(parameters.KeyContainerName, 
                                         parameters.ProviderName, 
                                         (CapiNative.ProviderType)parameters.ProviderType,
                                         providerFlags); 
        }

#endif // SILVERLIGHT
 
#if !SILVERLIGHT
        private static RSAParameters RSAObjectToStruct (RSACspObject rsaCspObject) { 
            RSAParameters rsaParams = new RSAParameters(); 
            rsaParams.Exponent = rsaCspObject.Exponent;
            rsaParams.Modulus = rsaCspObject.Modulus; 
            rsaParams.P = rsaCspObject.P;
            rsaParams.Q = rsaCspObject.Q;
            rsaParams.DP = rsaCspObject.DP;
            rsaParams.DQ = rsaCspObject.DQ; 
            rsaParams.InverseQ = rsaCspObject.InverseQ;
            rsaParams.D = rsaCspObject.D; 
            return rsaParams; 
        }
 
        private static RSACspObject RSAStructToObject (RSAParameters rsaParams) {
            RSACspObject rsaCspObject = new RSACspObject();
            rsaCspObject.Exponent = rsaParams.Exponent;
            rsaCspObject.Modulus = rsaParams.Modulus; 
            rsaCspObject.P = rsaParams.P;
            rsaCspObject.Q = rsaParams.Q; 
            rsaCspObject.DP = rsaParams.DP; 
            rsaCspObject.DQ = rsaParams.DQ;
            rsaCspObject.InverseQ = rsaParams.InverseQ; 
            rsaCspObject.D = rsaParams.D;
            return rsaCspObject;
        }
 
        // find whether an RSA key blob is public.
        private static bool IsPublic (byte[] keyBlob) { 
            if (keyBlob == null) 
                throw new ArgumentNullException("keyBlob");
            Contract.EndContractBlock(); 

            // The CAPI RSA public key representation consists of the following sequence:
            //  - BLOBHEADER
            //  - RSAPUBKEY 

            // The first should be PUBLICKEYBLOB and magic should be RSA_PUB_MAGIC "RSA1" 
            if (keyBlob[0] != Constants.PUBLICKEYBLOB) 
                return false;
 
            if (keyBlob[11] != 0x31 || keyBlob[10] != 0x41 || keyBlob[9] != 0x53 || keyBlob[8] != 0x52)
                return false;

            return true; 
        }
#endif // !SILVERLIGHT 
 
        // Since P is required, we will assume its presence is synonymous to a private key.
        private static bool IsPublic(RSAParameters rsaParams) { 
            return (rsaParams.P == null);
        }
    }
} 

