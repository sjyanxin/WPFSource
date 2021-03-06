// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
/*============================================================
** 
** Class:  UnSafeBuffer 
**
** Purpose: A class to detect incorrect usage of UnSafeBuffer 
**
**
===========================================================*/
 
namespace System {
    using System.Diagnostics; 
    using System.Diagnostics.Contracts; 

    unsafe internal struct UnSafeCharBuffer{ 
        char * m_buffer;
        int m_totalSize;
        int m_length;
 
        [System.Security.SecurityCritical]  // auto-generated
        public UnSafeCharBuffer( char *buffer,  int bufferSize) { 
            Contract.Assert( buffer != null, "buffer pointer can't be null."  ); 
            Contract.Assert( bufferSize >= 0, "buffer size can't be negative."  );
            m_buffer = buffer; 
            m_totalSize = bufferSize;
            m_length = 0;
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public void AppendString(string stringToAppend) { 
            if( String.IsNullOrEmpty( stringToAppend ) ) { 
                return;
            } 

            if ( (m_totalSize - m_length) < stringToAppend.Length ) {
                throw new IndexOutOfRangeException();
            } 

            fixed( char* pointerToString = stringToAppend ) { 
                Buffer.memcpyimpl( (byte *) pointerToString, (byte*) (m_buffer + m_length), stringToAppend.Length * sizeof(char)); 
            }
 
            m_length += stringToAppend.Length;
            Contract.Assert(m_length <= m_totalSize, "Buffer has been overflowed!");
        }
 
        public int Length {
            get { 
                return m_length; 
            }
        } 
    }
}

