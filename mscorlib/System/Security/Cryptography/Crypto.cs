// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
// <OWNER>[....]</OWNER>
// 
 
//
// Crypto.cs 
//

namespace System.Security.Cryptography {
#if !SILVERLIGHT 
    using Microsoft.Win32;
    using System.Runtime.Serialization; 
    using System.Globalization; 
#endif // !SILVERLIGHT
 
    // This enum represents cipher chaining modes: cipher block chaining (CBC),
    // electronic code book (ECB), output feedback (OFB), cipher feedback (CFB),
    // and ciphertext-stealing (CTS).  Not all implementations will support all modes.
    [Serializable] 
    [System.Runtime.InteropServices.ComVisible(true)]
    public enum CipherMode {            // Please keep in [....] with wincrypt.h 
        CBC = 1, 
        ECB = 2,
        OFB = 3, 
        CFB = 4,
        CTS = 5
    }
 
    // This enum represents the padding method to use for filling out short blocks.
    // "None" means no padding (whole blocks required). 
    // "PKCS7" is the padding mode defined in RFC 2898, Section 6.1.1, Step 4, generalized 
    // to whatever block size is required.
    // "Zeros" means pad with zero bytes to fill out the last block. 
    // "ISO 10126" is the same as PKCS5 except that it fills the bytes before the last one with
    // random bytes. "ANSI X.923" fills the bytes with zeros and puts the number of padding
    // bytes in the last byte.
 
    [Serializable]
[System.Runtime.InteropServices.ComVisible(true)] 
    public enum PaddingMode { 
        None = 1,
        PKCS7 = 2, 
        Zeros = 3,
        ANSIX923 = 4,
        ISO10126 = 5
    } 

#if !SILVERLIGHT 
    // This structure is used for returning the set of legal key sizes and 
    // block sizes of the symmetric algorithms.
    // Note: this class should be sealed, otherwise someone could sub-class it and the read-only 
    // properties we depend on can have setters. Ideally, we should have a struct here (value type)
    // but we use what we have now and try to close the hole allowing someone to specify an invalid key size
#if !FEATURE_CORECLR
[System.Runtime.InteropServices.ComVisible(true)] 
#endif // !FEATURE_CORECLR
    public sealed class KeySizes { 
        private int m_minSize; 
        private int m_maxSize;
        private int m_skipSize; 

        public int MinSize {
            get { return m_minSize; }
        } 

        public int MaxSize { 
            get { return m_maxSize; } 
        }
 
        public int SkipSize {
            get { return m_skipSize; }
        }
 
        public KeySizes(int minSize, int maxSize, int skipSize) {
            m_minSize = minSize; m_maxSize = maxSize; m_skipSize = skipSize; 
        } 
    }
#endif // !SILVERLIGHT 

#if !SILVERLIGHT
    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)] 
    public class CryptographicException : SystemException {
        private const int FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200; 
        private const int FORMAT_MESSAGE_FROM_SYSTEM    = 0x00001000; 
        private const int FORMAT_MESSAGE_ARGUMENT_ARRAY = 0x00002000;
 
        public CryptographicException()
            : base(Environment.GetResourceString("Arg_CryptographyException")) {
            SetErrorCode(__HResults.CORSEC_E_CRYPTO);
        } 

        public CryptographicException(String message) 
            : base(message) { 
            SetErrorCode(__HResults.CORSEC_E_CRYPTO);
        } 

        public CryptographicException(String format, String insert)
            : base(String.Format(CultureInfo.CurrentCulture, format, insert)) {
            SetErrorCode(__HResults.CORSEC_E_CRYPTO); 
        }
 
        public CryptographicException(String message, Exception inner) 
            : base(message, inner) {
            SetErrorCode(__HResults.CORSEC_E_CRYPTO); 
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        public CryptographicException(int hr) 
            : this(Win32Native.GetMessage(hr)) {
            if ((hr & 0x80000000) != 0x80000000) 
                hr = (hr & 0x0000FFFF) | unchecked((int)0x80070000); 
            SetErrorCode(hr);
        } 

#if FEATURE_SERIALIZATION
        [System.Security.SecuritySafeCritical]  // auto-generated
        protected CryptographicException(SerializationInfo info, StreamingContext context) : base (info, context) {} 
#endif // FEATURE_SERIALIZATION
 
        // This method is only called from inside the VM. 
        private static void ThrowCryptographicException (int hr) {
            throw new CryptographicException(hr); 
        }
    }

    [Serializable()] 
[System.Runtime.InteropServices.ComVisible(true)]
    public class CryptographicUnexpectedOperationException : CryptographicException { 
 
        public CryptographicUnexpectedOperationException()
            : base() { 
            SetErrorCode(__HResults.CORSEC_E_CRYPTO_UNEX_OPER);
        }

        public CryptographicUnexpectedOperationException(String message) 
            : base(message) {
            SetErrorCode(__HResults.CORSEC_E_CRYPTO_UNEX_OPER); 
        } 

        public CryptographicUnexpectedOperationException(String format, String insert) 
            : base(String.Format(CultureInfo.CurrentCulture, format, insert)) {
            SetErrorCode(__HResults.CORSEC_E_CRYPTO_UNEX_OPER);
        }
 
        public CryptographicUnexpectedOperationException(String message, Exception inner)
            : base(message, inner) { 
            SetErrorCode(__HResults.CORSEC_E_CRYPTO_UNEX_OPER); 
        }
 
#if FEATURE_SERIALIZATION
        [System.Security.SecuritySafeCritical]  // auto-generated
        protected CryptographicUnexpectedOperationException(SerializationInfo info, StreamingContext context) : base (info, context) {}
#endif // FEATURE_SERIALIZATION 
    }
#endif // !SILVERLIGHT 
 
#if SILVERLIGHT
    // 
    // The cryptography sources are from mscorlib, so they use several constructs which are not available in
    // non-msocrlib assemblies, such as System.Prototype.dll - where they ship in Silverlight. The following
    // are shim classes which have the interface of mscorlib constructs, but redirect to their non-mscorlib
    // equivilents on Silverlight builds.  This prevents us from having to put any more #ifdefs in the 
    // source than we necessary.
    // 
 
    internal static class Buffer {
        internal static void InternalBlockCopy(Array source, 
                                               int sourceOffset,
                                               Array destination,
                                               int destinationOffset,
                                               int count) { 
            global::System.Buffer.BlockCopy(source, sourceOffset, destination, destinationOffset, count);
        } 
 
        internal static void BlockCopy(Array source,
                                       int sourceOffset, 
                                       Array destination,
                                       int destinationOffset,
                                       int count) {
            global::System.Buffer.BlockCopy(source, sourceOffset, destination, destinationOffset, count); 
        }
 
        internal static unsafe void memcpyimpl(byte* src, byte* dest, int len) { 
            // Just use a naive implementation since memcpyimpl isn't available outside mscorlib.
            for (; len > 0; --len, ++dest, ++src) { 
                *dest = *src;
            }
        }
    } 

    internal static class Environment { 
        internal static OperatingSystem OSVersion { 
            get { return global::System.Environment.OSVersion; }
        } 

        internal static string GetResourceString(string message) {
            return SR.GetString(message);
        } 

        internal static string GetResourceString(string message, params object[] args) { 
            return SR.GetString(message, args); 
        }
    } 
#endif
}

