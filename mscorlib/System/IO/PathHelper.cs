using System; 
using System.Collections;
using System.Text;
using Microsoft.Win32;
using System.Runtime.InteropServices; 
using System.Runtime.CompilerServices;
using System.Globalization; 
using System.Runtime.Versioning; 
using System.Diagnostics.Contracts;
 
namespace System.IO {

    // ABOUT:
    // Helps with path normalization; support allocating on the stack or heap 
    //
    // PathHelper can't stackalloc the array for obvious reasons; you must pass 
    // in an array of chars allocated on the stack. 
    //
    // USAGE: 
    // Suppose you need to represent a char array of length len. Then this is the
    // suggested way to instantiate PathHelper:
    // ****************************************************************************
    // PathHelper pathHelper; 
    // if (charArrayLength less than stack alloc threshold == Path.MaxPath)
    //     char* arrayPtr = stackalloc char[Path.MaxPath]; 
    //     pathHelper = new PathHelper(arrayPtr); 
    // else
    //     pathHelper = new PathHelper(capacity, maxPath); 
    // ***************************************************************************
    //
    // note in the StringBuilder ctor:
    // - maxPath may be greater than Path.MaxPath (for isolated storage) 
    // - capacity may be greater than maxPath. This is even used for non-isolated
    //   storage scenarios where we want to temporarily allow strings greater 
    //   than Path.MaxPath if they can be normalized down to Path.MaxPath. This 
    //   can happen if the path contains escape characters "..".
    // 
    unsafe internal class PathHelper {   // should not be serialized

        // maximum size, max be greater than max path if contains escape sequence
        private int m_capacity; 
        // current length (next character position)
        private int m_length; 
        // max path, may be less than capacity 
        private int m_maxPath;
 
        // ptr to stack alloc'd array of chars
        private char* m_arrayPtr;

        // StringBuilder 
        private StringBuilder m_sb;
 
        // whether to operate on stack alloc'd or heap alloc'd array 
        private bool useStackAlloc;
 
        // Instantiates a PathHelper with a stack alloc'd array of chars
        [System.Security.SecurityCritical]
        internal PathHelper(char* charArrayPtr, int length) {
            Contract.Requires(charArrayPtr != null); 
            // force callers to be aware of this
            Contract.Requires(length == Path.MaxPath); 
 
            this.m_arrayPtr = charArrayPtr;
            this.m_capacity = length; 
            this.m_maxPath = Path.MaxPath;
            useStackAlloc = true;
        }
 
        // Instantiates a PathHelper with a heap alloc'd array of ints. Will create a StringBuilder
        internal PathHelper(int capacity, int maxPath) { 
            this.m_sb = new StringBuilder(capacity); 
            this.m_capacity = capacity;
            this.m_maxPath = maxPath; 
        }

        internal int Length {
            get { 
                if (useStackAlloc) {
                    return m_length; 
                } 
                else {
                    return m_sb.Length; 
                }
            }
            set {
                if (useStackAlloc) { 
                    m_length = value;
                } 
                else { 
                    m_sb.Length = value;
                } 
            }
        }

        internal int Capacity { 
            get {
                return m_capacity; 
            } 
        }
 
        internal char this[int index] {
            [System.Security.SecurityCritical]
            get {
                Contract.Requires(index >= 0 && index < Length); 
                if (useStackAlloc) {
                    return m_arrayPtr[index]; 
                } 
                else {
                    return m_sb[index]; 
                }
            }
            [System.Security.SecurityCritical]
            set { 
                Contract.Requires(index >= 0 && index < Length);
                if (useStackAlloc) { 
                    m_arrayPtr[index] = value; 
                }
                else { 
                    m_sb[index] = value;
                }
            }
        } 

        [System.Security.SecurityCritical] 
        internal unsafe void Append(char value) { 
            if (Length + 1 >= m_capacity)
                throw new PathTooLongException(Environment.GetResourceString("IO.PathTooLong")); 

            if (useStackAlloc) {
                m_arrayPtr[Length] = value;
                m_length++; 
            }
            else { 
                m_sb.Append(value); 
            }
        } 

        [System.Security.SecurityCritical]
        internal unsafe int GetFullPathName() {
            if (useStackAlloc) { 
                char* finalBuffer = stackalloc char[Path.MaxPath + 1];
                int result = Win32Native.GetFullPathName(m_arrayPtr, Path.MaxPath + 1, finalBuffer, IntPtr.Zero); 
 
                // If success, the return buffer length does not account for the terminating null character.
                // If in-sufficient buffer, the return buffer length does account for the path + the terminating null character. 
                // If failure, the return buffer length is zero
                if (result > Path.MaxPath) {
                    char* tempBuffer = stackalloc char[result];
                    finalBuffer = tempBuffer; 
                    result = Win32Native.GetFullPathName(m_arrayPtr, result, finalBuffer, IntPtr.Zero);
                } 
 
                // Full path is genuinely long
                if (result >= Path.MaxPath) 
                    throw new PathTooLongException(Environment.GetResourceString("IO.PathTooLong"));

                Contract.Assert(result < Path.MaxPath, "did we accidently remove a PathTooLongException check?");
                if (result == 0 && m_arrayPtr[0] != '\0') { 
                    __Error.WinIOError();
                } 
 
                else if (result < Path.MaxPath) {
                    // Null terminate explicitly (may be only needed for some cases such as empty strings) 
                    // GetFullPathName return length doesn't account for null terminating char...
                    finalBuffer[result] = '\0'; // Safe to write directly as result is < Path.MaxPath
                }
 
                Buffer.memcpy(finalBuffer, 0, m_arrayPtr, 0, result);
                // Doesn't account for null terminating char. Think of this as the last 
                // valid index into the buffer but not the length of the buffer 
                Length = result;
                return result; 
            }
            else {
                StringBuilder finalBuffer = new StringBuilder(m_capacity + 1);
                int result = Win32Native.GetFullPathName(m_sb.ToString(), m_capacity + 1, finalBuffer, IntPtr.Zero); 

                // If success, the return buffer length does not account for the terminating null character. 
                // If in-sufficient buffer, the return buffer length does account for the path + the terminating null character. 
                // If failure, the return buffer length is zero
                if (result > m_maxPath) { 
                    finalBuffer.Length = result;
                    result = Win32Native.GetFullPathName(m_sb.ToString(), result, finalBuffer, IntPtr.Zero);
                }
 
                // Fullpath is genuinely long
                if (result >= m_maxPath) 
                    throw new PathTooLongException(Environment.GetResourceString("IO.PathTooLong")); 

                Contract.Assert(result < m_maxPath, "did we accidentally remove a PathTooLongException check?"); 
                if (result == 0 && m_sb[0] != '\0') {
                    if (Length >= m_maxPath) {
                        throw new PathTooLongException(Environment.GetResourceString("IO.PathTooLong"));
                    } 
                    __Error.WinIOError();
                } 
                m_sb = finalBuffer; 
                return result;
            } 
        }

        [System.Security.SecurityCritical]
        internal unsafe bool TryExpandShortFileName() { 
            if (useStackAlloc) {
                NullTerminate(); 
                char* buffer = UnsafeGetArrayPtr(); 
                char* shortFileNameBuffer = stackalloc char[Path.MaxPath + 1];
 
                int r = Win32Native.GetLongPathName(buffer, shortFileNameBuffer, Path.MaxPath);

                // If success, the return buffer length does not account for the terminating null character.
                // If in-sufficient buffer, the return buffer length does account for the path + the terminating null character. 
                // If failure, the return buffer length is zero
                if (r >= Path.MaxPath) 
                    throw new PathTooLongException(Environment.GetResourceString("IO.PathTooLong")); 

                if (r == 0) { 
                    // Note: GetLongPathName will return ERROR_INVALID_FUNCTION on a
                    // path like \\.\PHYSICALDEVICE0 - some device driver doesn't
                    // support GetLongPathName on that string.  This behavior is
                    // by design, according to the Core File Services team. 
                    // We also get ERROR_NOT_ENOUGH_QUOTA in SQL_CLR_STRESS runs
                    // intermittently on paths like D:\DOCUME~1\user\LOCALS~1\Temp\ 
                    return false; 
                }
 
                // Safe to copy as we have already done Path.MaxPath bound checking
                Buffer.memcpy(shortFileNameBuffer, 0, buffer, 0, r);
                Length = r;
                // We should explicitly null terminate as in some cases the long version of the path 
                // might actually be shorter than what we started with because of Win32's normalization
                // Safe to write directly as bufferLength is guaranteed to be < Path.MaxPath 
                NullTerminate(); 
                return true;
            } 
            else {
                StringBuilder sb = GetStringBuilder();

                String origName = sb.ToString(); 
                String tempName = origName;
                bool addedPrefix = false; 
                if (tempName.Length > Path.MaxPath) { 
                    tempName = Path.AddLongPathPrefix(tempName);
                    addedPrefix = true; 
                }
                sb.Capacity = m_capacity;
                sb.Length = 0;
                int r = Win32Native.GetLongPathName(tempName, sb, m_capacity); 

                if (r == 0) { 
                    // Note: GetLongPathName will return ERROR_INVALID_FUNCTION on a 
                    // path like \\.\PHYSICALDEVICE0 - some device driver doesn't
                    // support GetLongPathName on that string.  This behavior is 
                    // by design, according to the Core File Services team.
                    // We also get ERROR_NOT_ENOUGH_QUOTA in SQL_CLR_STRESS runs
                    // intermittently on paths like D:\DOCUME~1\user\LOCALS~1\Temp\
                    sb.Length = 0; 
                    sb.Append(origName);
                    return false; 
                } 

                if (addedPrefix) 
                    r -= 4;

                // If success, the return buffer length does not account for the terminating null character.
                // If in-sufficient buffer, the return buffer length does account for the path + the terminating null character. 
                // If failure, the return buffer length is zero
                if (r >= m_maxPath) 
                    throw new PathTooLongException(Environment.GetResourceString("IO.PathTooLong")); 

 
                sb = Path.RemoveLongPathPrefix(sb);
                Length = sb.Length;
                return true;
 
            }
        } 
 
        [System.Security.SecurityCritical]
        internal unsafe void Fixup(int lenSavedName, int lastSlash) { 
            if (useStackAlloc) {
                char* savedName = stackalloc char[lenSavedName];
                Buffer.memcpy(m_arrayPtr, lastSlash + 1, savedName, 0, lenSavedName);
                Length = lastSlash; 
                NullTerminate();
                bool r = TryExpandShortFileName(); 
                // Clean up changes made to the newBuffer. 
                Append(Path.DirectorySeparatorChar);
                if (Length + lenSavedName >= Path.MaxPath) 
                    throw new PathTooLongException(Environment.GetResourceString("IO.PathTooLong"));
                Buffer.memcpy(savedName, 0, m_arrayPtr, Length, lenSavedName);
                Length = Length + lenSavedName;
 
            }
            else { 
                String savedName = m_sb.ToString(lastSlash + 1, lenSavedName); 
                Length = lastSlash;
                bool r = TryExpandShortFileName(); 
                // Clean up changes made to the newBuffer.
                Append(Path.DirectorySeparatorChar);
                if (Length + lenSavedName >= m_maxPath)
                    throw new PathTooLongException(Environment.GetResourceString("IO.PathTooLong")); 
                m_sb.Append(savedName);
            } 
        } 

        [System.Security.SecurityCritical] 
        internal unsafe bool OrdinalStartsWith(String compareTo, bool ignoreCase) {
            if (Length < compareTo.Length)
                return false;
 
            if (useStackAlloc) {
                NullTerminate(); 
                if (ignoreCase) { 
                    String s = new String(m_arrayPtr, 0, compareTo.Length);
                    return compareTo.Equals(s, StringComparison.OrdinalIgnoreCase); 
                }
                else {
                    for (int i = 0; i < compareTo.Length; i++) {
                        if (m_arrayPtr[i] != compareTo[i]) { 
                            return false;
                        } 
                    } 
                    return true;
                } 
            }
            else {
                if (ignoreCase) {
                    return m_sb.ToString().StartsWith(compareTo, StringComparison.OrdinalIgnoreCase); 
                }
                else { 
                    return m_sb.ToString().StartsWith(compareTo, StringComparison.Ordinal); 
                }
            } 
        }

        [System.Security.SecuritySafeCritical]
        public override String ToString() { 
            if (useStackAlloc) {
                return new String(m_arrayPtr, 0, Length); 
            } 
            else {
                return m_sb.ToString(); 
            }
        }

        [System.Security.SecurityCritical] 
        private unsafe char* UnsafeGetArrayPtr() {
            Contract.Assert(useStackAlloc, "This should never be called for PathHelpers wrapping a StringBuilder"); 
            return m_arrayPtr; 
        }
 
        private StringBuilder GetStringBuilder() {
            Contract.Assert(!useStackAlloc, "This should never be called for PathHelpers that wrap a stackalloc'd buffer");
            return m_sb;
        } 

        [System.Security.SecurityCritical] 
        private unsafe void NullTerminate() { 
            Contract.Assert(useStackAlloc, "This should never be called for PathHelpers wrapping a StringBuilder");
            m_arrayPtr[m_length] = '\0'; 
        }

    }
} 

